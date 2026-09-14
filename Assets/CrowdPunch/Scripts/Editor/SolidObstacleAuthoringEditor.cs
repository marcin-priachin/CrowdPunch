using CrowdPunch.Authoring;
using UnityEditor;
using UnityEngine;
namespace CrowdPunch.Editor
{
    [CustomEditor(typeof(SolidObstacleAuthoring)), CanEditMultipleObjects]
    public sealed class SolidObstacleAuthoringEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("Place under the Navigation Arena root. Footprints are world-axis aligned. Keep the visible child mesh silhouette within the orange collision preview; no extra collider is needed.",MessageType.Info);
            if(GUILayout.Button("Snap footprint and resize greybox silhouette"))foreach(var item in targets)
            {
                var obstacle=(SolidObstacleAuthoring)item;Undo.RecordObject(obstacle.transform,"Snap solid obstacle");obstacle.Snap();
                var child=obstacle.transform.Find("Blocking silhouette");if(child!=null){Undo.RecordObject(child,"Resize obstacle silhouette");child.localPosition=Vector3.up*obstacle.height*.5f;child.localScale=new Vector3(obstacle.Size.x,obstacle.height,obstacle.Size.y);}
                EditorUtility.SetDirty(obstacle);
            }
        }
    }
}
