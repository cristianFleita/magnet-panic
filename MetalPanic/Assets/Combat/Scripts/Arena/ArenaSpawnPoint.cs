using UnityEngine;

namespace MagnetPanic.Combat
{
    public enum ArenaSpawnCategory
    {
        Enemy,
        Scrap,
        Pickup
    }

    [DisallowMultipleComponent]
    public sealed class ArenaSpawnPoint : MonoBehaviour
    {
        [SerializeField] ArenaSpawnCategory category = ArenaSpawnCategory.Enemy;
        [SerializeField, Min(0.1f)] float occupancyRadius = 1f;

        public ArenaSpawnCategory Category => category;
        public float OccupancyRadius => occupancyRadius;
        public Vector3 Position => transform.position;

        public void Configure(ArenaSpawnCategory nextCategory, float radius = 1f)
        {
            category = nextCategory;
            occupancyRadius = Mathf.Max(0.1f, radius);
        }
    }
}
