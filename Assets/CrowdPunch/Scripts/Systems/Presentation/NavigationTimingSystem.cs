using CrowdPunch.Components;
using CrowdPunch.Systems.Groups;
using Unity.Entities;
using Unity.Profiling;
namespace CrowdPunch.Systems.Presentation
{
    // Profiler collection stays outside the Burst routing update. No enemy query or Mono ownership.
    [UpdateInGroup(typeof(GamePresentationGroup))]
    public partial struct NavigationTimingSystem : ISystem
    {
        private ProfilerRecorder recorder;
        public void OnCreate(ref SystemState state)
        {
            recorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "CrowdPunch.Navigation", 1);
            state.RequireForUpdate<NavigationDiagnostics>();
        }
        public void OnDestroy(ref SystemState state) { recorder.Dispose(); }
        public void OnUpdate(ref SystemState state)
        {
            var settings = SystemAPI.GetSingleton<NavigationRuntimeSettings>();
            var diagnostics = SystemAPI.GetSingleton<NavigationDiagnostics>();
            diagnostics.Milliseconds = settings.Diagnostics != 0 && recorder.Valid ? recorder.LastValue / 1000000d : 0;
            SystemAPI.SetSingleton(diagnostics);
        }
    }
}
