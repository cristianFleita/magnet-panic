using System.Collections.Generic;
using UnityEngine;

namespace MagnetPanic.Combat
{
    public sealed class DamagePopupPool : MonoBehaviour
    {
        [SerializeField] DamagePopup popupPrefab;
        [SerializeField] int prewarm = 16;
        [SerializeField] int hardCap = 128;

        readonly Queue<DamagePopup> available = new Queue<DamagePopup>();
        int totalCreated;

        static DamagePopupPool instance;

        public static DamagePopupPool EnsureInstance()
        {
            if (instance != null)
                return instance;

            GameObject host = new GameObject("[DamagePopupPool]");
            instance = host.AddComponent<DamagePopupPool>();
            return instance;
        }

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;

            for (int i = 0; i < prewarm; i++)
            {
                DamagePopup popup = Create();
                if (popup != null)
                    Release(popup);
            }
        }

        void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        public DamagePopup Get()
        {
            DamagePopup popup = null;

            while (available.Count > 0)
            {
                popup = available.Dequeue();
                if (popup != null)
                    break;
            }

            if (popup == null)
                popup = Create();

            return popup;
        }

        public void Release(DamagePopup popup)
        {
            if (popup == null)
                return;

            popup.Cancel();
            popup.transform.SetParent(transform, false);
            available.Enqueue(popup);
        }

        DamagePopup Create()
        {
            if (totalCreated >= hardCap)
                return null;

            DamagePopup popup;
            if (popupPrefab != null)
            {
                popup = Instantiate(popupPrefab, transform);
                popup.name = "DamagePopup";
            }
            else
            {
                GameObject go = new GameObject("DamagePopup");
                go.transform.SetParent(transform, false);
                popup = go.AddComponent<DamagePopup>();
            }

            popup.gameObject.SetActive(false);
            popup.OnExpired += HandleExpired;
            totalCreated++;
            return popup;
        }

        void HandleExpired(DamagePopup popup)
        {
            Release(popup);
        }
    }
}
