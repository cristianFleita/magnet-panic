using UnityEngine;

namespace MagnetPanic.Combat
{
    /// <summary>
    /// Player-side "magnetic sense" cue. Replaces the per-enemy counter
    /// indicator: a single VFX over the player's head turns on whenever
    /// at least one counterable enemy is telegraphing an attack inside
    /// the counter radius. Spider-Man-style spider sense.
    ///
    /// HeavyBot attacks intentionally do NOT trigger this — heavies aren't
    /// counterable, so the cue would lie. The sense is read from
    /// <see cref="ArkhamEnemy.IsCounterTarget"/>, which already filters
    /// uncounterable archetypes.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CounterSenseIndicator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] ArkhamCombatController combat;

        [Header("VFX")]
        [Tooltip("Optional prefab spawned once at startup and toggled on/off above the player's head. If null, a fallback billboard primitive is created so the cue is still visible during prototyping.")]
        [SerializeField] GameObject senseVfxPrefab;
        [SerializeField] float vfxHeight = 2.1f;
        [SerializeField] Color fallbackColor = new Color(1f, 0.42f, 0.16f, 0.85f);

        [Header("Tuning")]
        [Tooltip("Maximum distance for non-shooter enemies to trigger the sense. Set to the player's strike reach so the cue only fires when a counter would actually land. Shooters firing always trigger regardless of this radius.")]
        [SerializeField] float strikeRadius = 2f;
        [Tooltip("Hide the cue briefly while the player is mid-attack or mid-counter so it doesn't fight the combat animation.")]
        [SerializeField] bool hideWhileBusy = true;

        GameObject vfxInstance;
        bool cueActive;

        public bool CueActive => cueActive;

        void Awake()
        {
            if (combat == null)
                combat = GetComponentInParent<ArkhamCombatController>();

            EnsureVfxInstance();
            SetCue(false);
        }

        void OnDisable()
        {
            SetCue(false);
        }

        void Update()
        {
            if (combat == null || !combat.IsAlive)
            {
                SetCue(false);
                return;
            }

            if (hideWhileBusy && (combat.isAttackingEnemy || combat.isCountering || combat.isDodging))
            {
                SetCue(false);
                return;
            }

            ArkhamEnemyManager manager = combat.EnemyManager;
            if (manager == null)
            {
                SetCue(false);
                return;
            }

            bool any = manager.HasImminentSenseThreat(combat.transform.position, strikeRadius);
            SetCue(any);
        }

        void EnsureVfxInstance()
        {
            if (vfxInstance != null)
                return;

            if (senseVfxPrefab != null)
            {
                vfxInstance = Instantiate(senseVfxPrefab, transform);
                vfxInstance.transform.localPosition = new Vector3(0f, vfxHeight, 0f);
                vfxInstance.transform.localRotation = Quaternion.identity;
                vfxInstance.name = senseVfxPrefab.name + " (CounterSense)";
                return;
            }

            // Fallback primitive so the cue still reads while the art VFX is being authored.
            GameObject cue = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            cue.name = "Counter Sense Cue (Fallback)";
            cue.transform.SetParent(transform, false);
            cue.transform.localPosition = new Vector3(0f, vfxHeight, 0f);
            cue.transform.localScale = Vector3.one * 0.32f;

            Collider col = cue.GetComponent<Collider>();
            if (col != null)
                Destroy(col);

            Renderer renderer = cue.GetComponent<Renderer>();
            if (renderer != null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null)
                    shader = Shader.Find("Sprites/Default");
                if (shader == null)
                    shader = Shader.Find("Standard");

                renderer.sharedMaterial = new Material(shader) { color = fallbackColor };
            }

            vfxInstance = cue;
        }

        void SetCue(bool active)
        {
            if (cueActive == active)
                return;

            cueActive = active;
            if (vfxInstance != null)
                vfxInstance.SetActive(active);
        }

        public static CounterSenseIndicator EnsureOn(GameObject host, ArkhamCombatController owner)
        {
            if (host == null)
                return null;

            CounterSenseIndicator existing = host.GetComponent<CounterSenseIndicator>();
            if (existing != null)
            {
                if (existing.combat == null)
                    existing.combat = owner;
                return existing;
            }

            CounterSenseIndicator created = host.AddComponent<CounterSenseIndicator>();
            created.combat = owner;
            return created;
        }
    }
}
