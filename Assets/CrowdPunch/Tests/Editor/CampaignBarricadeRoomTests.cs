using System;
using System.Linq;
using CrowdPunch.Authoring;
using CrowdPunch.Components;
using CrowdPunch.Utilities;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CrowdPunch.Tests
{
    public sealed class CampaignBarricadeRoomTests
    {
        private static readonly int[] Levels={13,22,34,42,56,62,74},Lengths={42,40,44,44,42,44,46},Widths={30,30,32,34,32,36,38};
        internal static void AssertRooms(int level)
        {
            int index=Array.IndexOf(Levels,level),count=index+1;var gates=UnityEngine.Object.FindObjectsByType<BarricadeAuthoring>(FindObjectsSortMode.None).OrderBy(g=>g.transform.position.z).ToArray();
            Assert.That(gates.Length,Is.EqualTo(count));
            var sides=UnityEngine.Object.FindObjectsByType<SolidObstacleAuthoring>(FindObjectsSortMode.None).Where(s=>s.name.StartsWith("Gate terrain")).ToArray();
            foreach(var gate in gates)
            {
                Assert.That(gate.size.x,Is.EqualTo(2));Assert.That(gate.settings.requiredHits,Is.EqualTo(level<=22?3:4));Assert.That(gate.settings.replenishDelay,Is.EqualTo(4));
                var seal=sides.Where(s=>Mathf.Abs(s.transform.position.z-gate.transform.position.z-1.2f)<.01f).OrderBy(s=>s.transform.position.x).ToArray();
                Assert.That(seal.Length,Is.EqualTo(2));
                Assert.That(seal[0].transform.position.x+seal[0].Size.x*.5f,Is.EqualTo(gate.transform.position.x-1));Assert.That(seal[1].transform.position.x-seal[1].Size.x*.5f,Is.EqualTo(gate.transform.position.x+1));
                Assert.That(seal[0].transform.position.x-seal[0].Size.x*.5f,Is.LessThanOrEqualTo(-Widths[index]*.5f));Assert.That(seal[1].transform.position.x+seal[1].Size.x*.5f,Is.GreaterThanOrEqualTo(Widths[index]*.5f));
                foreach(var s in seal){float front=s.transform.position.z-s.Size.y*.5f;Assert.That(front,Is.GreaterThan(gate.transform.position.z-gate.size.z*.5f));Assert.That(front,Is.LessThan(gate.transform.position.z+gate.size.z*.5f));}
            }
            for(int i=0;i<count;i++)
            {
                Assert.That(gates[i].completeOnDestruction,Is.EqualTo(i<count-1));
                if(i>0)Assert.That(gates[i].transform.position.z-gates[i-1].transform.position.z,Is.EqualTo(Lengths[index]));
                if(i<count-1)Assert.That(gates[i].exit,Is.Null);
            }
            Assert.That(gates.Last().exit.position.z-gates.Last().transform.position.z,Is.EqualTo(5));
            Assert.That(gates.Last().exit.position.x,Is.EqualTo(level==62?8:0));
        }

        [Test] public void Barricade007_CampaignChainsHaveSealedRoomsLocalCrowdsAndConnectedSpawnRegions()
        {
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                for(int index=0;index<Levels.Length;index++)
                {
                    int level=Levels[index],count=index+1;EditorSceneManager.OpenScene($"Assets/CrowdPunch/Scenes/Campaign/Campaign_{level:00}/Campaign_{level:00} Sub Scene.unity");AssertRooms(level);
                    var arena=UnityEngine.Object.FindFirstObjectByType<ArenaAuthoring>();var nav=arena.GetComponent<NavigationArenaAuthoring>();CampaignChapterTwoTests.AssertNavigationClearance(nav,level);
                    var floor=GameObject.Find("Continuous Convex Floor").GetComponent<MeshCollider>();Assert.That(floor.convex,Is.True);
                    Assert.That(floor.sharedMesh.bounds.size.z,Is.EqualTo(Lengths[index]*count));
                    var gates=UnityEngine.Object.FindObjectsByType<BarricadeAuthoring>(FindObjectsSortMode.None).OrderBy(g=>g.transform.position.z).ToArray();
                    var crowds=UnityEngine.Object.FindObjectsByType<EnemyWaveSequenceAuthoring>(FindObjectsSortMode.None).OrderBy(c=>c.barricade.transform.position.z).ToArray();Assert.That(crowds.Length,Is.EqualTo(count));
                    var patches=UnityEngine.Object.FindObjectsByType<GroundHazardAuthoring>(FindObjectsSortMode.None);Assert.That(patches.Length,Is.EqualTo((level==34?1:level==42||level==62||level==74?2:0)*count));
                    Assert.That(UnityEngine.Object.FindObjectsByType<GroundHazardPolicyAuthoring>(FindObjectsSortMode.None).Length,Is.EqualTo(patches.Length>0?1:0));
                    var obstacles=arena.GetComponentsInChildren<SolidObstacleAuthoring>();var rectangles=new NativeArray<NavigationRectangle>(obstacles.Length,Allocator.Temp);
                    for(int i=0;i<obstacles.Length;i++){var center=((float3)obstacles[i].transform.position).xz;var half=(float2)obstacles[i].Size*.5f;rectangles[i]=new NavigationRectangle{Minimum=center-half,Maximum=center+half};}
                    float2 origin=((float3)arena.transform.position).xz+arena.SpacingCenterOffset.xz;
                    var blob=NavigationGridConstruction.Build(origin-arena.SpacingSize.xz*.5f,origin+arena.SpacingSize.xz*.5f,nav.settings.cellSize,(float3)nav.settings.clearanceRadii+nav.settings.clearanceMargin,rectangles,Allocator.Temp);rectangles.Dispose();
                    try
                    {
                        blob.Value.Margin=nav.settings.clearanceMargin;var grid=new NavigationGrid{Data=blob,ParticipationAnchor=nav.participationAnchor};
                        for(int room=0;room<count;room++)
                        {
                            Assert.That(crowds[room].barricade,Is.EqualTo(gates[room]));Assert.That(crowds[room].Waves.Count,Is.EqualTo(1));
                            Assert.That(crowds[room].Waves[0].TotalEnemyCount,Is.EqualTo(new[]{12,14,14,16,14,16,18}[index]));
                            var activation=crowds[room].GetComponent<BarricadeRoomAuthoring>();
                            if(count>1){Assert.That(activation,Is.Not.Null);Assert.That(activation.previousGate,Is.EqualTo(room==0?null:gates[room-1]));}
                            foreach(var bank in crowds[room].Waves[0].SpawnRectangles)
                            {
                                Assert.That(NavigationGeometry.SpawnAllowed(grid,((float3)bank.Center).xz,.5f),Is.True,$"Level {level} room {room+1} disconnected spawn bank.");
                                Assert.That(bank.Center.z,Is.LessThan(gates[room].transform.position.z));
                                if(room>0)Assert.That(bank.Center.z,Is.GreaterThan(gates[room-1].transform.position.z+3));
                            }
                            foreach(var patch in patches.Where(p=>p.sequence==crowds[room]))foreach(var bank in crowds[room].Waves[0].SpawnRectangles)
                            {
                                var half=patch.shape==GroundHazardShape.Circle?Vector2.one*patch.radius:new Vector2(patch.width,patch.depth)*.5f;
                                var d=new Vector2(Mathf.Abs(bank.Center.x-patch.transform.position.x),Mathf.Abs(bank.Center.z-patch.transform.position.z));
                                Assert.That(d.x>bank.Width*.5f+half.x+.5f||d.y>bank.Depth*.5f+half.y+.5f,Is.True);
                            }
                        }
                    }
                    finally{blob.Dispose();}
                }
            }
            finally{if(setup.Any(s=>s.isLoaded&&s.isActive))EditorSceneManager.RestoreSceneManagerSetup(setup);else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);}
        }
    }
}
