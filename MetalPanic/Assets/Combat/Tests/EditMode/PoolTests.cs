using System.Collections;
using MagnetPanic.Combat;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MagnetPanic.Combat.Tests
{
    public sealed class PoolTests
    {
        GameObject prefab;

        [SetUp]
        public void SetUp()
        {
            Pool.ReleaseAll();
            prefab = new GameObject("Pool Test Prefab");
            prefab.AddComponent<TestPoolable>();
        }

        [TearDown]
        public void TearDown()
        {
            Pool.ReleaseAll();

            if (prefab != null)
                Object.DestroyImmediate(prefab);
        }

        [Test]
        public void Spawn_ReusesWarmupInstanceAndAppliesTransform()
        {
            Pool.Warmup(prefab, 1);
            int countAfterWarmup = Object.FindObjectsByType<TestPoolable>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;

            GameObject instance = Pool.Spawn(prefab, new Vector3(2f, 3f, 4f), Quaternion.Euler(0f, 45f, 0f));
            int countAfterSpawn = Object.FindObjectsByType<TestPoolable>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;

            Assert.That(countAfterSpawn, Is.EqualTo(countAfterWarmup));
            Assert.That(instance.activeSelf, Is.True);
            Assert.That(instance.transform.position, Is.EqualTo(new Vector3(2f, 3f, 4f)));
            Assert.That(instance.transform.rotation.eulerAngles.y, Is.EqualTo(45f).Within(0.01f));
        }

        [Test]
        public void Despawn_ReturnsInstanceForNextSpawn()
        {
            GameObject first = Pool.Spawn(prefab, Vector3.zero, Quaternion.identity);

            Pool.Despawn(first);
            GameObject second = Pool.Spawn(prefab, Vector3.one, Quaternion.identity);

            Assert.That(first.activeSelf, Is.True);
            Assert.That(second, Is.SameAs(first));
            Assert.That(second.transform.position, Is.EqualTo(Vector3.one));
        }

        [Test]
        public void SpawnAndDespawn_InvokePoolableCallbacks()
        {
            TestPoolable instance = Pool.Spawn(prefab.GetComponent<TestPoolable>(), Vector3.zero, Quaternion.identity);

            Pool.Despawn(instance);

            Assert.That(instance.SpawnCount, Is.EqualTo(1));
            Assert.That(instance.DespawnCount, Is.EqualTo(1));
            Assert.That(instance.ActiveDuringSpawn, Is.True);
            Assert.That(instance.ActiveDuringDespawn, Is.True);
        }

        [UnityTest]
        public IEnumerator Despawn_WithDelay_KeepsInstanceActiveUntilDelayExpires()
        {
            GameObject instance = Pool.Spawn(prefab, Vector3.zero, Quaternion.identity);

            Pool.Despawn(instance, 0.05f);

            Assert.That(instance.activeSelf, Is.True);

            yield return new WaitForSeconds(0.08f);

            Assert.That(instance.activeSelf, Is.False);
        }

        [Test]
        public void ReleaseAll_DestroysIdleAndActiveInstances()
        {
            Pool.Warmup(prefab, 1);
            GameObject active = Pool.Spawn(prefab, Vector3.zero, Quaternion.identity);

            Pool.ReleaseAll();

            Assert.That(active == null, Is.True);
            Assert.That(Object.FindObjectsByType<TestPoolable>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, Is.EqualTo(1));
        }

        sealed class TestPoolable : MonoBehaviour, IPoolable
        {
            public int SpawnCount { get; private set; }
            public int DespawnCount { get; private set; }
            public bool ActiveDuringSpawn { get; private set; }
            public bool ActiveDuringDespawn { get; private set; }

            public void OnSpawn()
            {
                SpawnCount++;
                ActiveDuringSpawn = gameObject.activeSelf;
            }

            public void OnDespawn()
            {
                DespawnCount++;
                ActiveDuringDespawn = gameObject.activeSelf;
            }
        }
    }
}
