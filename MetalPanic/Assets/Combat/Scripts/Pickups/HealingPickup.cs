using UnityEngine;

namespace MagnetPanic.Combat
{
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class HealingPickup : MonoBehaviour
    {
        [SerializeField] int healAmount = 2;
        [SerializeField] bool destroyOnUse = true;
        [SerializeField] float spinSpeed = 120f;
        [SerializeField] float bobAmplitude = 0.12f;
        [SerializeField] float bobSpeed = 2.6f;

        Vector3 startPosition;

        void Awake()
        {
            Collider pickupCollider = GetComponent<Collider>();
            pickupCollider.isTrigger = true;
            Rigidbody body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            startPosition = transform.position;
        }

        void Update()
        {
            transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
            Vector3 position = startPosition;
            position.y += Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
            transform.position = position;
        }

        public void Configure(int amount)
        {
            healAmount = Mathf.Max(1, amount);
        }

        void OnTriggerEnter(Collider other)
        {
            CombatHealth health = other.GetComponentInParent<CombatHealth>();
            if (health == null || !health.IsAlive || health.IsFull)
                return;

            if (!health.Heal(healAmount))
                return;

            if (destroyOnUse)
                Destroy(gameObject);
            else
                gameObject.SetActive(false);
        }
    }
}
