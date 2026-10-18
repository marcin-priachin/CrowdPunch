using System;
using System.Linq;
using UnityEditor;
namespace CrowdPunch.Editor
{
    // Retain the established Chapter One menu/API; output ownership is explicit in the shared baker.
    public static class ChapterOneShadowBake
    {
        public static bool Running => CampaignShadowBake.Running;
        public static int Completed => CampaignShadowBake.Completed;
        public static string Error => CampaignShadowBake.Error;
        [MenuItem("Crowd Punch/Art Direction/Bake Chapter One Shadows")]
        public static void BakeAll() => Begin(Enumerable.Range(1,10).ToArray());
        public static void Begin(params int[] numbers)
        {
            if(numbers.Length==0 || numbers.Any(n=>n<1 || n>10)) throw new ArgumentOutOfRangeException(nameof(numbers));
            CampaignShadowBake.Begin(numbers.Select(n=>$"Assets/CrowdPunch/Scenes/Campaign/Campaign_{n:00}.unity").ToArray(),ChapterOneVillageAssets.Output+"/BakedShadows",true);
        }
    }
}
