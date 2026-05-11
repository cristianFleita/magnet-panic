using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MagnetPanic.Combat
{
    public static class Pool
    {
        static readonly Dictionary<int, PoolBucket> Buckets = new Dictionary<int, PoolBucket>(32);
        static PoolRunner runner;

        public static T Spawn<T>(T prefab, Vector3 position, Quaternion rotation, Transform parent = null)
            where T : Component
        {
            if (prefab == null)
                return null;

            GameObject instance = Spawn(prefab.gameObject, position, rotation, parent);
            return instance != null ? instance.GetComponent<T>() : null;
        }

        public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            if (prefab == null)
                return null;

            int prefabId = prefab.GetInstanceID();
            if (!Buckets.TryGetValue(prefabId, out PoolBucket bucket))
            {
                bucket = new PoolBucket(prefab, prefabId, EnsureRunner().Root);
                Buckets.Add(prefabId, bucket);
            }

            return bucket.Spawn(position, rotation, parent);
        }

        public static void Despawn<T>(T instance, float delay = 0f)
            where T : Component
        {
            if (instance == null)
                return;

            Despawn(instance.gameObject, delay);
        }

        public static void Despawn(GameObject instance, float delay = 0f)
        {
            if (instance == null)
                return;

            if (delay > 0f)
            {
                PoolIdentity identity = instance.GetComponent<PoolIdentity>();
                if (identity != null && identity.DelayedDespawn != null)
                    EnsureRunner().StopCoroutine(identity.DelayedDespawn);

                Coroutine coroutine = EnsureRunner().StartCoroutine(DespawnAfterDelay(instance, delay));
                if (identity != null)
                    identity.DelayedDespawn = coroutine;
                return;
            }

            DespawnNow(instance);
        }

        public static void Warmup(GameObject prefab, int count)
        {
            if (prefab == null || count <= 0)
                return;

            int prefabId = prefab.GetInstanceID();
            if (!Buckets.TryGetValue(prefabId, out PoolBucket bucket))
            {
                bucket = new PoolBucket(prefab, prefabId, EnsureRunner().Root);
                Buckets.Add(prefabId, bucket);
            }

            bucket.Warmup(count);
        }

        public static void ReleaseAll()
        {
            if (runner != null)
                runner.StopAllCoroutines();

            foreach (PoolBucket bucket in Buckets.Values)
                bucket.Release();

            Buckets.Clear();
        }

        static IEnumerator DespawnAfterDelay(GameObject instance, float delay)
        {
            yield return new WaitForSeconds(delay);

            if (instance != null)
                DespawnNow(instance);
        }

        static void DespawnNow(GameObject instance)
        {
            if (instance == null || !instance.activeSelf)
                return;

            PoolIdentity identity = instance.GetComponent<PoolIdentity>();
            if (identity != null)
                identity.DelayedDespawn = null;

            if (identity != null && identity.Bucket != null && Buckets.ContainsKey(identity.PrefabId))
            {
                identity.Bucket.Return(identity);
                return;
            }

            InvokeDespawnCallbacks(instance);
            instance.SetActive(false);
        }

        static PoolRunner EnsureRunner()
        {
            if (runner != null)
                return runner;

            GameObject root = new GameObject("[Pool]");
            root.hideFlags = HideFlags.HideInHierarchy;
            if (Application.isPlaying)
                Object.DontDestroyOnLoad(root);

            runner = root.AddComponent<PoolRunner>();
            return runner;
        }

        static void InvokeDespawnCallbacks(GameObject instance)
        {
            MonoBehaviour[] behaviours = instance.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IPoolable poolable)
                    poolable.OnDespawn();
            }
        }

        static void DestroyObject(Object target)
        {
            if (target == null)
                return;

            if (Application.isPlaying)
                Object.Destroy(target);
            else
                Object.DestroyImmediate(target);
        }

        internal sealed class PoolBucket
        {
            readonly GameObject prefab;
            readonly int prefabId;
            readonly Transform root;
            readonly Queue<PoolIdentity> idle = new Queue<PoolIdentity>(16);
            readonly HashSet<PoolIdentity> active = new HashSet<PoolIdentity>();

            int totalCreated;
            int softCap;

            public PoolBucket(GameObject prefab, int prefabId, Transform root)
            {
                this.prefab = prefab;
                this.prefabId = prefabId;
                this.root = root;
                softCap = 4;
            }

            public void Warmup(int count)
            {
                int missing = Mathf.Max(0, count - idle.Count);
                softCap = Mathf.Max(softCap, count * 4);

                for (int i = 0; i < missing; i++)
                {
                    PoolIdentity identity = CreateInstance();
                    identity.gameObject.SetActive(false);
                    identity.transform.SetParent(root, true);
                    identity.IsInPool = true;
                    idle.Enqueue(identity);
                }
            }

            public GameObject Spawn(Vector3 position, Quaternion rotation, Transform parent)
            {
                PoolIdentity identity = TakeIdle();
                if (identity == null)
                    identity = CreateInstance();

                identity.IsInPool = false;
                if (parent != null)
                    identity.transform.SetParent(parent, true);
                else
                    identity.transform.SetParent(null, true);

                identity.transform.SetPositionAndRotation(position, rotation);
                active.Add(identity);
                identity.gameObject.SetActive(true);
                identity.InvokeSpawn();
                return identity.gameObject;
            }

            public void Return(PoolIdentity identity)
            {
                if (identity == null || identity.IsInPool)
                    return;

                active.Remove(identity);
                identity.InvokeDespawn();
                identity.gameObject.SetActive(false);
                identity.transform.SetParent(root, true);
                identity.IsInPool = true;
                idle.Enqueue(identity);
            }

            public void Release()
            {
                foreach (PoolIdentity identity in active)
                {
                    if (identity == null)
                        continue;

                    identity.InvokeDespawn();
                    DestroyObject(identity.gameObject);
                }

                while (idle.Count > 0)
                {
                    PoolIdentity identity = idle.Dequeue();
                    if (identity != null)
                        DestroyObject(identity.gameObject);
                }

                active.Clear();
            }

            PoolIdentity TakeIdle()
            {
                while (idle.Count > 0)
                {
                    PoolIdentity identity = idle.Dequeue();
                    if (identity != null)
                        return identity;
                }

                return null;
            }

            PoolIdentity CreateInstance()
            {
                GameObject instance = Object.Instantiate(prefab, root);
                instance.name = prefab.name;

                PoolIdentity identity = instance.GetComponent<PoolIdentity>();
                if (identity == null)
                    identity = instance.AddComponent<PoolIdentity>();

                identity.Configure(prefabId, this);
                totalCreated++;

                if (totalCreated > softCap)
                    Debug.LogWarning($"Pool for '{prefab.name}' grew past soft cap ({softCap}). Active spawn pressure may be too high.", prefab);

                return identity;
            }
        }

    }

}
