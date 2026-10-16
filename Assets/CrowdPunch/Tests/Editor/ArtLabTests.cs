using CrowdPunch.Systems.Presentation;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

namespace CrowdPunch.Tests
{
    public sealed class ArtLabTests
    {
        [Test]
        public void Vision005_CharacterAndEnvironmentChannelsSwitchIndependently()
        {
            var environment=new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var enemy=new Material(Shader.Find("CrowdPunch/EnemySkinning"));
            var player=new Material(Shader.Find("Shader Graphs/Sidekick_ShaderGraph"));
            try
            {
                ArtLabPreview.Enabled=true;
                for(int style=1;style<=4;style++)
                {
                    ArtLabPreview.EnvironmentStyle=0;ArtLabPreview.CharacterStyle=style;
                    Assert.That(ArtLabPreview.Variant(environment),Is.SameAs(environment));
                    var enemyPreview=ArtLabPreview.Variant(enemy);
                    var playerPreview=ArtLabPreview.Variant(player);
                    Assert.That(enemyPreview.shader.name,Is.EqualTo("CrowdPunch/ArtLab/Enemy"));
                    Assert.That(playerPreview.shader.name,Is.EqualTo("CrowdPunch/ArtLab/Player"));
                    ArtLabPreview.EnvironmentStyle=style;ArtLabPreview.CharacterStyle=0;
                    Assert.That(ArtLabPreview.Variant(environment).shader.name,Is.EqualTo("CrowdPunch/ArtLab/Environment"));
                    Assert.That(ArtLabPreview.Variant(enemyPreview),Is.SameAs(enemy));
                    Assert.That(ArtLabPreview.Variant(playerPreview),Is.SameAs(player));
                }
                ArtLabPreview.Enabled=false;
                Assert.That(ArtLabPreview.Variant(environment),Is.SameAs(environment));
                Assert.That(enemy.shader.name,Is.EqualTo("CrowdPunch/EnemySkinning"));
                Assert.That(player.shader.name,Is.EqualTo("Shader Graphs/Sidekick_ShaderGraph"));
            }
            finally
            {
                ArtLabPreview.Enabled=false;ArtLabPreview.DisposeMaterials();
                Object.DestroyImmediate(environment);Object.DestroyImmediate(enemy);Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void Vision005_PreviewPreservesMeshRangesColoursAndNewSpawnsThenRestores()
        {
            using var world=new World("Art Lab presentation test");
            var system=world.GetOrCreateSystemManaged<ArtLabMaterialSystem>();
            var material=new Material(Shader.Find("Universal Render Pipeline/Lit")) {color=Color.green};
            var mesh=new Mesh();
            try
            {
                var manager=world.EntityManager;
                var ranges=new[]{new MaterialMeshIndex{MaterialIndex=0,MeshIndex=0,SubMeshIndex=0}};
                var array=new RenderMeshArray(new[]{material},new[]{mesh},ranges);
                var entity=manager.CreateEntity(typeof(MaterialMeshInfo),typeof(URPMaterialPropertyBaseColor),typeof(Prefab));
                manager.AddSharedComponentManaged(entity,array);
                var info=MaterialMeshInfo.FromMaterialMeshIndexRange(0,1);manager.SetComponentData(entity,info);
                var colour=new float4(.2f,.4f,.6f,1);manager.SetComponentData(entity,new URPMaterialPropertyBaseColor{Value=colour});
                ArtLabPreview.Enabled=true;ArtLabPreview.EnvironmentStyle=2;ArtLabPreview.CharacterStyle=2;ArtLabPreview.Revision++;
                system.Update();
                var replaced=manager.GetSharedComponentManaged<RenderMeshArray>(entity);
                Assert.That(replaced.MaterialReferences[0].Value,Is.Not.SameAs(material));
                Assert.That(replaced.MaterialReferences[0].Value.shader.name,Is.EqualTo("CrowdPunch/ArtLab/Environment"));
                Assert.That(replaced.MeshReferences[0].Value,Is.SameAs(mesh));
                Assert.That(replaced.MaterialMeshIndices[0].SubMeshIndex,Is.EqualTo(0));
                Assert.That(manager.GetComponentData<MaterialMeshInfo>(entity).MaterialMeshIndexRange,Is.EqualTo(info.MaterialMeshIndexRange));
                Assert.That(manager.GetComponentData<URPMaterialPropertyBaseColor>(entity).Value,Is.EqualTo(colour));
                var spawned=manager.Instantiate(entity);
                Assert.That(manager.GetSharedComponentManaged<RenderMeshArray>(spawned),Is.EqualTo(replaced));
                ArtLabPreview.Enabled=false;system.Update();
                Assert.That(manager.GetSharedComponentManaged<RenderMeshArray>(spawned),Is.EqualTo(array));
                Assert.That(manager.GetSharedComponentManaged<RenderMeshArray>(entity),Is.EqualTo(array));
                Assert.That(material.color,Is.EqualTo(Color.green));
                Assert.That(material.shader.name,Is.EqualTo("Universal Render Pipeline/Lit"));
                Assert.That(system.ReplacedArrays,Is.Zero);
            }
            finally {ArtLabPreview.Enabled=false;system.Restore();ArtLabPreview.DisposeMaterials();Object.DestroyImmediate(material);Object.DestroyImmediate(mesh);}
        }
    }
}
