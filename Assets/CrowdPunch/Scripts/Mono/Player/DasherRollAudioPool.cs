using UnityEngine;

namespace CrowdPunch.Mono.Player
{
    internal sealed class DasherRollAudioPool
    {
        private const int Capacity = 4;
        private const float AudibleRange = 36f;
        private const float FullVolumeRange = 6f;
        private readonly Voice[] voices = new Voice[Capacity];
        private readonly Sample[] samples = new Sample[Capacity];
        private readonly Transform player;
        private readonly Transform audioCamera;
        private int sampleCount;

        private struct Voice
        {
            public AudioSource Source;
            public ulong Id;
        }

        private struct Sample
        {
            public ulong Id;
            public Vector3 Offset;
            public float Gain;
            public float RollSpeed;
            public float Intensity;
        }

        public DasherRollAudioPool(Transform parent, Transform player, Transform audioCamera, AudioClip loop)
        {
            this.player = player;
            this.audioCamera = audioCamera;
            for (int i = 0; i < voices.Length; i++)
            {
                var child = new GameObject("Dasher Roll SFX");
                child.transform.SetParent(parent, false);
                var source = child.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = true;
                source.priority = 112;
                source.clip = loop;
                voices[i].Source = source;
            }
        }

        public void Begin() => sampleCount = 0;

        public void Show(ulong id, Vector3 position, float rollSpeed, float intensity)
        {
            Vector3 offset = position - player.position;
            offset.y = 0f;
            float gain = Mathf.InverseLerp(AudibleRange, FullVolumeRange, offset.magnitude);
            if (gain <= 0f) return;
            var sample = new Sample { Id = id, Offset = offset, Gain = gain, RollSpeed = rollSpeed, Intensity = intensity };
            if (sampleCount < samples.Length)
            {
                samples[sampleCount++] = sample;
                return;
            }

            // Keep the nearest audible rolls when several enemies charge together.
            int weakest = 0;
            for (int i = 1; i < sampleCount; i++)
                if (samples[i].Offset.sqrMagnitude > samples[weakest].Offset.sqrMagnitude) weakest = i;
            if (sample.Offset.sqrMagnitude < samples[weakest].Offset.sqrMagnitude) samples[weakest] = sample;
        }

        public void End()
        {
            float fadeStep = 12f * Time.unscaledDeltaTime;
            for (int i = 0; i < voices.Length; i++)
            {
                ref var voice = ref voices[i];
                if (voice.Id == 0) continue;
                int sample = FindSample(voice.Id);
                if (sample >= 0)
                {
                    UpdateVoice(voice.Source, samples[sample], fadeStep);
                    samples[sample].Id = 0;
                }
                else
                {
                    voice.Source.volume = Mathf.MoveTowards(voice.Source.volume, 0f, fadeStep);
                    if (voice.Source.volume <= 0f)
                    {
                        voice.Source.Stop();
                        voice.Id = 0;
                    }
                }
            }

            for (int i = 0; i < sampleCount; i++)
            {
                var sample = samples[i];
                if (sample.Id == 0) continue;
                for (int slot = 0; slot < voices.Length; slot++)
                {
                    ref var voice = ref voices[slot];
                    if (voice.Id != 0) continue;
                    voice.Id = sample.Id;
                    voice.Source.volume = 0f;
                    UpdateVoice(voice.Source, sample, 0f);
                    voice.Source.Play();
                    break;
                }
            }
        }

        public void Clear()
        {
            sampleCount = 0;
            for (int i = 0; i < voices.Length; i++)
            {
                voices[i].Source.Stop();
                voices[i].Id = 0;
            }
        }

        private int FindSample(ulong id)
        {
            for (int i = 0; i < sampleCount; i++)
                if (samples[i].Id == id) return i;
            return -1;
        }

        private void UpdateVoice(AudioSource source, Sample sample, float fadeStep)
        {
            source.pitch = .65f * sample.RollSpeed;
            float volume = sample.Gain * Mathf.Lerp(.16f, .42f, sample.Intensity);
            source.volume = Mathf.MoveTowards(source.volume, volume, fadeStep);
            source.panStereo = audioCamera != null ? .55f * Vector3.Dot(audioCamera.right, sample.Offset.normalized) : 0f;
        }
    }
}
