#if UNITY_EDITOR
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using CrowdPunch.Systems.Groups;

namespace CrowdPunch.Systems.Presentation
{
    // Managed SystemBase is intentional: this development-only system owns RenderMeshArray
    // substitutions. Simulation, entity colours, deformation indices and mesh ranges stay intact.
    [UpdateInGroup(typeof(GamePresentationGroup))]
    public partial class ArtLabMaterialSystem : SystemBase
    {
        private EntityQuery renderers;
        private readonly Dictionary<RenderMeshArray,RenderMeshArray> originals = new();
        private readonly List<RenderMeshArray> arrays = new();
        private int revision=-1;
        private double nextScan;
        public int ReplacedArrays => originals.Count;

        protected override void OnCreate()
        {
            renderers=GetEntityQuery(new EntityQueryDesc {
                All=new[]{ComponentType.ReadOnly<RenderMeshArray>()},
                Options=EntityQueryOptions.IncludePrefab|EntityQueryOptions.IncludeDisabledEntities
            });
        }

        protected override void OnUpdate()
        {
            if(!ArtLabPreview.Enabled) { Restore(); return; }
            if(revision!=ArtLabPreview.Revision) {Restore();revision=ArtLabPreview.Revision;nextScan=0;}
            double now=UnityEngine.Time.realtimeSinceStartupAsDouble;
            if(now<nextScan) return;
            nextScan=now+.25;
            arrays.Clear();EntityManager.GetAllUniqueSharedComponentsManaged(arrays);
            using var commands=new EntityCommandBuffer(Allocator.Temp);
            foreach(var array in arrays)
            {
                if(array.MaterialReferences==null || originals.ContainsKey(array)) continue;
                var materials=(UnityObjectRef<UnityEngine.Material>[])array.MaterialReferences.Clone();
                bool changed=false;
                for(int i=0;i<materials.Length;i++)
                {
                    var source=materials[i].Value;var variant=ArtLabPreview.Variant(source);
                    if(variant==source) continue;
                    materials[i]=variant;changed=true;
                }
                if(!changed) continue;
                var replacement=new RenderMeshArray(materials,array.MeshReferences,array.MaterialMeshIndices);
                originals[replacement]=array;
                renderers.SetSharedComponentFilterManaged(array);
                commands.SetSharedComponentManaged(renderers,replacement,EntityQueryCaptureMode.AtRecord);
            }
            renderers.ResetFilter();commands.Playback(EntityManager);
        }

        public void Restore()
        {
            if(originals.Count==0) return;
            using var commands=new EntityCommandBuffer(Allocator.Temp);
            foreach(var pair in originals)
            {
                renderers.SetSharedComponentFilterManaged(pair.Key);
                commands.SetSharedComponentManaged(renderers,pair.Value,EntityQueryCaptureMode.AtRecord);
            }
            renderers.ResetFilter();commands.Playback(EntityManager);originals.Clear();
        }
    }
}
#endif
