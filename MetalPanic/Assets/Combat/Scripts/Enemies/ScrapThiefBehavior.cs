using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace MagnetPanic.Combat
{
    /// <summary>
    /// Scrap Thief behavior: an enemy that can grab nearby MagneticObjects (scraps)
    /// from the arena floor and throw them at the player (Resident Evil 4 style).
    ///
    /// This creates a dynamic where:
    ///   - Enemies deny the player ammo by stealing scraps
    ///   - Stolen scraps become threats (thrown at player)
    ///   - But the player can attract the thrown scrap mid-air → counter-play loop
    ///   - Strategic tension: kill the thief before it throws, or attract the scrap back
    ///
    /// Works as a companion component to ArkhamEnemy. Any enemy archetype can
    /// have this behavior — it's not exclusive to a single type.
    /// </summary>
    public sealed class ScrapThiefBehavior : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] ArkhamEnemy enemy;
        [SerializeField] ArkhamCombatController playerCombat;
        [SerializeField] Transform grabPoint;

        [Header("Scrap Grabbing")]
        [SerializeField] float grabRadius = 3.5f;
        [SerializeField] float grabCooldown = 5f;
        [SerializeField] float grabDuration = 0.4f;
        [SerializeField] LayerMask scrapLayers = ~0;

        [Header("Throwing")]
        [SerializeField] float throwSpeed = 12f;
        [SerializeField] float throwPrepTime = 0.35f;
        [SerializeField] float throwRecovery = 0.55f;
        [SerializeField] float throwInaccuracy = 8f;
        [SerializeField, Tooltip("Chance (0-1) the enemy grabs+throws when selected for attack and scrap is available.")]
        float throwChance = 0.45f;

        [Header("Events")]
        public UnityEvent<ScrapThiefBehavior> OnScrapGrabbed = new UnityEvent<ScrapThiefBehavior>();
        public UnityEvent<ScrapThiefBehavior> OnScrapThrown = new UnityEvent<ScrapThiefBehavior>();

        static readonly Collider[] GrabBuffer = new Collider[16];

        MagneticObject heldScrap;
        float nextGrabTime;
        Coroutine throwRoutine;
        bool isThrowing;

        public bool IsThrowing => isThrowing;
        public bool IsHoldingScrap => heldScrap != null;

        void Awake()
        {
            if (enemy == null)
                enemy = GetComponent<ArkhamEnemy>();
            if (playerCombat == null)
                playerCombat = FindFirstObjectByType<ArkhamCombatController>();
            if (grabPoint == null)
            {
                Transform gp = transform.Find("GrabPoint");
                grabPoint = gp != null ? gp : transform;
            }
        }

        void OnEnable()
        {
            if (enemy == null)
                enemy = GetComponent<ArkhamEnemy>();
            if (playerCombat == null)
                playerCombat = FindFirstObjectByType<ArkhamCombatController>();
        }

        void OnDisable()
        {
            DropHeldScrap();
        }

        void Update()
        {
            if (heldScrap != null && grabPoint != null)
            {
                // Stick the held scrap to the grab point
                heldScrap.transform.position = grabPoint.position;
                heldScrap.transform.rotation = grabPoint.rotation;
            }
        }

        /// <summary>
        /// Should be called when the Attack Director selects this enemy.
        /// Returns true if the thief will do a scrap throw instead of melee.
        /// </summary>
        public bool TryScrapAttack()
        {
            if (isThrowing || !enemy.IsAlive || Time.time < nextGrabTime)
                return false;

            // Roll chance
            if (Random.value > throwChance)
                return false;

            // Already holding? Just throw
            if (heldScrap != null)
            {
                StartThrow();
                return true;
            }

            // Try to grab nearby scrap
            MagneticObject target = FindNearestScrap();
            if (target == null)
                return false;

            if (throwRoutine != null)
                StopCoroutine(throwRoutine);
            throwRoutine = StartCoroutine(GrabAndThrowRoutine(target));
            return true;
        }

        void StartThrow()
        {
            if (heldScrap == null)
                return;

            if (throwRoutine != null)
                StopCoroutine(throwRoutine);
            throwRoutine = StartCoroutine(ThrowRoutine());
        }

        IEnumerator GrabAndThrowRoutine(MagneticObject target)
        {
            isThrowing = true;

            // Grab animation time
            yield return GrabScrap(target);

            if (!enemy.IsAlive || heldScrap == null)
            {
                isThrowing = false;
                throwRoutine = null;
                yield break;
            }

            // Throw
            yield return DoThrow();

            isThrowing = false;
            throwRoutine = null;
        }

        IEnumerator ThrowRoutine()
        {
            isThrowing = true;
            yield return DoThrow();
            isThrowing = false;
            throwRoutine = null;
        }

        IEnumerator GrabScrap(MagneticObject target)
        {
            if (target == null || !target.CanEnterOrbit)
                yield break;

            // Snap scrap to grab point
            heldScrap = target;

            // Disable the scrap's normal physics during hold
            Rigidbody rb = heldScrap.GetComponent<Rigidbody>();
            if (rb != null)
                rb.isKinematic = true;

            Collider col = heldScrap.GetComponent<Collider>();
            if (col != null)
                col.enabled = false;

            nextGrabTime = Time.time + grabCooldown;
            OnScrapGrabbed.Invoke(this);

            yield return new WaitForSeconds(grabDuration);
        }

        IEnumerator DoThrow()
        {
            // Prepare throw — show telegraph
            yield return new WaitForSeconds(throwPrepTime);

            if (!enemy.IsAlive || heldScrap == null || playerCombat == null)
            {
                DropHeldScrap();
                yield break;
            }

            // Calculate throw direction toward player
            Vector3 origin = grabPoint != null ? grabPoint.position : transform.position;
            Vector3 target = playerCombat.transform.position + Vector3.up * 0.5f;
            Vector3 direction = (target - origin).normalized;

            // Inaccuracy
            if (throwInaccuracy > 0f)
            {
                float yaw = Random.Range(-throwInaccuracy, throwInaccuracy);
                direction = Quaternion.AngleAxis(yaw, Vector3.up) * direction;
            }

            direction.y = 0f;
            direction = direction.normalized;

            MagneticObject scrap = heldScrap;
            heldScrap = null;

            // Re-enable physics
            Collider col = scrap.GetComponent<Collider>();
            if (col != null)
                col.enabled = true;

            // Use ForcedEject to send it flying as a projectile
            // This turns it into a Projectile-state MagneticObject that can hit enemies
            scrap.ForcedEject(direction, throwSpeed);

            OnScrapThrown.Invoke(this);

            yield return new WaitForSeconds(throwRecovery);
        }

        void DropHeldScrap()
        {
            if (heldScrap == null)
                return;

            Rigidbody rb = heldScrap.GetComponent<Rigidbody>();
            if (rb != null)
                rb.isKinematic = false;

            Collider col = heldScrap.GetComponent<Collider>();
            if (col != null)
                col.enabled = true;

            heldScrap = null;
        }

        MagneticObject FindNearestScrap()
        {
            int count = Physics.OverlapSphereNonAlloc(
                transform.position,
                grabRadius,
                GrabBuffer,
                scrapLayers,
                QueryTriggerInteraction.Collide);

            MagneticObject best = null;
            float bestDist = float.PositiveInfinity;

            for (int i = 0; i < count; i++)
            {
                MagneticObject obj = GrabBuffer[i].GetComponentInParent<MagneticObject>();
                if (obj == null || obj.MagneticState != MagneticObjectState.InWorld)
                    continue;

                float dist = Vector3.SqrMagnitude(obj.transform.position - transform.position);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = obj;
                }
            }

            return best;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.9f, 0.5f, 0.1f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, grabRadius);
        }
    }
}
