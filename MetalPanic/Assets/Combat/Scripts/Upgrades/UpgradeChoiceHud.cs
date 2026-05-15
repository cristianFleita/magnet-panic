using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace MagnetPanic.Combat.Upgrades
{
    /// <summary>
    /// UI Toolkit panel that presents 3 upgrade cards. Unlocks the cursor while
    /// visible, supports mouse click and keys 1/2/3. Runs on unscaled time so it
    /// works while <c>Time.timeScale = 0</c>.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class UpgradeChoiceHud : MonoBehaviour
    {
        [SerializeField] UpgradeSystem upgradeSystem;
        [SerializeField] Color panelBackground = new Color(0.02f, 0.04f, 0.07f, 0.82f);
        [SerializeField] Color cardBackground = new Color(0.08f, 0.10f, 0.14f, 0.95f);
        [SerializeField] Color cardHoverBackground = new Color(0.14f, 0.18f, 0.24f, 1f);
        [SerializeField] Color titleColor = new Color(0.95f, 0.97f, 1f, 1f);
        [SerializeField] Color bodyColor = new Color(0.78f, 0.86f, 0.94f, 1f);

        UIDocument document;
        VisualElement root;
        VisualElement panel;
        readonly List<CardBinding> cards = new List<CardBinding>(3);
        Action<UpgradeData> callback;
        bool visible;
        CursorLockMode prevLockMode;
        bool prevCursorVisible;

        struct CardBinding
        {
            public VisualElement Root;
            public Label Title;
            public Label Body;
            public Label Hotkey;
            public Label Stack;
            public UpgradeData Data;
            public Color Accent;
        }

        void Awake()
        {
            document = GetComponent<UIDocument>();
        }

        void OnDisable()
        {
            if (visible)
                RestoreCursor();
        }

        void Update()
        {
            if (!visible || Keyboard.current == null)
                return;

            // Keyboard 1 / 2 / 3 selection — read directly to bypass action map state.
            if (Keyboard.current.digit1Key.wasPressedThisFrame || Keyboard.current.numpad1Key.wasPressedThisFrame)
                TrySelectIndex(0);
            else if (Keyboard.current.digit2Key.wasPressedThisFrame || Keyboard.current.numpad2Key.wasPressedThisFrame)
                TrySelectIndex(1);
            else if (Keyboard.current.digit3Key.wasPressedThisFrame || Keyboard.current.numpad3Key.wasPressedThisFrame)
                TrySelectIndex(2);
        }

        public void Show(List<UpgradeData> offers, int newLevel, Action<UpgradeData> onChosen)
        {
            callback = onChosen;
            EnsureRoot();
            BuildPanel(offers, newLevel);

            visible = true;
            root.style.display = DisplayStyle.Flex;

            CaptureCursorState();
            UnityEngine.Cursor.visible = true;
            UnityEngine.Cursor.lockState = CursorLockMode.None;
        }

        public void Hide()
        {
            if (!visible)
                return;
            visible = false;
            if (root != null)
                root.style.display = DisplayStyle.None;
            RestoreCursor();
            callback = null;
        }

        void CaptureCursorState()
        {
            prevLockMode = UnityEngine.Cursor.lockState;
            prevCursorVisible = UnityEngine.Cursor.visible;
        }

        void RestoreCursor()
        {
            UnityEngine.Cursor.visible = prevCursorVisible;
            UnityEngine.Cursor.lockState = prevLockMode;
        }

        void EnsureRoot()
        {
            if (document == null)
                document = GetComponent<UIDocument>();

            if (document == null || document.rootVisualElement == null)
                return;

            if (root != null && root.parent == document.rootVisualElement)
                return;

            root = new VisualElement { name = "upgrade-hud-root" };
            root.style.position = Position.Absolute;
            root.style.left = 0;
            root.style.right = 0;
            root.style.top = 0;
            root.style.bottom = 0;
            root.style.backgroundColor = panelBackground;
            root.style.alignItems = Align.Center;
            root.style.justifyContent = Justify.Center;
            root.style.display = DisplayStyle.None;
            document.rootVisualElement.Add(root);
        }

        void BuildPanel(List<UpgradeData> offers, int newLevel)
        {
            root.Clear();
            cards.Clear();

            VisualElement container = new VisualElement();
            container.style.flexDirection = FlexDirection.Column;
            container.style.alignItems = Align.Center;
            container.style.justifyContent = Justify.Center;
            container.style.paddingTop = 12;
            container.style.paddingBottom = 12;
            container.style.paddingLeft = 20;
            container.style.paddingRight = 20;
            root.Add(container);

            Label title = new Label($"LEVEL {newLevel}  —  CHOOSE YOUR UPGRADE");
            title.style.color = titleColor;
            title.style.fontSize = 26;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 18;
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            container.Add(title);

            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.justifyContent = Justify.Center;
            container.Add(row);

            for (int i = 0; i < offers.Count; i++)
            {
                CardBinding card = BuildCard(offers[i], i + 1);
                row.Add(card.Root);
                cards.Add(card);
            }

            Label footer = new Label("Press 1 / 2 / 3 or click to choose");
            footer.style.color = bodyColor;
            footer.style.fontSize = 14;
            footer.style.marginTop = 16;
            footer.style.opacity = 0.8f;
            container.Add(footer);
        }

        CardBinding BuildCard(UpgradeData data, int hotkeyNumber)
        {
            CardBinding card = new CardBinding
            {
                Data = data,
                Accent = data.accentColor
            };

            VisualElement cardRoot = new VisualElement { name = $"upgrade-card-{hotkeyNumber}" };
            cardRoot.style.width = 240;
            cardRoot.style.height = 320;
            cardRoot.style.marginLeft = 12;
            cardRoot.style.marginRight = 12;
            cardRoot.style.paddingTop = 16;
            cardRoot.style.paddingBottom = 16;
            cardRoot.style.paddingLeft = 16;
            cardRoot.style.paddingRight = 16;
            cardRoot.style.backgroundColor = cardBackground;
            cardRoot.style.borderTopWidth = 3;
            cardRoot.style.borderBottomWidth = 0;
            cardRoot.style.borderLeftWidth = 0;
            cardRoot.style.borderRightWidth = 0;
            cardRoot.style.borderTopColor = card.Accent;
            cardRoot.style.flexDirection = FlexDirection.Column;
            cardRoot.style.justifyContent = Justify.SpaceBetween;
            cardRoot.pickingMode = PickingMode.Position;
            card.Root = cardRoot;

            Label hotkey = new Label($"[{hotkeyNumber}]");
            hotkey.style.color = card.Accent;
            hotkey.style.fontSize = 14;
            hotkey.style.unityFontStyleAndWeight = FontStyle.Bold;
            cardRoot.Add(hotkey);
            card.Hotkey = hotkey;

            Label name = new Label(data.displayName);
            name.style.color = titleColor;
            name.style.fontSize = 20;
            name.style.unityFontStyleAndWeight = FontStyle.Bold;
            name.style.marginTop = 6;
            name.style.marginBottom = 10;
            name.style.whiteSpace = WhiteSpace.Normal;
            cardRoot.Add(name);
            card.Title = name;

            Label branchLabel = new Label(data.branch.ToString().ToUpperInvariant());
            branchLabel.style.color = card.Accent;
            branchLabel.style.fontSize = 11;
            branchLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            branchLabel.style.marginBottom = 8;
            cardRoot.Add(branchLabel);

            Label body = new Label(data.description);
            body.style.color = bodyColor;
            body.style.fontSize = 14;
            body.style.whiteSpace = WhiteSpace.Normal;
            body.style.flexGrow = 1;
            cardRoot.Add(body);
            card.Body = body;

            int currentStacks = upgradeSystem != null ? upgradeSystem.GetStacks(data.id) : 0;
            Label stack = new Label($"Stack: {currentStacks + 1} / {data.maxStacks}");
            stack.style.color = bodyColor;
            stack.style.fontSize = 12;
            stack.style.marginTop = 10;
            stack.style.opacity = 0.85f;
            cardRoot.Add(stack);
            card.Stack = stack;

            int captured = hotkeyNumber - 1;
            cardRoot.RegisterCallback<MouseEnterEvent>(_ =>
            {
                cardRoot.style.backgroundColor = cardHoverBackground;
                cardRoot.style.scale = new StyleScale(new Scale(new Vector3(1.04f, 1.04f, 1f)));
            });
            cardRoot.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                cardRoot.style.backgroundColor = cardBackground;
                cardRoot.style.scale = new StyleScale(new Scale(Vector3.one));
            });
            cardRoot.RegisterCallback<ClickEvent>(_ => TrySelectIndex(captured));

            return card;
        }

        void TrySelectIndex(int index)
        {
            if (!visible || index < 0 || index >= cards.Count)
                return;

            UpgradeData picked = cards[index].Data;
            Action<UpgradeData> cb = callback;
            // Hide() clears callback, so cache first.
            cb?.Invoke(picked);
        }
    }
}
