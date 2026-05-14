using UnityEngine;

namespace MagnetPanic.Combat
{
    public enum ArenaDoorId
    {
        North,
        South,
        East,
        West
    }

    [DisallowMultipleComponent]
    public sealed class ArenaDoor : MonoBehaviour
    {
        [SerializeField] ArenaDoorId doorId = ArenaDoorId.North;
        [SerializeField] Transform exit;
        [SerializeField] Transform queue;
        [SerializeField] Light warningLight;
        [SerializeField, Min(0f)] float spawnRadius = 1.5f;
        [SerializeField, Min(0.1f)] float occupancyRadius = 1.5f;
        [SerializeField] bool isEnabled = true;
        [SerializeField] Vector3 localFacingDirection = Vector3.back;

        public ArenaDoorId DoorId => doorId;
        public Transform Exit => exit != null ? exit : transform;
        public Transform Queue => queue;
        public Light WarningLight => warningLight;
        public float SpawnRadius => spawnRadius;
        public float OccupancyRadius => occupancyRadius;

        public bool IsEnabled
        {
            get => isEnabled;
            set => isEnabled = value;
        }

        public Vector3 ExitPosition => Exit.position;

        public Vector3 FacingDirection
        {
            get
            {
                Vector3 world = transform.TransformDirection(localFacingDirection);
                world.y = 0f;
                return world.sqrMagnitude > 0.0001f ? world.normalized : Vector3.forward;
            }
        }

        public void Configure(ArenaDoorId id, Vector3 facingLocal, float spawn = 1.5f, float occupancy = 1.5f)
        {
            doorId = id;
            localFacingDirection = facingLocal;
            spawnRadius = Mathf.Max(0f, spawn);
            occupancyRadius = Mathf.Max(0.1f, occupancy);
        }

        void OnDrawGizmos()
        {
            Vector3 origin = ExitPosition;
            Gizmos.color = isEnabled ? new Color(1f, 0.4f, 0.1f, 0.85f) : new Color(0.4f, 0.4f, 0.4f, 0.6f);
            Gizmos.DrawWireSphere(origin, Mathf.Max(0.25f, spawnRadius));
            Gizmos.DrawLine(origin, origin + FacingDirection * 2.5f);
        }
    }
}
