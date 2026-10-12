using System.Linq;
using CrowdPunch.Authoring;
using CrowdPunch.Configuration;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CrowdPunch.Tests
{
    public sealed class CampaignBarricadeSectorTests
    {
        [Test] public void Barricade009_SectorsHaveDistinctAscendingLayoutsAndPreservedFinalObstacles()
        {
            int[] levels={22,34,42,56,62,74};
            Vector4[][] finalRocks={new[]{new Vector4(-7,-14,3,5),new Vector4(7,-9,3,5)},
                new[]{new Vector4(-5,-21,7,3),new Vector4(5,-9,7,3)},new[]{new Vector4(0,-13,7,12)},
                new[]{new Vector4(-5,-15,5,3)},new[]{new Vector4(-6,-18,3,7)},
                new[]{new Vector4(-5,-19,4,6),new Vector4(5,-9,4,6)}};
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                for(int index=0;index<levels.Length;index++)
                {
                    int level=levels[index];
                    EditorSceneManager.OpenScene($"Assets/CrowdPunch/Scenes/Campaign/Campaign_{level:00}/Campaign_{level:00} Sub Scene.unity");
                    var crowds=Object.FindObjectsByType<EnemyWaveSequenceAuthoring>(FindObjectsSortMode.None).OrderBy(c=>c.barricade.transform.position.z).ToArray();
                    var rocks=Object.FindObjectsByType<SolidObstacleAuthoring>(FindObjectsSortMode.None).Where(s=>!s.name.StartsWith("Gate terrain")).ToArray();
                    var patches=Object.FindObjectsByType<GroundHazardAuthoring>(FindObjectsSortMode.None);
                    int previousPopulation=0,previousTypes=0;float previousArea=0;
                    var signatures=new System.Collections.Generic.HashSet<string>();
                    for(int room=0;room<crowds.Length;room++)
                    {
                        var crowd=crowds[room];var bounds=crowd.GetComponent<BarricadeRoomAuthoring>();var wave=crowd.Waves.Single();
                        var local=rocks.Where(s=>s.transform.position.z>=bounds.minimum.y&&s.transform.position.z<=bounds.maximum.y)
                            .OrderBy(s=>s.transform.position.x).ToArray();
                        float area=local.Sum(s=>s.Size.x*s.Size.y);
                        Assert.That(area,Is.GreaterThan(previousArea),$"L{level} sector {room+1} must have more obstruction than the preceding sector.");
                        Assert.That(wave.TotalEnemyCount,Is.GreaterThan(previousPopulation));
                        Assert.That(wave.Enemies.Count,Is.GreaterThanOrEqualTo(previousTypes));
                        Assert.That(wave.Enemies.Single(e=>e.Settings.Archetype==EnemyArchetype.Baseline).MinimumCount,Is.GreaterThanOrEqualTo(6));
                        Assert.That(wave.ArmoredAmmunitionProfile,Is.Null,"Bounded replenishing Baselines provide ammunition without extra roots.");
                        var signature=string.Join(";",local.Select(s=>$"{s.transform.position.x:F2}:{s.transform.position.z-crowd.barricade.transform.position.z:F2}:{s.Size}"));
                        Assert.That(signatures.Add(signature),Is.True,"Every sector needs a distinct obstacle layout.");
                        if(room<crowds.Length-1)
                        {
                            Assert.That(patches.Where(p=>p.sequence==crowd).All(p=>p.operation==CrowdPunch.Components.GroundHazardOperation.Periodic),Is.True);
                            Assert.That(patches.Count(p=>p.sequence==crowd),Is.LessThanOrEqualTo(patches.Count(p=>p.sequence==crowds.Last())));
                        }
                        else
                        {
                            Assert.That(local.Length,Is.EqualTo(finalRocks[index].Length));
                            for(int r=0;r<local.Length;r++)
                            {
                                var expected=finalRocks[index][r];
                                Assert.That(local[r].transform.position.x,Is.EqualTo(expected.x).Within(.001f));
                                Assert.That(local[r].transform.position.z-crowd.barricade.transform.position.z,Is.EqualTo(expected.y).Within(.001f));
                                Assert.That(local[r].Size,Is.EqualTo(new Vector2(expected.z,expected.w)));
                            }
                            Assert.That(wave.Enemies.Select(e=>e.Settings.Archetype),Is.EquivalentTo(new[]{EnemyArchetype.Baseline,EnemyArchetype.Explosive,EnemyArchetype.Ranged,EnemyArchetype.Dasher,EnemyArchetype.Armored}));
                            Assert.That(wave.Enemies.Single(e=>e.Settings.Archetype==EnemyArchetype.Explosive).MinimumCount,Is.EqualTo(2));
                        }
                        previousArea=area;previousPopulation=wave.TotalEnemyCount;previousTypes=wave.Enemies.Count;
                    }
                }
            }
            finally
            {
                if(setup.Any(s=>s.isLoaded&&s.isActive))EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            }
        }
    }
}
