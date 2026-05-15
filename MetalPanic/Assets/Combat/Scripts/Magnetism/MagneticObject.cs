using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace MagnetPanic.Combat
{
    [RequireComponent(typeof(Collider))]
    public sealed class MagneticObject : MonoBehaviour, IAttractable, IPoolable
    {
        [Header("Identity")]
        [SerializeField] MagneticObjectType objectType = MagneticObjectType.LightScrap;
        [SerializeField] bool useTypeDefaults = true;

        [Header("Magnetism")]
        [SerializeField] float magneticMass = 1f;
        [SerializeField] float objectSpeedModifier = 1.4f;
        [SerializeField] float orbitSnapSharpness = 24f;

        [Header("Projectile")]
        [SerializeField] int damage = 2;
        [SerializeField] int aoeDamage = 4;
        [SerializeField] float hitRadius = 0.55f;
        [SerializeField] float knockbackDistance = 0.8f;
        [SerializeField] int maxPierceCount = 1;
        [SerializeField] float projectileLifetime = 3f;
        [SerializeField] bool explodesOnImpact;
        [SerializeField] float explosionRadius = 2.5f;
        [SerializeField] LayerMask hitLayers = ~0;

        [Header("Presentation (swap-friendly)")]
        [Tooltip("Empty transform that owns the visual model. Drag a 3D model under it — colliders, rigidbody and VFX live on the root and stay untouched.")]
        [SerializeField] Transform modelRoot;

        [Header("Optional Feedback")]
        [SerializeField] TrailRenderer trail = null;
        [SerializeField] ParticleSystem orbitParticle = null;
        [SerializeField] ParticleSystem impactParticle = null;

        [Header("Events")]
        public UnityEvent<MagneticObject> OnAttractStarted = new UnityEvent<MagneticObject>();
        public UnityEvent<MagneticObject> OnOrbited = new UnityEvent<MagneticObject>();
        public UnityEvent<MagneticObject> OnRepelled = new UnityEvent<MagneticObject>();
        public UnityEvent<MagneticObject> OnImpact = new UnityEvent<MagneticObject>();

        static readonly Collider[] HitBuffer = new Collider[24];

        readonly HashSet<ArkhamEnemy> hitEnemies = new HashSet<ArkhamEnemy>();

        Rigidbody body;
        Collider objectCollider;
        Transform magnetTransform;
        MagneticObjectState state = MagneticObjectState.InWorld;
        int pierceRemaining;
        int damageBonusActive;
        float projectileAge;
        bool originalColliderIsTrigger;
        bool cachedOriginalColliderState;

        public MagneticObjectType ObjectType => objectType;
        public float MagneticMass => magneticMass;
        public MagneticObjectState MagneticState => state;
        public Transform AttractionTransform => transform;
        public Transform ModelRoot => modelRoot;
        public bool CanEnterOrbit => isActiveAndEnabled && (state == MagneticObjectState.InWorld || state == MagneticObjectState.Attracting);

        void Awake()
        {
            EnsureReferences();

            if (useTypeDefaults)
                ApplyTypeDefaults(objectType);

            ResetAsWorldScrap();
        }

        void OnEnable()
        {
            if (state == MagneticObjectState.Projectile)
                state = MagneticObjectState.InWorld;

            projectileAge = 0f;
            hitEnemies.Clear();
        }

        public void OnSpawn()
        {
            EnsureReferences();
            magnetTransform = null;
            state = MagneticObjectState.InWorld;
            projectileAge = 0f;
            pierceRemaining = Mathf.Max(1, maxPierceCount);
            damageBonusActive = 0;
            hitEnemies.Clear();
            objectCollider.enabled = true;
            RestoreColliderMode();
            ResetAsWorldScrap();

            if (trail != null)
            {
                trail.Clear();
                trail.emitting = false;
            }

            StopParticle(orbitParticle);
            StopParticle(impactParticle);
        }

        public void OnDespawn()
        {
            EnsureReferences();
            magnetTransform = null;
            state = MagneticObjectState.InWorld;
            projectileAge = 0f;
            hitEnemies.Clear();
            StopParticle(orbitParticle);
            StopParticle(impactParticle);

            if (trail != null)
            {
                trail.Clear();
                trail.emitting = false;
            }

            if (body != null && !body.isKinematic)
                body.linearVelocity = Vector3.zero;

            SetKinematic(true);

            if (objectCollider != null)
            {
                objectCollider.enabled = false;
                RestoreColliderMode();
            }
        }

        void Update()
        {
            if (state != MagneticObjectState.Projectile)
                return;

            projectileAge += Time.deltaTime;
            CheckProjectileHits();

            if (projectileAge >= projectileLifetime)
                Consume();
        }

        public void ConfigurePrototype(MagneticObjectType type, LayerMask layers)
        {
            objectType = type;
            hitLayers = layers;
            ApplyTypeDefaults(type);
            EnsureReferences();
        }

        public void BeginAttract(Transform magnet)
        {
            if (!CanEnterOrbit)
                return;

            EnsureReferences();
            magnetTransform = magnet;
            state = MagneticObjectState.Attracting;
            SetKinematic(true);
            objectCollider.enabled = true;
            objectCollider.isTrigger = true;
            if (trail != null)
            {
                trail.Clear();
                trail.emitting = true;
            }
            OnAttractStarted.Invoke(this);
        }

        public void TickAttract(Vector3 magnetPosition, float pullSpeed, float deltaTime)
        {
            if (state != MagneticObjectState.Attracting)
                return;

            float speed = pullSpeed / Mathf.Max(0.35f, magneticMass);
            transform.position = Vector3.MoveTowards(transform.position, magnetPosition, speed * deltaTime);
        }

        public bool IsCloseEnoughForOrbit(Vector3 magnetPosition, float orbitRadius)
        {
            Vector3 delta = transform.position - magnetPosition;
            delta.y = 0f;
            return delta.sqrMagnitude <= (orbitRadius + 0.2f) * (orbitRadius + 0.2f);
        }

        public void EnterOrbit(Transform orbitCenter)
        {
            EnsureReferences();
            magnetTransform = orbitCenter;
            state = MagneticObjectState.InOrbit;
            projectileAge = 0f;
            hitEnemies.Clear();
            SetKinematic(true);
            objectCollider.enabled = true;
            objectCollider.isTrigger = true;
            if (trail != null)
            {
                trail.Clear();
                trail.emitting = true;
            }
            PlayParticle(orbitParticle);
            OnOrbited.Invoke(this);
        }

        public void TickOrbit(Vector3 orbitPosition, float deltaTime)
        {
            if (state != MagneticObjectState.InOrbit)
                return;

            float t = 1f - Mathf.Exp(-orbitSnapSharpness * deltaTime);
            transform.position = Vector3.Lerp(transform.position, orbitPosition, t);

            if (magnetTransform != null)
            {
                Vector3 look = transform.position - magnetTransform.position;
                look.y = 0f;
                if (look.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.LookRotation(look);
            }
        }

        public void StopAttracting()
        {
            if (state != MagneticObjectState.Attracting)
                return;

            state = MagneticObjectState.InWorld;
            ResetAsWorldScrap();
            if (trail != null)
                trail.emitting = false;
            RestoreColliderMode();
        }

        public void RejectFromOrbit(Vector3 magnetPosition, float rejectSpeed)
        {
            Vector3 direction = transform.position - magnetPosition;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f)
                direction = Random.insideUnitSphere;

            ForcedEject(direction.normalized, rejectSpeed);
        }

        public void Repel(Vector3 direction, float speed)
        {
            Repel(direction, speed, 0, 0);
        }

        public void Repel(Vector3 direction, float speed, int bonusDamage, int bonusPierce)
        {
            EnsureReferences();
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f)
                direction = transform.forward;

            state = MagneticObjectState.Projectile;
            projectileAge = 0f;
            pierceRemaining = Mathf.Max(1, maxPierceCount + Mathf.Max(0, bonusPierce));
            damageBonusActive = Mathf.Max(0, bonusDamage);
            hitEnemies.Clear();
            SetKinematic(false);
            objectCollider.enabled = true;
            objectCollider.isTrigger = true;
            if (trail != null)
                trail.emitting = true;
            StopParticle(orbitParticle);
            body.linearVelocity = direction.normalized * speed * objectSpeedModifier;
            body.WakeUp();
            OnRepelled.Invoke(this);
        }

        public void ForcedEject(Vector3 direction, float speed)
        {
            EnsureReferences();
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f)
                direction = transform.forward;

            state = MagneticObjectState.Projectile;
            projectileAge = 0f;
            pierceRemaining = Mathf.Max(1, maxPierceCount);
            hitEnemies.Clear();
            SetKinematic(false);
            body.useGravity = true;
            objectCollider.enabled = true;
            objectCollider.isTrigger = true;
            if (trail != null)
            {
                trail.Clear();
                trail.emitting = true;
            }
            StopParticle(orbitParticle);
            body.linearVelocity = direction.normalized * speed;
            body.WakeUp();
        }

        void CheckProjectileHits()
        {
            int count = Physics.OverlapSphereNonAlloc(
                transform.position,
                hitRadius,
                HitBuffer,
                hitLayers,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                ArkhamEnemy enemy = HitBuffer[i].GetComponentInParent<ArkhamEnemy>();
                if (enemy == null || !enemy.IsAlive || hitEnemies.Contains(enemy))
                    continue;

                if (explodesOnImpact)
                {
                    Explode(enemy);
                    return;
                }

                hitEnemies.Add(enemy);
                enemy.ReceiveMagneticImpact(damage + damageBonusActive, transform.position, knockbackDistance, true);
                OnImpact.Invoke(this);

                pierceRemaining--;
                if (pierceRemaining <= 0)
                {
                    Consume();
                    return;
                }
            }
        }

        void Explode(ArkhamEnemy directTarget)
        {
            if (directTarget != null && directTarget.IsAlive)
                directTarget.ReceiveMagneticImpact(damage, transform.position, knockbackDistance, true);

            int count = Physics.OverlapSphereNonAlloc(
                transform.position,
                explosionRadius,
                HitBuffer,
                hitLayers,
                QueryTriggerInteraction.Ignore);

            hitEnemies.Clear();

            for (int i = 0; i < count; i++)
            {
                ArkhamEnemy enemy = HitBuffer[i].GetComponentInParent<ArkhamEnemy>();
                if (enemy == null || !enemy.IsAlive || hitEnemies.Contains(enemy))
                    continue;

                hitEnemies.Add(enemy);
                enemy.ReceiveMagneticImpact(aoeDamage, transform.position, knockbackDistance * 1.4f, true);
            }

            OnImpact.Invoke(this);
            Consume();
        }

        void Consume()
        {
            if (state != MagneticObjectState.Projectile)
                return;

            PlayParticle(impactParticle);
            if (trail != null)
                trail.emitting = false;
            SetKinematic(true);
            state = MagneticObjectState.InWorld;
            objectCollider.enabled = false;
            Pool.Despawn(gameObject);
        }

        void ApplyTypeDefaults(MagneticObjectType type)
        {
            switch (type)
            {
                case MagneticObjectType.LightScrap:
                    magneticMass = 1f;
                    objectSpeedModifier = 1.4f;
                    damage = 2;
                    aoeDamage = 0;
                    knockbackDistance = 0.8f;
                    maxPierceCount = 2;
                    explodesOnImpact = false;
                    break;
                case MagneticObjectType.Plate:
                    magneticMass = 2f;
                    objectSpeedModifier = 1f;
                    damage = 3;
                    aoeDamage = 0;
                    knockbackDistance = 1.15f;
                    maxPierceCount = 3;
                    explodesOnImpact = false;
                    break;
                case MagneticObjectType.Mine:
                    magneticMass = 1.5f;
                    objectSpeedModifier = 1.2f;
                    damage = 3;
                    aoeDamage = 4;
                    knockbackDistance = 1.35f;
                    maxPierceCount = 1;
                    explodesOnImpact = true;
                    explosionRadius = Mathf.Max(2.5f, explosionRadius);
                    break;
                case MagneticObjectType.Heavy:
                    magneticMass = 3f;
                    objectSpeedModifier = 0.6f;
                    damage = 7;
                    aoeDamage = 0;
                    knockbackDistance = 1.8f;
                    maxPierceCount = 1;
                    explodesOnImpact = false;
                    break;
            }
        }

        void EnsureReferences()
        {
            if (body == null)
            {
                body = GetComponent<Rigidbody>();
                if (body == null)
                    body = gameObject.AddComponent<Rigidbody>();
            }

            if (objectCollider == null)
            {
                objectCollider = GetComponent<Collider>();
                if (objectCollider != null && !cachedOriginalColliderState)
                {
                    originalColliderIsTrigger = objectCollider.isTrigger;
                    cachedOriginalColliderState = true;
                }
            }

            body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.interpolation = RigidbodyInterpolation.Interpolate;
        }

        void SetKinematic(bool value)
        {
            if (body == null)
                return;

            if (value)
            {
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }

                body.isKinematic = true;
                return;
            }

            body.isKinematic = false;
        }

        void RestoreColliderMode()
        {
            if (objectCollider != null && cachedOriginalColliderState)
                objectCollider.isTrigger = originalColliderIsTrigger;
        }

        void ResetAsWorldScrap()
        {
            EnsureReferences();
            body.useGravity = false;
            if (body.isKinematic)
                body.isKinematic = false;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            if (objectCollider != null)
                objectCollider.enabled = true;
            RestoreColliderMode();
        }

        static void PlayParticle(ParticleSystem particle)
        {
            if (particle != null)
                particle.Play(true);
        }

        static void StopParticle(ParticleSystem particle)
        {
            if (particle != null)
            {
                particle.Clear(true);
                particle.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        void OnCollisionEnter(Collision collision)
        {
            if (state != MagneticObjectState.Projectile)
                return;

            if (collision.collider.GetComponentInParent<ArkhamEnemy>() == null)
                Consume();
        }

        void OnTriggerEnter(Collider other)
        {
            if (state != MagneticObjectState.Projectile)
                return;

            // Arena uses trigger colliders on the Ground/ArenaWall layers to define the
            // playable area for spawning and containment. They are not real obstacles, so
            // the projectile must fly through them instead of being consumed on contact.
            if (ShouldIgnoreProjectileTrigger(other))
                return;

            if (other.GetComponentInParent<ArkhamEnemy>() == null)
            {
                Consume();
                return;
            }

            CheckProjectileHits();
        }

        static bool ShouldIgnoreProjectileTrigger(Collider other)
        {
            if (other == null)
                return true;

            if (!other.isTrigger)
                return false;

            int layer = other.gameObject.layer;
            int groundLayer = LayerMask.NameToLayer("Ground");
            int wallLayer = LayerMask.NameToLayer("ArenaWall");
            if ((groundLayer >= 0 && layer == groundLayer) || (wallLayer >= 0 && layer == wallLayer))
                return true;

            // Spawn markers, doors and other gameplay volumes live on the arena hierarchy
            // as triggers — they should never destroy a repelled projectile either.
            return other.GetComponentInParent<ArenaSystem>() != null;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = explodesOnImpact ? new Color(1f, 0.35f, 0.1f, 0.25f) : new Color(0.25f, 0.8f, 1f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, explodesOnImpact ? explosionRadius : hitRadius);
        }
    }
}
