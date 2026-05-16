using NUnit.Framework;
using UnityEngine.UIElements;

namespace MagnetPanic.UI.Tests
{
    public sealed class UiDocumentQueryTests
    {
        [Test]
        public void TryGetButtonFindsNamedButton()
        {
            VisualElement root = new VisualElement();
            Button play = new Button { name = MainMenuController.PlayRunButtonName };
            root.Add(play);

            bool found = UiDocumentQuery.TryGetButton(root, MainMenuController.PlayRunButtonName, out Button button);

            Assert.That(found, Is.True);
            Assert.That(button, Is.SameAs(play));
        }

        [Test]
        public void TryGetButtonReturnsFalseWhenNameIsMissing()
        {
            VisualElement root = new VisualElement();
            root.Add(new Button { name = MainMenuController.ControlsGuideButtonName });

            bool found = UiDocumentQuery.TryGetButton(root, MainMenuController.PlayRunButtonName, out Button button);

            Assert.That(found, Is.False);
            Assert.That(button, Is.Null);
        }
    }
}
