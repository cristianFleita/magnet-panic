using System.IO;
using NUnit.Framework;
using UnityEngine.UIElements;
using UnityEditor;

namespace MagnetPanic.UI.Tests
{
    public sealed class HudDocumentContractTests
    {
        const string HudDocumentPath = "Assets/UI/HUD/HudDocument.uxml";
        const string MissionHudPath = "Assets/Combat/Scripts/Missions/UI/MissionHud.cs";

        [Test]
        public void HudDocumentContainsSharedRuntimeElements()
        {
            VisualTreeAsset asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(HudDocumentPath);
            Assert.That(asset, Is.Not.Null);

            VisualElement root = new VisualElement();
            asset.CloneTree(root);

            Assert.That(root.Q<VisualElement>("hud-root"), Is.Not.Null);
            Assert.That(root.Q<VisualElement>("hp-fill"), Is.Not.Null);
            Assert.That(root.Q<VisualElement>("xp-fill"), Is.Not.Null);
            Assert.That(root.Q<Label>("score-value"), Is.Not.Null);
            Assert.That(root.Q<VisualElement>("mission-card"), Is.Not.Null);
            Assert.That(root.Q<Label>("mission-title"), Is.Not.Null);
        }

        [Test]
        public void MissionHudDoesNotClearSharedHudDocument()
        {
            string script = File.ReadAllText(MissionHudPath);

            Assert.That(script, Does.Not.Contain("root.Clear()"));
        }
    }
}
