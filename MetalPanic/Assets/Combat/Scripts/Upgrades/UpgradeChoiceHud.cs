using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace MagnetPanic.Combat.Upgrades
{
    /// <summary>
    /// UI Toolkit panel that presents 3 upgrade cards. Keyboard-only input:
    /// A/D (or Left/Right, W/S, Up/Down) move a selection cursor between cards,
    /// 1/2/3 pick a card directly, and F (or Enter/Space) confirms the highlighted
    /// card. Runs on unscaled time so it works while <c>Time.timeScale = 0</c>.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class UpgradeChoiceHud : MonoBehaviour
    {
        [SerializeField] UpgradeSystem upgradeSystem;
        [SerializeField] Color panelBackground = new Color(0.02f, 0.04f, 0.07f, 0.82f);
        [SerializeField] Color cardBackground = new Color(0.08f, 0.10f, 0.14f, 0.95f);
        [SerializeField] Color cardSelectedBackground = new Color(0.14f, 0.18f, 0.24f, 1f);
        [SerializeField] Color titleColor = new Color(0.95f, 0.97f, 1f, 1f);
        [SerializeField] Color bodyColor = new Color(0.78f, 0.86f, 0.94f, 1f);

        UIDocument document;
        VisualElement root;
        readonly List<CardBinding> cards = new List<CardBinding>(3);
        Action<UpgradeData> callback;
        bool visible;
        int selectedIndex;
        // Edge-trigger guard: ignore any key already held the frame the panel opens,
        // so the keypress that caused level-up (or a held movement key) cannot
        // bleed through and select an upgrade immediately.
        bool inputArmed;

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

        void Update()
        {
            if (!visible || Keyboard.current == null || cards.Count == 0)
                return;

            if (!inputArmed)
            {
                if (!AnySelectionKeyHeld())
                    inputArmed = true;
                return;
            }

            Keyboard kb = Keyboard.current;

            // Navigation: A/D + Left/Right (and W/S + Up/Down) cycle the highlight.
            if (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame
                || kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame)
            {
                MoveSelection(-1);
                return;
            }
            if (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame
                || kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame)
            {
                MoveSelection(+1);
                return;
            }

            // Direct pick: 1 / 2 / 3 (top row or numpad).
            if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame)
            {
                TrySelectIndex(0);
                return;
            }
            if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame)
            {
                TrySelectIndex(1);
                return;
            }
            if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame)
            {
                TrySelectIndex(2);
                return;
            }

            // Confirm currently highlighted card.
            if (kb.fKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame
                || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
            {
                TrySelectIndex(selectedIndex);
            }
        }

        public void Show(List<UpgradeData> offers, int newLevel, Action<UpgradeData> onChosen)
        {
            callback = onChosen;
            EnsureRoot();
            BuildPanel(offers, newLevel);

            visible = true;
            selectedIndex = 0;
            inputArmed = false;
            ApplySelectionVisuals();
            root.style.display = DisplayStyle.Flex;
        }

        public void Hide()
        {
            if (!visible)
                return;
            visible = false;
            if (root != null)
                root.style.display = DisplayStyle.None;
            callback = null;
        }

        bool AnySelectionKeyHeld()
        {
            Keyboard kb = Keyboard.current;
            return kb.digit1Key.isPressed || kb.numpad1Key.isPressed
                || kb.digit2Key.isPressed || kb.numpad2Key.isPressed
                || kb.digit3Key.isPressed || kb.numpad3Key.isPressed
                || kb.fKey.isPressed || kb.enterKey.isPressed || kb.numpadEnterKey.isPressed
                || kb.spaceKey.isPressed
                || kb.aKey.isPressed || kb.dKey.isPressed
                || kb.wKey.isPressed || kb.sKey.isPressed
                || kb.leftArrowKey.isPressed || kb.rightArrowKey.isPressed
                || kb.upArrowKey.isPressed || kb.downArrowKey.isPressed;
        }

        void MoveSelection(int delta)
        {
            int count = cards.Count;
            selectedIndex = ((selectedIndex + delta) % count + count) % count;
            ApplySelectionVisuals();
        }

        void ApplySelectionVisuals()
        {
            for (int i = 0; i < cards.Count; i++)
            {
                bool isSelected = i == selectedIndex;
                VisualElement c = cards[i].Root;
                c.style.backgroundColor = isSelected ? cardSelectedBackground : cardBackground;
                c.style.scale = new StyleScale(new Scale(isSelected ? new Vector3(1.04f, 1.04f, 1f) : Vector3.one));
            }
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
            // Block mouse interaction entirely — selection is keyboard-only.
            root.pickingMode = PickingMode.Ignore;
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
            container.pickingMode = PickingMode.Ignore;
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
            row.pickingMode = PickingMode.Ignore;
            container.Add(row);

            for (int i = 0; i < offers.Count; i++)
            {
                CardBinding card = BuildCard(offers[i], i + 1);
                row.Add(card.Root);
                cards.Add(card);
            }

            Label footer = new Label("A/D move  •  1/2/3 pick  •  F confirm");
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
            cardRoot.pickingMode = PickingMode.Ignore;
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

            return card;
        }

        void TrySelectIndex(int index)
        {
            if (!visible || index < 0 || index >= cards.Count)
                return;

            selectedIndex = index;
            ApplySelectionVisuals();

            UpgradeData picked = cards[index].Data;
            Action<UpgradeData> cb = callback;
            // Hide() clears callback, so cache first.
            cb?.Invoke(picked);
        }
    }
}
