using UnityEngine;
namespace CrowdPunch.Authoring
{
    public sealed class SolidObstacleAuthoring : MonoBehaviour
    {
        [Min(.25f), Tooltip("Placement unit in metres, independent from navigation cell size.")]
        public float placementUnit = 1f;
        [Tooltip("Rectangle dimensions in placement units. Axis-aligned world XZ footprint.")]
        public Vector2Int footprint = new Vector2Int(2, 2);
        [Min(.25f), Tooltip("Solid height above this transform's Y; visual mesh should match this silhouette.")]
        public float height = 2f;
        public Vector2 Size => new Vector2(footprint.x, footprint.y) * placementUnit;
        [ContextMenu("Snap footprint to placement grid")]
        public void Snap()
        {
            Vector2 size = Size; Vector3 p = transform.position;
            var arena = GetComponentInParent<ArenaAuthoring>();
            Vector2 origin = arena == null ? Vector2.zero : new Vector2(arena.transform.position.x + arena.SpacingCenterOffset.x - arena.SpacingSize.x * .5f,
                arena.transform.position.z + arena.SpacingCenterOffset.z - arena.SpacingSize.z * .5f);
            p.x = origin.x + Mathf.Round((p.x-origin.x-size.x*.5f)/placementUnit)*placementUnit+size.x*.5f;
            p.z = origin.y + Mathf.Round((p.z-origin.y-size.y*.5f)/placementUnit)*placementUnit+size.y*.5f;
            transform.SetPositionAndRotation(p, Quaternion.identity); transform.localScale = Vector3.one;
        }
        private void OnValidate() { placementUnit = Mathf.Max(.25f,placementUnit); footprint = Vector2Int.Max(Vector2Int.one,footprint); height = Mathf.Max(.25f,height); }
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1,.55f,.15f,.65f);
            Gizmos.DrawWireCube(transform.position + Vector3.up*height*.5f,new Vector3(Size.x,height,Size.y));
        }
    }
}
