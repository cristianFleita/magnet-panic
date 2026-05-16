using System.Collections.Generic;
using MagnetPanic.Combat.Upgrades;
using UnityEngine;
using UnityEngine.UIElements;

namespace MagnetPanic.Combat
{
    /// <summary>
    /// Renders the bottom-left "PERMANENT UPGRADES" strip. Listens to
    /// <see cref="UpgradeSystem.OnUpgradeApplied"/> and rebuilds chips from the
    /// system's stack table; never queries gameplay each frame. Binds the
    /// shared HUD UIDocument.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class UpgradeStripHud : MonoBehaviour
    {
        [SerializeField] UpgradeSystem upgradeSystem;
        [SerializeField] UpgradeCatalog catalog;

        UIDocument document;
        VisualElement container;
        Label emptyCaption;

        readonly Dictionary<UpgradeId, ChipBinding> chips = new Dictionary<UpgradeId, ChipBinding>(10);

        struct ChipBinding
        {
            public VisualElement Root;
            public Label Count;
        }

        void Awake()
        {
            document = GetComponent<UIDocument>();
            if (upgradeSystem == null)
                upgradeSystem = FindFirstObjectByType<UpgradeSystem>();
        }

        void OnEnable()
        {
            Build();
            Bind();
            RebuildFromState();
        }

        void OnDisable()
        {
            Unbind();
        }

        public void Bind(UpgradeSystem system)
        {
            Unbind();
            upgradeSystem = system;
            Bind();
            RebuildFromState();
        }

        void Bind()
        {
            if (upgradeSystem != null)
                upgradeSystem.OnUpgradeApplied.AddListener(HandleUpgradeApplied);
        }

        void Unbind()
        {
            if (upgradeSystem != null)
                upgradeSystem.OnUpgradeApplied.RemoveListener(HandleUpgradeApplied);
        }

        void Build()
        {
            if (document == null || document.rootVisualElement == null)
                return;

            VisualElement root = document.rootVisualElement;
            container = root.Q<VisualElement>("upgrade-chip-container");
            emptyCaption = root.Q<Label>("upgrade-empty-caption");

            if (container != null)
                container.Clear();
            chips.Clear();
        }

        void RebuildFromState()
        {
            if (container == null || upgradeSystem == null)
                return;

            container.Clear();
            chips.Clear();

            foreach (var pair in upgradeSystem.Stacks)
            {
                if (pair.Value <= 0)
                    continue;
                AddOrUpdateChip(pair.Key, pair.Value);
            }

            UpdateEmptyCaption();
        }

        void HandleUpgradeApplied(UpgradeData data, int newStacks)
        {
            if (data == null)
                return;
            AddOrUpdateChip(data.id, newStacks);
            UpdateEmptyCaption();
        }

        void AddOrUpdateChip(UpgradeId id, int stacks)
        {
            if (container == null)
                return;

            if (chips.TryGetValue(id, out ChipBinding existing) && existing.Root != null)
            {
                if (existing.Count != null)
                    existing.Count.text = "x" + stacks;
                return;
            }

            UpgradeData data = ResolveUpgradeData(id);
            if (data == null)
                return;

            VisualElement chip = new VisualElement { name = "upgrade-chip-" + id };
            chip.AddToClassList("upgrade-chip");
            chip.AddToClassList("upgrade-chip-" + data.branch.ToString().ToLowerInvariant());
            chip.tooltip = data.displayName;
            chip.pickingMode = PickingMode.Ignore;

            VisualElement icon = new VisualElement();
            icon.AddToClassList("upgrade-icon");
            chip.Add(icon);

            Label iconText = new Label(ResolveAbbreviation(data));
            iconText.AddToClassList("upgrade-icon-text");
            iconText.pickingMode = PickingMode.Ignore;
            icon.Add(iconText);

            Label count = new Label("x" + stacks);
            count.AddToClassList("upgrade-count");
            count.pickingMode = PickingMode.Ignore;
            chip.Add(count);

            container.Add(chip);
            chips[id] = new ChipBinding { Root = chip, Count = count };
        }

        void UpdateEmptyCaption()
        {
            if (emptyCaption == null)
                return;
            emptyCaption.style.display = chips.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        UpgradeData ResolveUpgradeData(UpgradeId id)
        {
            if (catalog != null)
            {
                UpgradeData found = catalog.Find(id);
                if (found != null)
                    return found;
            }

            // Fallback synthetic data so the chip still renders if the catalog
            // reference is missing — uses sensible defaults pulled from UpgradeId.
            return new UpgradeData
            {
                id = id,
                branch = InferBranch(id),
                displayName = id.ToString(),
                maxStacks = 1,
            };
        }

        static UpgradeBranch InferBranch(UpgradeId id) => id switch
        {
            UpgradeId.MagneticReach => UpgradeBranch.Magnetism,
            UpgradeId.QuickCoil => UpgradeBranch.Magnetism,
            UpgradeId.MagneticChain => UpgradeBranch.Magnetism,
            UpgradeId.ScrapCannon => UpgradeBranch.Combat,
            UpgradeId.Railgun => UpgradeBranch.Combat,
            UpgradeId.DeepPockets => UpgradeBranch.Capacity,
            UpgradeId.HeavyLifter => UpgradeBranch.Capacity,
            UpgradeId.MagneticSlide => UpgradeBranch.Mobility,
            UpgradeId.MagneticSlam => UpgradeBranch.Mobility,
            UpgradeId.IronStride => UpgradeBranch.Mobility,
            _ => UpgradeBranch.Magnetism,
        };

        static string ResolveAbbreviation(UpgradeData data) => data.id switch
        {
            UpgradeId.MagneticReach => "MR",
            UpgradeId.QuickCoil => "QC",
            UpgradeId.MagneticChain => "MC",
            UpgradeId.ScrapCannon => "SC",
            UpgradeId.Railgun => "RG",
            UpgradeId.DeepPockets => "DP",
            UpgradeId.HeavyLifter => "HL",
            UpgradeId.MagneticSlide => "SL",
            UpgradeId.MagneticSlam => "SM",
            UpgradeId.IronStride => "IS",
            _ => data.displayName.Length > 0 ? data.displayName[..1] : "?",
        };
    }
}
