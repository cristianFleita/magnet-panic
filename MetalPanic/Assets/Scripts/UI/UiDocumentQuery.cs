using UnityEngine.UIElements;

namespace MagnetPanic.UI
{
    public static class UiDocumentQuery
    {
        public static bool TryGetButton(VisualElement root, string name, out Button button)
        {
            button = root != null ? root.Q<Button>(name) : null;
            return button != null;
        }

        public static bool TryGetLabel(VisualElement root, string name, out Label label)
        {
            label = root != null ? root.Q<Label>(name) : null;
            return label != null;
        }

        public static bool TryGetElement(VisualElement root, string name, out VisualElement element)
        {
            element = root != null ? root.Q<VisualElement>(name) : null;
            return element != null;
        }
    }
}
