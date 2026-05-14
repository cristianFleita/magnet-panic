using MagnetPanic.Combat;
using NUnit.Framework;
using UnityEngine;

namespace MagnetPanic.Combat.Tests
{
    public sealed class GameInputProviderTests
    {
        GameObject owner;
        GameInputProvider provider;

        [SetUp]
        public void SetUp()
        {
            owner = new GameObject("Input Provider Test Owner");
            provider = owner.AddComponent<GameInputProvider>();
            provider.BufferWindow = 0.15f;
            provider.SetState(GameInputState.Gameplay);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(owner);
        }

        [Test]
        public void CombatBuffer_ConsumesRecentPressOnlyOnce()
        {
            GameInputBuffer buffer = new GameInputBuffer();
            buffer.Record(GameInputIntent.Strike, 10f);

            Assert.That(buffer.Consume(GameInputIntent.Strike, 10.1f, 0.15f), Is.True);
            Assert.That(buffer.Consume(GameInputIntent.Strike, 10.1f, 0.15f), Is.False);
        }

        [Test]
        public void CombatBuffer_ExpiresOldPresses()
        {
            GameInputBuffer buffer = new GameInputBuffer();
            buffer.Record(GameInputIntent.Counter, 3f);

            Assert.That(buffer.Consume(GameInputIntent.Counter, 3.2f, 0.15f), Is.False);
        }

        [Test]
        public void CounterPressedInSameFrame_SuppressesStrike()
        {
            provider.OnStrike();
            provider.OnCounter();

            Assert.That(provider.CounterPressed, Is.True);
            Assert.That(provider.StrikePressed, Is.False);
            Assert.That(provider.ConsumeBuffered(GameInputIntent.Counter), Is.True);
            Assert.That(provider.ConsumeBuffered(GameInputIntent.Strike), Is.False);
        }

        [Test]
        public void DodgePressedInSameFrame_SuppressesStrike()
        {
            provider.OnStrike();
            provider.OnDodge();

            Assert.That(provider.DodgePressed, Is.True);
            Assert.That(provider.StrikePressed, Is.False);
            Assert.That(provider.ConsumeBuffered(GameInputIntent.Dodge), Is.True);
            Assert.That(provider.ConsumeBuffered(GameInputIntent.Strike), Is.False);
        }

        [Test]
        public void CounterPressedInSameFrame_SuppressesDodge()
        {
            provider.OnDodge();
            provider.OnCounter();

            Assert.That(provider.CounterPressed, Is.True);
            Assert.That(provider.DodgePressed, Is.False);
            Assert.That(provider.ConsumeBuffered(GameInputIntent.Counter), Is.True);
            Assert.That(provider.ConsumeBuffered(GameInputIntent.Dodge), Is.False);
        }
    }
}
