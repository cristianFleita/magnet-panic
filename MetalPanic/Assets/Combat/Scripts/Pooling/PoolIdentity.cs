using System.Collections.Generic;
using UnityEngine;

namespace MagnetPanic.Combat
{
    public sealed class PoolIdentity : MonoBehaviour
    {
        IPoolable[] poolables;

        internal int PrefabId { get; private set; }
        internal Pool.PoolBucket Bucket { get; private set; }
        internal bool IsInPool { get; set; }
        internal Coroutine DelayedDespawn { get; set; }

        internal void Configure(int prefabId, Pool.PoolBucket bucket)
        {
            PrefabId = prefabId;
            Bucket = bucket;
            CachePoolables();
        }

        internal void InvokeSpawn()
        {
            for (int i = 0; i < poolables.Length; i++)
                poolables[i].OnSpawn();
        }

        internal void InvokeDespawn()
        {
            for (int i = 0; i < poolables.Length; i++)
                poolables[i].OnDespawn();
        }

        void CachePoolables()
        {
            MonoBehaviour[] behaviours = GetComponentsInChildren<MonoBehaviour>(true);
            List<IPoolable> found = new List<IPoolable>(behaviours.Length);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IPoolable poolable)
                    found.Add(poolable);
            }

            poolables = found.ToArray();
        }
    }
}
