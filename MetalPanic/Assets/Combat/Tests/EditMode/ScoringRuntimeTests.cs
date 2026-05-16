using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace MagnetPanic.Combat.Tests
{
    public sealed class ScoringRuntimeTests
    {
        Type scoringRuntimeType;
        GameObject owner;
        Component scoring;

        [SetUp]
        public void SetUp()
        {
            scoringRuntimeType = Type.GetType("MagnetPanic.Combat.Scoring.ScoringRuntime, Assembly-CSharp", throwOnError: true);
            owner = new GameObject("Scoring Runtime Test Owner");
            scoring = owner.AddComponent(scoringRuntimeType);
            scoringRuntimeType.GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(scoring, null);
            scoringRuntimeType.GetMethod("BeginRun").Invoke(scoring, null);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(owner);
        }

        [Test]
        public void ScoreIncludesXpAndComboStyleButIgnoresSurvivalTime()
        {
            object stats = scoringRuntimeType.GetProperty("Stats").GetValue(scoring);
            stats.GetType().GetField("SurvivalTimeSeconds").SetValue(stats, 123f);
            stats.GetType().GetField("MaxComboReached").SetValue(stats, 4);

            scoringRuntimeType.GetMethod("ReportRawXp").Invoke(scoring, new object[] { 50 });

            long score = (long)scoringRuntimeType.GetProperty("Score").GetValue(scoring);
            Assert.That(score, Is.EqualTo(90));
        }
    }
}
