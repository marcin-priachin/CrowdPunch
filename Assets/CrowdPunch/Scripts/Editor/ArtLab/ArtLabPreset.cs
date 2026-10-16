using UnityEngine;
using UnityEngine.Rendering;

namespace CrowdPunch.Editor.ArtLab
{
    public enum ArtLabStyle { Current, SoftToonHybrid, FullCel, GraphicComic, Miniature }

    public sealed class ArtLabPreset : ScriptableObject
    {
        public string title;
        [TextArea] public string description;
        public ArtLabStyle shading;
        public VolumeProfile postProcessing;
        public Color sunTint = Color.white;
        public float sunIntensityMultiplier = 1;
        public float ambientMultiplier = 1;
        public float fogDistanceMultiplier = 1;
    }
}
