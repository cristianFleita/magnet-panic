using System;

namespace MagnetPanic.Combat
{
    public sealed class HealthPool
    {
        int maxHealth;
        int currentHealth;

        public int MaxHealth => maxHealth;
        public int CurrentHealth => currentHealth;
        public bool IsAlive => currentHealth > 0;
        public bool IsFull => currentHealth >= maxHealth;
        public float Normalized => maxHealth > 0 ? currentHealth / (float)maxHealth : 0f;

        public HealthPool(int maxHealth)
        {
            SetMaxHealth(maxHealth, true);
        }

        public void SetMaxHealth(int value, bool refill)
        {
            maxHealth = Math.Max(1, value);
            currentHealth = refill
                ? maxHealth
                : Clamp(currentHealth, 0, maxHealth);
        }

        public bool ApplyDamage(int amount)
        {
            if (!IsAlive || amount <= 0)
                return false;

            int previous = currentHealth;
            currentHealth = Clamp(currentHealth - amount, 0, maxHealth);
            return currentHealth != previous;
        }

        public bool Heal(int amount)
        {
            if (!IsAlive || amount <= 0 || IsFull)
                return false;

            int previous = currentHealth;
            currentHealth = Clamp(currentHealth + amount, 0, maxHealth);
            return currentHealth != previous;
        }

        public void Reset()
        {
            currentHealth = maxHealth;
        }

        static int Clamp(int value, int min, int max)
        {
            if (value < min)
                return min;
            if (value > max)
                return max;
            return value;
        }
    }
}
