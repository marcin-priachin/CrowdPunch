using System.IO;
using System.Linq;
using CrowdPunch.Authoring;
using CrowdPunch.Components;
using CrowdPunch.Editor;
using CrowdPunch.Systems.Presentation;
using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Deformations;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CrowdPunch.Tests
{
    public sealed class ChickenGoblinPresentationTests
    {
        [TestCase("Campaign", 20)] [TestCase("Campaign", 60)] [TestCase("Gauntlets", 20)]
        public void Chicken007_SavedWizardPreservesPrefabScalesAndValidSamples(string folder, int level)
        {
            string name = folder == "Campaign" ? "Campaign" : "Gauntlet";
            var scene = EditorSceneManager.OpenPreviewScene($"Assets/CrowdPunch/Scenes/{folder}/{name}_{level:00}/{name}_{level:00} Sub Scene.unity");
            try
            {
                var boss = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ChickenBossAuthoring>()).Single();
                var visual = boss.transform.Find("Chicken Model");
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(ChickenModelBuilder.ModelPath);
                Assert.AreEqual(source.transform.localScale, visual.localScale);
                foreach (var original in source.GetComponentsInChildren<Transform>(true))
                {
                    string path = AnimationUtility.CalculateTransformPath(original, source.transform);
                    var actual = path.Length == 0 ? visual : visual.Find(path);
                    Assert.IsNotNull(actual, path);
                    Assert.Less(Vector3.Distance(original.localScale, actual.localScale), .0001f, path);
                }
                var animations = visual.GetComponentsInChildren<EnemyAnimationAuthoring>(true);
                Assert.AreEqual(source.GetComponentsInChildren<Renderer>(true).Length, animations.Length,
                    "The body and every authored accessory need sampled skinning.");
                Assert.AreEqual(1, animations.Count(a => a.JogSamples != null), "Only the body's legs use jog samples.");
                foreach (var animation in animations)
                {
                    var renderer = animation.GetComponent<SkinnedMeshRenderer>();
                    var animator = animation.GetComponentInParent<Animator>();
                    using var reader = new BinaryReader(new MemoryStream(animation.Samples.bytes));
                    Assert.AreEqual(0x43504133, reader.ReadInt32());
                    string hash = AssetDatabase.GetAssetDependencyHash(ChickenModelBuilder.ControllerPath).ToString()
                        + AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(renderer.sharedMesh))
                        + AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(animator.avatar));
                    Assert.AreEqual(hash, reader.ReadString());
                    if(renderer.bones.Length>1)
                    {
                        int bones=reader.ReadInt32(), frames=reader.ReadInt32();
                        for(int i=0;i<bones;i++)reader.ReadString();
                        for(int i=0;i<EnemyAnimationSamples.MotionCount;i++)reader.ReadSingle();
                        long dataStart=reader.BaseStream.Position;
                        foreach(int motion in new[] { 6,7 })
                        {
                            var first=ReadMatrix(motion,0); var last=ReadMatrix(motion,frames-1);
                            Assert.Less(Quaternion.Angle(first.rotation,last.rotation),2f,
                                "Turn samples must remove the authored 90-degree root yaw; physics already faces the player.");
                        }
                        Matrix4x4 ReadMatrix(int motion,int frame)
                        {
                            reader.BaseStream.Position=dataStart+((motion*frames+frame)*bones)*48L;
                            var matrix=Matrix4x4.identity;
                            for(int column=0;column<4;column++)for(int row=0;row<3;row++)matrix[row,column]=reader.ReadSingle();
                            return matrix;
                        }
                    }
                    foreach (var material in renderer.sharedMaterials)
                    {
                        Assert.AreEqual("CrowdPunch/Cel/Enemy", material.shader.name);
                        Assert.IsNotNull(material.GetTexture("_ColorMap"));
                    }
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [TestCase(ChickenPhase.Rush)] [TestCase(ChickenPhase.Cast)]
        public void Chicken007_ActionMasksPreserveUpperBodyAndPhysics(ChickenPhase phase)
        {
            using var world = new World("Chicken masked jog");
            var em = world.EntityManager;
            var owner = em.CreateEntity(typeof(ChickenBoss), typeof(ChickenTuning), typeof(LocalTransform), typeof(PhysicsVelocity));
            em.SetComponentData(owner, new ChickenBoss { Phase=phase, Stage=1, ShotSequence=1, SinceShot=.1f,Remaining=.5f });
            em.AddComponentData(owner,new ChickenCastTuning { Duration=1 });
            em.AddComponentData(owner,new ChickenTurnState { YawRate=-1 });
            em.SetComponentData(owner, new ChickenTuning { WindUp=1, ShotReleasePhase=.5f, RushSpeed=10 });
            em.SetComponentData(owner, LocalTransform.Identity);
            em.SetComponentData(owner, new PhysicsVelocity { Linear=new float3(0,0,10) });
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var samples = ref builder.ConstructRoot<EnemyAnimationSamples>();
            samples.BoneCount=3; samples.FrameCount=2;
            var durations=builder.Allocate(ref samples.Durations,11);
            var poses=builder.Allocate(ref samples.Matrices,66);
            for(int i=0;i<11;i++) durations[i]=1;
            var identity=new float3x4(new float3(1,0,0),new float3(0,1,0),new float3(0,0,1),float3.zero);
            for(int i=0;i<poses.Length;i++) poses[i]=identity;
            // Distinct upper-body Sweep pose.
            poses[2*6+2]=poses[2*6+5]=new float3x4(identity.c0,identity.c1,identity.c2,new float3(7,8,9));
            poses[5*6+2]=poses[5*6+5]=new float3x4(identity.c0,identity.c1,identity.c2,new float3(7,8,9));
            using var blob=builder.CreateBlobAssetReference<EnemyAnimationSamples>(Allocator.Persistent);
            using var jogBuilder=new BlobBuilder(Allocator.Temp);
            ref var jog=ref jogBuilder.ConstructRoot<EnemyJogSamples>();
            jog.BoneCount=3; jog.FrameCount=2; jog.HipBoneIndex=0; jog.HipBindInverse=float4x4.identity;
            jogBuilder.Allocate(ref jog.LegBones,1)[0]=1;
            var jogDurations=jogBuilder.Allocate(ref jog.Durations,8);
            var legs=jogBuilder.Allocate(ref jog.Matrices,16);
            for(int i=0;i<8;i++) jogDurations[i]=1;
            for(int i=0;i<16;i++) legs[i]=new float3x4(identity.c0,identity.c1,identity.c2,new float3(1,2,3));
            using var jogBlob=jogBuilder.CreateBlobAssetReference<EnemyJogSamples>(Allocator.Persistent);
            var visual=em.CreateEntity(typeof(EnemyAnimationPlayback));
            em.AddComponentData(visual,new EnemyAnimation { Owner=owner,Samples=blob,JogSamples=jogBlob });
            var skin=em.AddBuffer<SkinMatrix>(visual);
            for(int i=0;i<3;i++) skin.Add(new SkinMatrix { Value=identity });
            world.SetTime(new TimeData(1,.02f));
            world.GetOrCreateSystem<ChickenAnimationSystem>().Update(world.Unmanaged); em.CompleteAllTrackedJobs();
            skin=em.GetBuffer<SkinMatrix>(visual);
            Assert.AreEqual(new float3(7,8,9),skin[2].Value.c3);
            Assert.AreEqual(phase==ChickenPhase.Cast?float3.zero:new float3(1,2,3),skin[1].Value.c3);
            if(phase==ChickenPhase.Cast) Assert.AreEqual(.5f,em.GetComponentData<EnemyAnimationPlayback>(visual).Phase,.0001f);
            Assert.AreEqual(LocalTransform.Identity,em.GetComponentData<LocalTransform>(owner));
            Assert.AreEqual(new float3(0,0,10),em.GetComponentData<PhysicsVelocity>(owner).Linear);
        }

        [TestCase(-1f,6)] [TestCase(1f,7)]
        public void Chicken007_IdleUsesDirectionalTurnFinishesAndReturnsToIdle(float rate,int motion)
        {
            using var world=new World("Chicken idle turning");var em=world.EntityManager;
            var owner=em.CreateEntity(typeof(ChickenBoss),typeof(ChickenTuning),typeof(ChickenTurnState));
            em.SetComponentData(owner,new ChickenBoss { Phase=ChickenPhase.Pause });
            em.SetComponentData(owner,new ChickenTurnState { YawRate=rate });
            using var builder=new BlobBuilder(Allocator.Temp);
            ref var data=ref builder.ConstructRoot<EnemyAnimationSamples>();data.BoneCount=1;data.FrameCount=2;
            var durations=builder.Allocate(ref data.Durations,11);var poses=builder.Allocate(ref data.Matrices,22);
            var identity=new float3x4(new float3(1,0,0),new float3(0,1,0),new float3(0,0,1),float3.zero);
            for(int i=0;i<11;i++){durations[i]=1;for(int frame=0;frame<2;frame++)poses[i*2+frame]=new float3x4(identity.c0,identity.c1,identity.c2,new float3(i,0,0));}
            using var samples=builder.CreateBlobAssetReference<EnemyAnimationSamples>(Allocator.Persistent);
            var visual=em.CreateEntity(typeof(EnemyAnimationPlayback));
            em.AddComponentData(visual,new EnemyAnimation { Owner=owner,Samples=samples });
            em.AddBuffer<SkinMatrix>(visual).Add(new SkinMatrix { Value=identity });
            var system=world.GetOrCreateSystem<ChickenAnimationSystem>();
            Update(.1f); Assert.AreEqual(motion+1,em.GetComponentData<EnemyAnimationPlayback>(visual).Initialized);
            Assert.AreEqual(0,em.GetBuffer<SkinMatrix>(visual)[0].Value.c3.x,"Unmasked upper-body/equipment bones must use Idle.");
            em.SetComponentData(owner,new ChickenTurnState());
            Update(.1f); Assert.AreEqual(motion+1,em.GetComponentData<EnemyAnimationPlayback>(visual).Initialized);
            Update(1f); Assert.AreEqual(1,em.GetComponentData<EnemyAnimationPlayback>(visual).Phase);
            Update(.1f); Assert.AreEqual(1,em.GetComponentData<EnemyAnimationPlayback>(visual).Initialized);
            em.SetComponentData(owner,new ChickenTurnState { YawRate=-rate });
            Update(.1f); Assert.AreEqual((motion==6?7:6)+1,em.GetComponentData<EnemyAnimationPlayback>(visual).Initialized);
            Update(1f);Update(.1f);Assert.AreEqual(.1f,em.GetComponentData<EnemyAnimationPlayback>(visual).Phase,.0001f);
            void Update(float delta){world.SetTime(new TimeData(world.Time.ElapsedTime+delta,delta));system.Update(world.Unmanaged);em.CompleteAllTrackedJobs();}
        }

        [TestCase(-1f,6)] [TestCase(1f,7)]
        public void Chicken007_TurningLayersOnlyLegsUnderContinuousIdlePelvisAndEquipment(float rate,int motion)
        {
            using var world=new World("Chicken masked idle turns");var em=world.EntityManager;
            var owner=em.CreateEntity(typeof(ChickenBoss),typeof(ChickenTuning),typeof(ChickenTurnState));
            em.SetComponentData(owner,new ChickenBoss { Phase=ChickenPhase.Pause });
            em.SetComponentData(owner,new ChickenTurnState { YawRate=rate });
            using var builder=new BlobBuilder(Allocator.Temp);
            ref var data=ref builder.ConstructRoot<EnemyAnimationSamples>();data.BoneCount=3;data.FrameCount=2;
            var durations=builder.Allocate(ref data.Durations,11);var poses=builder.Allocate(ref data.Matrices,66);
            var identity=new float3x4(new float3(1,0,0),new float3(0,1,0),new float3(0,0,1),float3.zero);
            for(int i=0;i<11;i++){durations[i]=i==6||i==7?2:1;for(int j=0;j<6;j++)poses[i*6+j]=identity;}
            for(int frame=0;frame<2;frame++)
            {
                poses[frame*3]=Pose(new float3(0,2,0));
                poses[frame*3+2]=Pose(new float3(10,20,30)*(frame+1));
                foreach(int turn in new[] {6,7})
                {
                    poses[turn*6+frame*3]=Pose(new float3(100,200,300));
                    poses[turn*6+frame*3+1]=Pose(new float3(100,200,300)+new float3(turn==6?-1:1,-2,turn==6?-3:3));
                    poses[turn*6+frame*3+2]=Pose(new float3(999));
                }
            }
            using var samples=builder.CreateBlobAssetReference<EnemyAnimationSamples>(Allocator.Persistent);
            using var maskBuilder=new BlobBuilder(Allocator.Temp);
            ref var mask=ref maskBuilder.ConstructRoot<EnemyJogSamples>();mask.BoneCount=3;mask.FrameCount=2;
            mask.HipBoneIndex=0;mask.HipBindInverse=float4x4.identity;maskBuilder.Allocate(ref mask.LegBones,1)[0]=1;
            using var legMask=maskBuilder.CreateBlobAssetReference<EnemyJogSamples>(Allocator.Persistent);
            var body=em.CreateEntity(typeof(EnemyAnimationPlayback));
            em.AddComponentData(body,new EnemyAnimation { Owner=owner,Samples=samples,JogSamples=legMask });
            var skin=em.AddBuffer<SkinMatrix>(body);for(int i=0;i<3;i++)skin.Add(new SkinMatrix { Value=identity });
            using var gearBuilder=new BlobBuilder(Allocator.Temp);
            ref var gearData=ref gearBuilder.ConstructRoot<EnemyAnimationSamples>();gearData.BoneCount=1;gearData.FrameCount=2;
            var gearDurations=gearBuilder.Allocate(ref gearData.Durations,11);var gearPoses=gearBuilder.Allocate(ref gearData.Matrices,22);
            for(int i=0;i<11;i++){gearDurations[i]=durations[i];for(int frame=0;frame<2;frame++)gearPoses[i*2+frame]=i==0?Pose(new float3(10,20,30)*(frame+1)):Pose(new float3(999));}
            using var gearSamples=gearBuilder.CreateBlobAssetReference<EnemyAnimationSamples>(Allocator.Persistent);
            var gear=em.CreateEntity(typeof(EnemyAnimationPlayback));
            em.AddComponentData(gear,new EnemyAnimation { Owner=owner,Samples=gearSamples });
            em.AddBuffer<SkinMatrix>(gear).Add(new SkinMatrix { Value=identity });
            var system=world.GetOrCreateSystem<ChickenAnimationSystem>();
            Verify(.25,motion,new float3(15,30,45));
            em.SetComponentData(owner,new ChickenTurnState { YawRate=-rate });
            Verify(.35,motion==6?7:6,new float3(17,34,51));
            float3x4 Pose(float3 position)=>new float3x4(identity.c0,identity.c1,identity.c2,position);
            void Verify(double time,int turn,float3 upper)
            {
                world.SetTime(new TimeData(time,.1f));system.Update(world.Unmanaged);em.CompleteAllTrackedJobs();
                var actual=em.GetBuffer<SkinMatrix>(body);
                Assert.AreEqual(new float3(0,2,0),actual[0].Value.c3,"The pelvis must stay on Idle.");
                Assert.Less(math.distance(new float3(turn==6?-1:1,0,turn==6?-3:3),actual[1].Value.c3),.0001f);
                Assert.Less(math.distance(upper,actual[2].Value.c3),.0001f,"Upper body must advance Idle independently of turn restarts.");
                Assert.Less(math.distance(upper,em.GetBuffer<SkinMatrix>(gear)[0].Value.c3),.0001f,"Equipment must match the Idle upper-body clock.");
                Assert.AreEqual(turn+1,em.GetComponentData<EnemyAnimationPlayback>(body).Initialized);
            }
        }

        [TestCase(2f)] [TestCase(-2f)]
        public void Chicken007_AuthoredScaleSurvivesBlendingAndPhaseColorsStayNeutral(float xScale)
        {
            var a=float4x4.TRS(float3.zero,quaternion.identity,new float3(xScale,3,4));
            var b=float4x4.TRS(float3.zero,quaternion.RotateX(math.PI),new float3(xScale,3,4));
            var pose=ChickenSkinPose.Blend(new float3x4(a.c0.xyz,a.c1.xyz,a.c2.xyz,a.c3.xyz),
                new float3x4(b.c0.xyz,b.c1.xyz,b.c2.xyz,b.c3.xyz),.5f);
            Assert.AreEqual(2,math.length(pose.c0),.0001f); Assert.AreEqual(3,math.length(pose.c1),.0001f);
            Assert.AreEqual(4,math.length(pose.c2),.0001f);
            Assert.AreEqual(math.sign(xScale),math.sign(math.determinant(new float3x3(pose.c0,pose.c1,pose.c2))));
            using var world=new World("Neutral wizard color"); var em=world.EntityManager;
            var owner=em.CreateEntity(typeof(ChickenBoss));
            var visual=em.CreateEntity(typeof(ChickenVisualOwner),typeof(URPMaterialPropertyBaseColor));
            em.SetComponentData(visual,new ChickenVisualOwner { Value=owner });
            for(int phase=0;phase<=4;phase++)
            {
                em.SetComponentData(owner,new ChickenBoss { Phase=(ChickenPhase)phase,InvulnerableUntil=100 });
                world.GetOrCreateSystem<ChickenPresentationSystem>().Update(world.Unmanaged);
                Assert.AreEqual(new float4(1),em.GetComponentData<URPMaterialPropertyBaseColor>(visual).Value);
            }
        }
    }
}
