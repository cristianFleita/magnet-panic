using UnityEngine;
using UnityEngine.Events;

namespace MagnetPanic.Combat
{
    /// <summary>
    /// A projectile fired by an enemy (e.g. Spitter Drone).
    /// It is a MagneticObject that can be attracted and repelled by the player,
    /// but it also flies toward the player on its own if not intercepted.
    /// The visual model lives under ModelRoot — swap the placeholder for a 3D mesh.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class EnemyProjectile : MonoBehaviour, IPoolable
    {
        [Header("Identity")]
        [SerializeField] Transform modelRoot;

        [Header("Flight")]
        [SerializeField] float speed = 10f;
        [SerializeField] float lifetime = 4f;
        [SerializeField] float hitRadius = 0.45f;
        [SerializeField] int damage = 1;
        [SerializeField] bool knocksDown;
        [SerializeField] LayerMask playerLayer = ~0;
        [SerializeField] LayerMask wallLayer;

        [Header("Magnetism")]
        [SerializeField, Tooltip("When true the projectile can be attracted into orbit and repelled back.")]
        bool attractable = true;
        [SerializeField] float magneticMass = 0.8f;

        [Header("Presentation")]
        [SerializeField] TrailRenderer trail;

        [Header("Events")]
        public UnityEvent<EnemyProjectile> OnHitPlayer = new UnityEvent<EnemyProjectile>();
        public UnityEvent<EnemyProjectile> OnHitWall = new UnityEvent<EnemyProjectile>();
        public UnityEvent<EnemyProjectile> OnAbsorbed = new UnityEvent<EnemyProjectile>();

        static readonly Collider[] HitBuffer = new Collider[8];

        Vector3 direction;
        float age;
        bool consumed;
        ArkhamCombatController playerCombat;
        MagneticObject magneticComponent;

        public Transform ModelRoot => modelRoot;
        public bool IsAttractable => attractable;
        public float MagneticMass => magneticMass;

        void Awake()
        {
            magneticComponent = GetComponent<MagneticObject>();
        }

        /// <summary>
        /// Fire the projectile toward the player from a source position.
        /// </summary>
        public void Fire(Vector3 fireDirection, ArkhamCombatController target, float overrideSpeed = -1f)
        {
            direction = fireDirection;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f)
                direction = Vector3.forward;
            direction = direction.normalized;

            playerCombat = target;
            age = 0f;
            consumed = false;

            if (overrideSpeed > 0f)
                speed = overrideSpeed;

            if (trail != null)
            {
                trail.Clear();
                trail.emitting = true;
            }

            // Face direction of travel
            transform.rotation = Quaternion.LookRotation(direction);
        }

        void Update()
        {
            if (consumed)
                return;

            // If a MagneticObject component exists and is being attracted/orbited/repelled,
            // let it handle movement — we only drive the projectile when InWorld
            if (magneticComponent != null && magneticComponent.MagneticState != MagneticObjectState.InWorld)
                return;

            age += Time.deltaTime;
            if (age >= lifetime)
            {
                Consume();
                return;
            }

            // Move forward
            transform.position += direction * speed * Time.deltaTime;

            // Check player hit
            if (playerCombat != null && playerCombat.IsAlive)
            {
                Vector3 delta = playerCombat.transform.position - transform.position;
                delta.y = 0f;
                if (delta.sqrMagnitude <= hitRadius * hitRadius)
                {
                    playerCombat.ReceiveDamage(null, damage, knocksDown);
                    OnHitPlayer.Invoke(this);
                    Consume();
                    return;
                }
            }

            // Check arena wall collision
            int wallMask = wallLayer.value != 0 ? wallLayer.value : LayerMask.GetMask("ArenaWall");
            if (wallMask != 0)
            {
                int hits = Physics.OverlapSphereNonAlloc(
                    transform.position, hitRadius * 0.7f,
                    HitBuffer, wallMask,
                    QueryTriggerInteraction.Ignore);

                if (hits > 0)
                {
                    OnHitWall.Invoke(this);
                    Consume();
                    return;
                }
            }
        }

        void Consume()
        {
            if (consumed)
                return;

            consumed = true;
            if (trail != null)
                trail.emitting = false;

            Pool.Despawn(gameObject);
        }

        public void OnSpawn()
        {
            consumed = false;
            age = 0f;
            direction = Vector3.forward;
            var col = GetComponent<Collider>();
            if (col != null)
                col.enabled = true;
            if (trail != null)
            {
                trail.Clear();
                trail.emitting = false;
            }
        }

        public void OnDespawn()
        {
            consumed = true;
            if (trail != null)
            {
                trail.Clear();
                trail.emitting = false;
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.15f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, hitRadius);
        }
    }
}
