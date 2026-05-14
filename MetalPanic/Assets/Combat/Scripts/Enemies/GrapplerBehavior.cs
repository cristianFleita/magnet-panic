using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace MagnetPanic.Combat
{
    /// <summary>
    /// Grappler behavior: the Metal Enemy lunges at the player and grabs them,
    /// locking the player in place while signaling other enemies to attack.
    ///
    /// The player must mash Q (Struggle intent) to build up an escape meter.
    /// Once enough presses are registered, the player breaks free, shoving the
    /// grappler back and stunning it briefly.
    ///
    /// Design goals:
    ///   - Creates urgency: other enemies can freely attack while grappled
    ///   - Gives Metal Enemy a unique threat profile beyond "always pullable"
    ///   - Rewards spatial awareness: avoid being cornered near Metal Enemies
    ///   - Counter-play: counter cue shown during lunge → can be countered
    ///   - Mash mechanic adds physicality and panic matching the "scrapstorm" theme
    ///
    /// Coordination flow:
    ///   1. GrapplerBehavior grabs player via ArkhamCombatController.BeginGrapple()
    ///   2. ArkhamEnemyManager detects grapple → fast-tracks another enemy to attack
    ///   3. Player mashes Q → escape meter fills
    ///   4. On escape: grappler gets stunned, player is immune briefly
    ///
    /// This component is added to the Metal Enemy alongside ArkhamEnemy.
    /// </summary>
    public sealed class GrapplerBehavior : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] ArkhamEnemy enemy;
        [SerializeField] ArkhamCombatController playerCombat;

        [Header("Throw Tuning")]
        [SerializeField, Tooltip("Distance the grappler must be within to initiate the grab.")]
        float grappleRange = 1.6f;
        [SerializeField, Tooltip("Damage dealt to the player on a successful grab.")]
        int throwDamage = 2;
        [SerializeField, Tooltip("Cooldown before this enemy can attempt another grab.")]
        float grappleCooldown = 6f;
        [SerializeField, Tooltip("Chance (0-1) of attempting grab when selected by director.")]
        [Range(0f, 1f)] float grappleChance = 0.8f;

        [Header("Approach")]
        [SerializeField, Tooltip("Speed when lunging to grab the player.")]
        float lungeSpeed = 10f;
        [SerializeField, Tooltip("Duration of the lunge toward the player.")]
        float lungeDuration = 0.35f;

        [Header("Events")]
        public UnityEvent<GrapplerBehavior> OnGrappleStart = new UnityEvent<GrapplerBehavior>();
        public UnityEvent<GrapplerBehavior> OnThrowComplete = new UnityEvent<GrapplerBehavior>();

        float nextGrappleTime;
        Coroutine grappleRoutine;
        bool isGrappling;

        public bool IsGrappling => isGrappling;

        void Awake()
        {
            if (enemy == null)
                enemy = GetComponent<ArkhamEnemy>();
            if (playerCombat == null)
                playerCombat = FindFirstObjectByType<ArkhamCombatController>();
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
            if (isGrappling)
                ForceReleaseGrapple();
        }

        public bool TryGrapple()
        {
            if (isGrappling || !enemy.IsAlive || Time.time < nextGrappleTime)
                return false;

            if (playerCombat == null || !playerCombat.IsAlive)
                return false;

            if (Random.value > grappleChance)
                return false;

            if (grappleRoutine != null)
                StopCoroutine(grappleRoutine);

            grappleRoutine = StartCoroutine(GrappleRoutine());
            return true;
        }

        IEnumerator GrappleRoutine()
        {
            isGrappling = true;
            
            // Prepare attack: show counter cue so the player can defend
            enemy.isPreparingAttack = true;
            enemy.ShowCounterCue();

            float prepTimer = 0f;
            // Assuming default prepare time, or we just hardcode ~0.4s
            float prepTime = 0.45f; 

            while (prepTimer < prepTime && enemy.IsAlive)
            {
                // Check if we were countered during preparation
                if (enemy.IsStunned || !isGrappling)
                    yield break;

                prepTimer += Time.deltaTime;
                
                // Keep facing the player
                if (playerCombat != null)
                {
                    Vector3 toPlayer = playerCombat.transform.position - transform.position;
                    toPlayer.y = 0f;
                    if (toPlayer.sqrMagnitude > 0.01f)
                        transform.rotation = Quaternion.LookRotation(toPlayer.normalized);
                }
                
                yield return null;
            }

            if (!enemy.IsAlive || enemy.IsStunned)
            {
                ForceReleaseGrapple();
                yield break;
            }

            // Start lunge
            enemy.HideCounterCue();
            enemy.isPreparingAttack = false;
            
            // Lunge toward the player
            float elapsed = 0f;
            bool hitLanded = false;

            // Optional: trigger attack animation on the enemy
            Animator anim = enemy.GetComponentInChildren<Animator>();
            if (anim != null)
                anim.SetTrigger(Animator.StringToHash("AirPunch"));

            while (elapsed < lungeDuration && enemy.IsAlive && !enemy.IsStunned)
            {
                if (playerCombat == null || !playerCombat.IsAlive)
                    break;

                Vector3 dir = (playerCombat.transform.position - transform.position);
                dir.y = 0f;
                float distToPlayer = dir.magnitude;

                if (distToPlayer > 0.01f)
                {
                    transform.rotation = Quaternion.LookRotation(dir.normalized);
                    CharacterController cc = enemy.GetComponent<CharacterController>();
                    if (cc != null && cc.enabled)
                        cc.Move(dir.normalized * lungeSpeed * Time.deltaTime);
                }

                // Check hit mid-lunge
                if (!hitLanded && distToPlayer <= grappleRange)
                {
                    hitLanded = true;
                    // Execute the throw! 2 damage and knockdown.
                    playerCombat.ReceiveDamage(enemy, throwDamage, true);
                    OnThrowComplete.Invoke(this);
                    break; // Stop lunging once we hit
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            nextGrappleTime = Time.time + grappleCooldown;
            isGrappling = false;
            grappleRoutine = null;
        }

        void ForceReleaseGrapple()
        {
            isGrappling = false;
            enemy.isPreparingAttack = false;
            enemy.HideCounterCue();
            grappleRoutine = null;
        }

        public void InterruptGrapple()
        {
            if (!isGrappling)
                return;

            ForceReleaseGrapple();
            nextGrappleTime = Time.time + grappleCooldown * 0.5f;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, grappleRange);
        }
    }
}
