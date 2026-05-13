using UnityEngine;

namespace MagnetPanic.Combat
{
    [DisallowMultipleComponent]
    public sealed class StrikeTargetIndicator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] ArkhamCombatController combat;
        [SerializeField] StrikeTargetPresenter presenter;

        [Header("Behavior")]
        [SerializeField] bool hideWhileAttacking = true;
        [SerializeField] bool autoCreateLineRendererPresenter = true;

        [Header("Auto Presenter Style")]
        [SerializeField] float ringRadius = 0.95f;
        [SerializeField] float ringHeight = 0.05f;
        [SerializeField] float ringWidth = 0.07f;
        [SerializeField] Color normalColor = new Color(0.2f, 0.85f, 1f, 0.85f);
        [SerializeField] Color counterColor = new Color(1f, 0.78f, 0.18f, 0.95f);

        public ArkhamEnemy CurrentTarget { get; private set; }
        public StrikeTargetState CurrentState { get; private set; } = StrikeTargetState.Hidden;
        public StrikeTargetPresenter Presenter => presenter;

        public static StrikeTargetIndicator EnsureOn(GameObject owner, ArkhamCombatController controller = null)
        {
            StrikeTargetIndicator indicator = owner.GetComponent<StrikeTargetIndicator>();
            if (indicator == null)
                indicator = owner.AddComponent<StrikeTargetIndicator>();

            if (controller != null && indicator.combat == null)
                indicator.combat = controller;

            return indicator;
        }

        public void SetPresenter(StrikeTargetPresenter next)
        {
            if (presenter == next)
                return;

            presenter?.Hide();
            presenter = next;
        }

        void Awake()
        {
            if (combat == null)
                combat = GetComponent<ArkhamCombatController>();

            if (presenter == null && autoCreateLineRendererPresenter)
            {
                presenter = LineRendererStrikeTargetPresenter.Create(
                    transform,
                    ringRadius,
                    ringHeight,
                    ringWidth,
                    normalColor,
                    counterColor);
            }
        }

        void OnDisable()
        {
            HideInternal();
        }

        void LateUpdate()
        {
            if (presenter == null || combat == null)
                return;

            StrikeTargetState state;
            ArkhamEnemy target = ResolveCurrentTarget(out state);

            if (target == null)
            {
                HideInternal();
                return;
            }

            CurrentTarget = target;
            CurrentState = state;
            presenter.Show(target, state);
        }

        void HideInternal()
        {
            if (CurrentTarget == null && CurrentState == StrikeTargetState.Hidden)
                return;

            CurrentTarget = null;
            CurrentState = StrikeTargetState.Hidden;
            presenter?.Hide();
        }

        ArkhamEnemy ResolveCurrentTarget(out StrikeTargetState state)
        {
            state = StrikeTargetState.Hidden;

            if (!combat.IsAlive)
                return null;

            if (hideWhileAttacking && (combat.isAttackingEnemy || combat.isCountering || combat.isDodging))
                return null;

            ArkhamEnemyManager manager = combat.EnemyManager;
            if (manager != null)
            {
                ArkhamEnemy counter = manager.ClosestCounterableEnemy(transform.position, combat.CounterRadius);
                if (counter != null && counter.IsAlive)
                {
                    state = StrikeTargetState.Counterable;
                    return counter;
                }
            }

            ArkhamTargetScanner scanner = combat.TargetScanner;
            if (scanner == null)
                return null;

            ArkhamEnemy strike = scanner.FindTarget(manager, transform.position, combat.PreferredStrikeDirection);
            if (strike == null || !strike.IsAlive)
                return null;

            state = StrikeTargetState.Normal;
            return strike;
        }
    }
}
