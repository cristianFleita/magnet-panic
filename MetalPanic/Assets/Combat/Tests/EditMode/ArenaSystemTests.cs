using MagnetPanic.Combat;
using NUnit.Framework;
using UnityEngine;

namespace MagnetPanic.Combat.Tests
{
    public sealed class ArenaSystemTests
    {
        GameObject owner;
        ArenaSystem arena;

        [SetUp]
        public void SetUp()
        {
            owner = new GameObject("Arena Test Owner");
            arena = owner.AddComponent<ArenaSystem>();
            arena.ConfigurePlayableBounds(Vector3.zero, new Vector3(20f, 4f, 10f));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(owner);
        }

        [Test]
        public void ClampToArena_ClampsHorizontalAxesAndPreservesHeight()
        {
            Vector3 clamped = arena.ClampToArena(new Vector3(14f, 99f, -8f));

            Assert.That(clamped.x, Is.EqualTo(10f).Within(0.001f));
            Assert.That(clamped.y, Is.EqualTo(99f).Within(0.001f));
            Assert.That(clamped.z, Is.EqualTo(-5f).Within(0.001f));
            Assert.That(arena.IsInsideArena(clamped), Is.True);
        }

        [Test]
        public void GetSpawnPointAwayFromPlayer_UsesValidPointOutsideMinimumDistance()
        {
            ArenaSpawnPoint near = CreateSpawn("Near Enemy Spawn", ArenaSpawnCategory.Enemy, new Vector3(1f, 0f, 0f));
            ArenaSpawnPoint far = CreateSpawn("Far Enemy Spawn", ArenaSpawnCategory.Enemy, new Vector3(9f, 0f, 0f));
            arena.RebuildSpawnCache();

            Vector3 spawn = arena.GetSpawnPointAwayFromPlayer(ArenaSpawnCategory.Enemy, Vector3.zero, 8f);

            Assert.That(spawn, Is.EqualTo(far.transform.position));
            Assert.That(spawn, Is.Not.EqualTo(near.transform.position));
        }

        [Test]
        public void CalculateWallSlamDamage_AddsSpeedScaledBonus()
        {
            arena.ConfigureWallSlamDamage(2, 5f);

            Assert.That(arena.CalculateWallSlamDamage(16f), Is.EqualTo(5));
        }

        [Test]
        public void MapColliderBuilder_AddsMeshAndGroundProxyColliders()
        {
            GameObject map = new GameObject("Map");
            GameObject room = new GameObject("room-large");
            room.transform.SetParent(map.transform, false);
            MeshFilter filter = room.AddComponent<MeshFilter>();
            filter.sharedMesh = CreateQuadMesh();
            room.AddComponent<MeshRenderer>();

            ArenaMapColliderBuilder builder = map.AddComponent<ArenaMapColliderBuilder>();
            builder.BuildNow();

            Assert.That(room.GetComponent<MeshCollider>(), Is.Not.Null);
            Assert.That(map.transform.Find("Generated Arena Colliders/Ground - room-large"), Is.Not.Null);

            Object.DestroyImmediate(map);
        }

        ArenaSpawnPoint CreateSpawn(string name, ArenaSpawnCategory category, Vector3 position)
        {
            GameObject spawn = new GameObject(name);
            spawn.transform.SetParent(owner.transform, false);
            spawn.transform.position = position;

            ArenaSpawnPoint point = spawn.AddComponent<ArenaSpawnPoint>();
            point.Configure(category);
            return point;
        }

        static Mesh CreateQuadMesh()
        {
            Mesh mesh = new Mesh();
            mesh.vertices = new[]
            {
                new Vector3(-1f, 0f, -1f),
                new Vector3(-1f, 0f, 1f),
                new Vector3(1f, 0f, -1f),
                new Vector3(1f, 0f, 1f)
            };
            mesh.triangles = new[] { 0, 1, 2, 2, 1, 3 };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
