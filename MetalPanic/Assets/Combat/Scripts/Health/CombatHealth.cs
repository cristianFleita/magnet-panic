using UnityEngine;
using UnityEngine.Events;

namespace MagnetPanic.Combat
{
    public sealed class CombatHealth : MonoBehaviour
    {
        [SerializeField] int maxHealth = 5;
        [SerializeField] bool refillOnAwake = true;

        [Header("Events")]
        public UnityEvent<CombatHealth> OnHealthChanged = new UnityEvent<CombatHealth>();
        public UnityEvent<CombatHealth> OnDeath = new UnityEvent<CombatHealth>();
        public UnityEvent<CombatHealth> OnHealed = new UnityEvent<CombatHealth>();
        public UnityEvent<CombatHealth> OnDamaged = new UnityEvent<CombatHealth>();

        HealthPool pool;
        bool deathRaised;

        public int MaxHealth => Pool.MaxHealth;
        public int CurrentHealth => Pool.CurrentHealth;
        public float Normalized => Pool.Normalized;
        public bool IsAlive => Pool.IsAlive;
        public bool IsFull => Pool.IsFull;

        HealthPool Pool
        {
            get
            {
                pool ??= new HealthPool(maxHealth);
                return pool;
            }
        }

        void Awake()
        {
            EnsureEvents();
            Configure(maxHealth, refillOnAwake);
        }

        public void Configure(int healthMax, bool refill)
        {
            maxHealth = Mathf.Max(1, healthMax);
            if (pool == null)
                pool = new HealthPool(maxHealth);
            else
                pool.SetMaxHealth(maxHealth, refill);

            deathRaised = !pool.IsAlive;
            OnHealthChanged.Invoke(this);
        }

        public bool ApplyDamage(int amount)
        {
            bool changed = Pool.ApplyDamage(amount);
            if (!changed)
                return false;

            OnDamaged.Invoke(this);
            OnHealthChanged.Invoke(this);

            if (!Pool.IsAlive && !deathRaised)
            {
                deathRaised = true;
                OnDeath.Invoke(this);
            }

            return true;
        }

        public bool Heal(int amount)
        {
            bool changed = Pool.Heal(amount);
            if (!changed)
                return false;

            OnHealed.Invoke(this);
            OnHealthChanged.Invoke(this);
            return true;
        }

        public void Refill()
        {
            Pool.Reset();
            deathRaised = false;
            OnHealthChanged.Invoke(this);
        }

        void EnsureEvents()
        {
            OnHealthChanged ??= new UnityEvent<CombatHealth>();
            OnDeath ??= new UnityEvent<CombatHealth>();
            OnHealed ??= new UnityEvent<CombatHealth>();
            OnDamaged ??= new UnityEvent<CombatHealth>();
        }
    }
}
