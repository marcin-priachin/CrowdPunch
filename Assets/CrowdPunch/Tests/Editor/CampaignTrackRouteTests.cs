using System;
using System.Linq;
using CrowdPunch.Authoring;
using CrowdPunch.Components;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CrowdPunch.Tests
{
    public sealed class CampaignTrackRouteTests
    {
        private static readonly int[] Levels = {16,19,26,32,39,46,54,69};

        internal static Vector3[] Points(TrackObjectAuthoring track)
            => new[]{track.transform.position}.Concat((track.corners??Array.Empty<Transform>()).Select(t=>t.position)).Concat(new[]{track.destination.position}).ToArray();

        internal static void AssertProgressiveTrack(TrackObjectAuthoring track, int level)
        {
            int legs=Array.IndexOf(Levels,level)+1; Assert.That(legs,Is.GreaterThan(0));
            var points=Points(track);
            Assert.That(points.Length,Is.EqualTo(legs+1),$"Level {level} corner count");
            Assert.That(track.settings.requiredNetHits,Is.EqualTo(legs*5),"Each original 10m length retains five 2m steps.");
            float length=0;
            for(int i=1;i<points.Length;i++)
            {
                var delta=points[i]-points[i-1]; length+=delta.magnitude;
                Assert.That(delta.magnitude,Is.EqualTo(10).Within(.001)); Assert.That(delta.y,Is.Zero.Within(.001));
                if(i>1) Assert.That(Vector3.Dot(delta.normalized,(points[i-1]-points[i-2]).normalized),Is.Zero.Within(.001),"Every added leg must turn left or right.");
            }
            Assert.That(length,Is.EqualTo(legs*10).Within(.001));
            Assert.That(track.destination.GetComponentsInChildren<TrackSocketVisualAuthoring>().All(v=>v.trackObject==track),Is.True);
        }

        [Test] public void Track006_AllEightCampaignRoutesHaveProgressiveLengthAndAccessibleCorners()
        {
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                for(int index=0;index<Levels.Length;index++)
                {
                    int level=Levels[index];
                    EditorSceneManager.OpenScene($"Assets/CrowdPunch/Scenes/Campaign/Campaign_{level:00}/Campaign_{level:00} Sub Scene.unity");
                    var track=UnityEngine.Object.FindFirstObjectByType<TrackObjectAuthoring>(); AssertProgressiveTrack(track,level);
                    var points=Points(track);
                    var nav=UnityEngine.Object.FindFirstObjectByType<NavigationArenaAuthoring>(); CampaignChapterTwoTests.AssertNavigationClearance(nav,level);
                    var floor=GameObject.Find("Continuous Convex Floor"); var mesh=floor.GetComponent<MeshCollider>().sharedMesh;
                    Assert.That(floor.GetComponent<MeshCollider>().convex,Is.True);
                    var outline=mesh.vertices.Take(mesh.vertexCount/2).Select(v=>floor.transform.TransformPoint(v)).ToArray();
                    var crowd=UnityEngine.Object.FindFirstObjectByType<EnemyWaveSequenceAuthoring>();
                    Assert.That(crowd.Waves.Single().TotalEnemyCount,Is.EqualTo(new[]{8,10,12,10,10,12,16,12}[index]));
                    var patches=UnityEngine.Object.FindObjectsByType<GroundHazardAuthoring>(FindObjectsSortMode.None);
                    for(int segment=1;segment<points.Length;segment++) for(int sample=0;sample<=20;sample++)
                    {
                        var position=Vector3.Lerp(points[segment-1],points[segment],sample/20f);
                        for(int edge=0;edge<outline.Length;edge++)
                        {
                            var a=new Vector2(outline[edge].x,outline[edge].z);var b=new Vector2(outline[(edge+1)%outline.Length].x,outline[(edge+1)%outline.Length].z);
                            var p=new Vector2(position.x,position.z);var delta=b-a;
                            Assert.That(delta.x*(p.y-a.y)-delta.y*(p.x-a.x),Is.GreaterThanOrEqualTo(-.001),$"Level {level} rail outside floor.");
                            float t=Mathf.Clamp01(Vector2.Dot(p-a,delta)/delta.sqrMagnitude);
                            Assert.That(Vector2.Distance(p,a+delta*t),Is.GreaterThanOrEqualTo(4.5f),$"Level {level} lacks room around rail/corner.");
                        }
                        foreach(var wave in crowd.Waves) foreach(var bank in wave.SpawnRectangles)
                        {
                            float x=Mathf.Max(0,Mathf.Abs(position.x-bank.Center.x)-bank.Width*.5f),z=Mathf.Max(0,Mathf.Abs(position.z-bank.Center.z)-bank.Depth*.5f);
                            Assert.That(new Vector2(x,z).magnitude,Is.GreaterThanOrEqualTo(3.5f),$"Level {level} bank overlaps rail clearance.");
                        }
                        foreach(var hazard in patches)
                        {
                            float x=Mathf.Abs(position.x-hazard.transform.position.x),z=Mathf.Abs(position.z-hazard.transform.position.z);
                            float distance=hazard.shape==GroundHazardShape.Circle?new Vector2(x,z).magnitude-hazard.radius:
                                new Vector2(Mathf.Max(0,x-hazard.width*.5f),Mathf.Max(0,z-hazard.depth*.5f)).magnitude;
                            Assert.That(distance,Is.GreaterThanOrEqualTo(4),$"Level {level} hazard blocks rail setup.");
                        }
                    }
                    var marks=UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r=>r.name=="Track rail" || r.name=="Direction arrow").ToArray();
                    Assert.That(marks.Length,Is.EqualTo(index==0?22:18*(index+1)));
                    Assert.That(marks.All(r=>r.sharedMaterial!=null && r.bounds.max.y>-.99f),Is.True);
                    if(index>0)
                    {
                        var endpoints=nav.transform.Cast<Transform>().Where(t=>t.name=="Track rail")
                            .SelectMany(t=>new[]{t.position-t.forward*t.localScale.z*.5f,t.position+t.forward*t.localScale.z*.5f})
                            .GroupBy(p=>$"{Mathf.RoundToInt(p.x*1000)},{Mathf.RoundToInt(p.z*1000)}").ToArray();
                        Assert.That(endpoints.Count(g=>g.Count()==2),Is.EqualTo(2*index),"Both visible rails must join at every turn.");
                    }
                    if(index>0) Assert.That(Vector3.Dot(track.destination.forward,(points.Last()-points[points.Length-2]).normalized),Is.GreaterThan(.9999f));
                }
            }
            finally
            {
                if(setup.Any(s=>s.isLoaded && s.isActive)) EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            }
        }
    }
}
