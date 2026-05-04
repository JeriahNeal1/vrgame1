using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using VRGame.Items;

namespace VRGame.Runtime
{
    [DisallowMultipleComponent]
    public sealed class VRInventoryUIController : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField]
        private ItemDefinitionDatabase itemDefinitionDatabase = null;

        [SerializeField]
        private EquipmentLoadoutConfig equipmentLoadoutConfig = null;

        [Tooltip("Optional MonoBehaviour that implements IPlayerInventoryStateProvider.")]
        [SerializeField]
        private MonoBehaviour inventoryStateProviderBehaviour = null;

        [Header("Services")]
        [SerializeField]
        private ManifestationPortal manifestationPortal = null;

        [SerializeField]
        private ItemManifestationService manifestationService = null;

        [Header("Panel Roots")]
        [SerializeField]
        private RectTransform categoryListRoot = null;

        [SerializeField]
        private RectTransform inventoryListRoot = null;

        [SerializeField]
        private ManifestationPortalBindingView portalBindingView = null;

        [SerializeField]
        private RectTransform equipmentSlotRoot = null;

        [Header("Optional Row Prefabs")]
        [SerializeField]
        private InventoryCategoryButtonView categoryButtonPrefab = null;

        [SerializeField]
        private InventoryItemRowView inventoryItemRowPrefab = null;

        [SerializeField]
        private EquipmentSlotView equipmentSlotPrefab = null;

        [Header("Behavior")]
        [SerializeField]
        private InventoryUiCategory activeCategory = InventoryUiCategory.All;

        [SerializeField]
        private string defaultHandId = "right";

        [SerializeField]
        private bool autoCreateMissingUi = true;

        [SerializeField]
        private bool enforceWorldSpaceCanvas = true;

        [SerializeField]
        private bool refreshOnEnable = true;

        [SerializeField]
        private bool verboseLogs = false;

        private readonly List<InventoryUiEntry> visibleEntries = new List<InventoryUiEntry>();
        private readonly List<EquipmentSlotView> equipmentSlotViews = new List<EquipmentSlotView>();
        private IPlayerInventoryStateProvider inventoryStateProvider;
        private IPlayerInventoryStateProvider runtimeInventoryStateProvider;
        private InventoryItemSelection selectedItem = InventoryItemSelection.None;
        private InventoryUiEntry selectedEntry;

        public InventoryItemSelection SelectedItem
        {
            get { return selectedItem; }
        }

        public InventoryUiCategory ActiveCategory
        {
            get { return activeCategory; }
        }

        public int DisplayedInventoryItemCount
        {
            get { return visibleEntries.Count; }
        }

        public int DisplayedEquipmentSlotCount
        {
            get { return equipmentSlotViews.Count; }
        }

        public void BindRuntime(
            IPlayerInventoryStateProvider newInventoryStateProvider,
            ItemDefinitionDatabase newItemDefinitionDatabase,
            EquipmentLoadoutConfig newEquipmentLoadoutConfig,
            ItemManifestationService newManifestationService,
            ManifestationPortal newManifestationPortal)
        {
            runtimeInventoryStateProvider = newInventoryStateProvider;
            inventoryStateProvider = newInventoryStateProvider;
            itemDefinitionDatabase = newItemDefinitionDatabase;
            equipmentLoadoutConfig = newEquipmentLoadoutConfig;
            manifestationService = newManifestationService;
            manifestationPortal = newManifestationPortal;

            if (manifestationPortal != null)
            {
                manifestationPortal.BindRuntime(manifestationService, itemDefinitionDatabase, inventoryStateProvider);
            }

            RefreshAll();
        }

        public void SetCategoryFilter(InventoryUiCategory category)
        {
            activeCategory = category;
            RefreshInventoryList();
            RefreshCategoryButtons();
        }

        public void SelectInventoryItemByIndex(int visibleEntryIndex)
        {
            if (visibleEntryIndex < 0 || visibleEntryIndex >= visibleEntries.Count)
            {
                return;
            }

            SelectInventoryItem(visibleEntries[visibleEntryIndex].Selection);
        }

        public void SelectStack(string itemDefId)
        {
            SelectInventoryItem(InventoryItemSelection.ForStack(ItemDefId.FromString(itemDefId), StackQuantity.One));
        }

        public void SelectItemInstance(string itemInstanceId)
        {
            ItemInstanceId instanceId = ItemInstanceId.FromString(itemInstanceId);
            if (TryGetInventoryState(out PlayerInventoryState inventoryState) &&
                inventoryState.TryGetInstance(instanceId, out ItemInstanceState instance) &&
                instance != null)
            {
                SelectInventoryItem(InventoryItemSelection.ForItemInstance(instance.ItemDefId, instanceId));
            }
        }

        public void SelectInventoryItem(InventoryItemSelection selection)
        {
            selectedItem = selection;
            selectedEntry = FindEntry(selection);

            if (manifestationPortal != null)
            {
                if (selection.IsStack)
                {
                    manifestationPortal.SelectStack(selection.ItemDefId);
                }
                else if (selection.IsItemInstance)
                {
                    manifestationPortal.SelectItemInstance(selection.ItemInstanceId);
                }
            }

            if (portalBindingView != null)
            {
                portalBindingView.SetSelectedEntry(selectedEntry);
            }

            RefreshInventoryList();
            Log($"Selected inventory item: {selection.Kind} {selection.ItemDefId} {selection.ItemInstanceId}");
        }

        public ItemManifestationResult RequestManifestSelectedItem()
        {
            return RequestManifestSelectedItem(defaultHandId);
        }

        public ItemManifestationResult RequestManifestSelectedItem(string requestedHandId)
        {
            if (manifestationPortal == null)
            {
                return new ItemManifestationResult(InventoryOperationResult.Failed(
                    selectedItem.IsItemInstance ? InventoryOperationType.ManifestItemInstance : InventoryOperationType.ManifestStack,
                    InventoryFailureReason.InvalidManifestationRequest,
                    "Inventory UI has no manifestation portal."));
            }

            if (!selectedItem.IsValid)
            {
                return new ItemManifestationResult(InventoryOperationResult.Failed(
                    InventoryOperationType.ManifestStack,
                    InventoryFailureReason.InvalidManifestationRequest,
                    "No inventory item is selected."));
            }

            if (manifestationPortal != null)
            {
                manifestationPortal.BindRuntime(manifestationService, itemDefinitionDatabase, ResolveInventoryProvider());
            }

            ItemManifestationResult result = manifestationPortal.RequestManifestSelectedItem(requestedHandId);
            RefreshAll();
            return result;
        }

        public InventoryOperationResult NotifyHeldItemHoverSlot(WorldItemView heldItemView, string slotId)
        {
            return CanEquipWorldItem(heldItemView, slotId);
        }

        public InventoryOperationResult NotifyHeldItemHoverSlot(WorldItemView heldItemView, EquipmentSlotView slotView)
        {
            return NotifyHeldItemHoverSlot(heldItemView, slotView != null ? slotView.SlotId : string.Empty);
        }

        public InventoryOperationResult NotifyHeldItemReleaseOverSlot(WorldItemView heldItemView, string slotId)
        {
            InventoryOperationResult validation = CanEquipWorldItem(heldItemView, slotId);
            if (!validation.Success)
            {
                Log($"Rejected held item release over '{slotId}': {validation.FailureReason} {validation.Message}");
                return validation;
            }

            WorldItemIdentity identity = heldItemView.Identity;
            PlayerInventoryState inventoryState = ResolveInventoryProvider().InventoryState;
            InventoryOperationResult equipResult = EquipmentService.Equip(
                inventoryState,
                itemDefinitionDatabase,
                equipmentLoadoutConfig,
                identity.ItemInstanceId,
                slotId);

            if (!equipResult.Success)
            {
                Log($"Equip failed after hover validation: {equipResult.FailureReason} {equipResult.Message}");
                return equipResult;
            }

            string requestId = identity.ManifestationRequestId;
            if (manifestationService != null && !string.IsNullOrEmpty(requestId))
            {
                InventoryOperationResult commitResult = manifestationService.CommitManifestedItemAsEquipped(inventoryState, requestId);
                if (!commitResult.Success)
                {
                    Debug.LogWarning($"Equipped item but failed to commit manifestation '{requestId}': {commitResult.Message}", this);
                }
            }
            else
            {
                DestroyWorldItem(heldItemView);
            }

            RefreshAll();
            return equipResult;
        }

        public InventoryOperationResult NotifyHeldItemReleaseOverSlot(WorldItemView heldItemView, EquipmentSlotView slotView)
        {
            return NotifyHeldItemReleaseOverSlot(heldItemView, slotView != null ? slotView.SlotId : string.Empty);
        }

        public InventoryOperationResult UnequipSlot(string slotId)
        {
            if (!TryGetInventoryState(out PlayerInventoryState inventoryState))
            {
                return InventoryOperationResult.Failed(InventoryOperationType.Unequip, InventoryFailureReason.InvalidInventoryState, "Inventory state is unavailable.");
            }

            InventoryOperationResult result = EquipmentService.Unequip(inventoryState, equipmentLoadoutConfig, slotId, out _);
            RefreshAll();
            return result;
        }

        public bool TryGetEquipmentSlotView(string slotId, out EquipmentSlotView slotView)
        {
            for (int i = 0; i < equipmentSlotViews.Count; i++)
            {
                EquipmentSlotView view = equipmentSlotViews[i];
                if (view != null && StableIdUtility.EqualsNormalized(view.SlotId, slotId))
                {
                    slotView = view;
                    return true;
                }
            }

            slotView = null;
            return false;
        }

        public bool TryGetVisibleEntry(int index, out InventoryUiEntry entry)
        {
            if (index >= 0 && index < visibleEntries.Count)
            {
                entry = visibleEntries[index];
                return true;
            }

            entry = null;
            return false;
        }

        public void RefreshAll()
        {
            ResolveInventoryProvider();
            EnsureUi();
            RefreshCategoryButtons();
            RefreshInventoryList();
            RefreshEquipmentPanel();
            RefreshPortalBinding();
        }

        public void RefreshInventoryList()
        {
            EnsureUi();
            visibleEntries.Clear();
            CollectVisibleEntries(visibleEntries);
            selectedEntry = selectedItem.IsValid ? FindEntry(selectedItem) : null;
            RebuildInventoryRows();
            RefreshPortalBinding();
        }

        public void RefreshEquipmentPanel()
        {
            EnsureUi();
            RebuildEquipmentSlots();
        }

        private void Awake()
        {
            ResolveInventoryProvider();
            if (manifestationService == null)
            {
                manifestationService = GetComponent<ItemManifestationService>();
            }

            if (manifestationPortal == null)
            {
                manifestationPortal = GetComponent<ManifestationPortal>();
            }
        }

        private void OnEnable()
        {
            if (refreshOnEnable)
            {
                RefreshAll();
            }
        }

        private void OnValidate()
        {
            if (manifestationService == null)
            {
                manifestationService = GetComponent<ItemManifestationService>();
            }

            if (manifestationPortal == null)
            {
                manifestationPortal = GetComponent<ManifestationPortal>();
            }
        }

        private IPlayerInventoryStateProvider ResolveInventoryProvider()
        {
            if (runtimeInventoryStateProvider != null)
            {
                inventoryStateProvider = runtimeInventoryStateProvider;
                return inventoryStateProvider;
            }

            if (inventoryStateProvider != null)
            {
                return inventoryStateProvider;
            }

            inventoryStateProvider = inventoryStateProviderBehaviour as IPlayerInventoryStateProvider;
            if (inventoryStateProvider == null && inventoryStateProviderBehaviour == null)
            {
                inventoryStateProvider = GetComponentInParent<IPlayerInventoryStateProvider>();
            }

            return inventoryStateProvider;
        }

        private bool TryGetInventoryState(out PlayerInventoryState inventoryState)
        {
            IPlayerInventoryStateProvider provider = ResolveInventoryProvider();
            inventoryState = provider != null ? provider.InventoryState : null;
            return inventoryState != null;
        }

        private void CollectVisibleEntries(List<InventoryUiEntry> target)
        {
            if (target == null ||
                itemDefinitionDatabase == null ||
                !TryGetInventoryState(out PlayerInventoryState inventoryState))
            {
                return;
            }

            IReadOnlyList<InventoryStackRecord> stacks = inventoryState.StackLedger;
            for (int i = 0; i < stacks.Count; i++)
            {
                InventoryStackRecord stack = stacks[i];
                if (stack == null || stack.ItemDefId.IsEmpty || !stack.Quantity.IsPositive)
                {
                    continue;
                }

                if (!itemDefinitionDatabase.TryGet(stack.ItemDefId, out ItemDefinition definition) || definition == null)
                {
                    continue;
                }

                if (!MatchesCategory(definition, activeCategory))
                {
                    continue;
                }

                target.Add(new InventoryUiEntry
                {
                    Selection = InventoryItemSelection.ForStack(stack.ItemDefId, stack.Quantity),
                    ItemDefinition = definition,
                    Quantity = stack.Quantity
                });
            }

            IReadOnlyList<ItemInstanceState> instances = inventoryState.ItemInstances;
            for (int i = 0; i < instances.Count; i++)
            {
                ItemInstanceState instance = instances[i];
                if (instance == null ||
                    instance.ItemInstanceId.IsEmpty ||
                    instance.LifecycleState != ItemLifecycleState.InInventory)
                {
                    continue;
                }

                if (!itemDefinitionDatabase.TryGet(instance.ItemDefId, out ItemDefinition definition) || definition == null)
                {
                    continue;
                }

                if (!MatchesCategory(definition, activeCategory))
                {
                    continue;
                }

                target.Add(new InventoryUiEntry
                {
                    Selection = InventoryItemSelection.ForItemInstance(instance.ItemDefId, instance.ItemInstanceId),
                    ItemDefinition = definition,
                    ItemInstance = instance,
                    Quantity = StackQuantity.One
                });
            }
        }

        private InventoryUiEntry FindEntry(InventoryItemSelection selection)
        {
            List<InventoryUiEntry> entries = new List<InventoryUiEntry>();
            InventoryUiCategory previous = activeCategory;
            activeCategory = InventoryUiCategory.All;
            CollectVisibleEntries(entries);
            activeCategory = previous;

            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Selection == selection)
                {
                    return entries[i];
                }
            }

            return null;
        }

        private InventoryOperationResult CanEquipWorldItem(WorldItemView heldItemView, string slotId)
        {
            if (heldItemView == null || heldItemView.Identity == null)
            {
                return InventoryOperationResult.Failed(InventoryOperationType.CanEquip, InventoryFailureReason.InvalidWorldItem, "Held world item is null.");
            }

            WorldItemIdentity identity = heldItemView.Identity;
            if (identity.ItemInstanceId.IsEmpty)
            {
                return InventoryOperationResult.Failed(InventoryOperationType.CanEquip, InventoryFailureReason.ItemMustBeEquipment, "Only unstackable equipment item instances can be equipped.");
            }

            if (!TryGetInventoryState(out PlayerInventoryState inventoryState))
            {
                return InventoryOperationResult.Failed(InventoryOperationType.CanEquip, InventoryFailureReason.InvalidInventoryState, "Inventory state is unavailable.");
            }

            return EquipmentService.CanEquip(
                inventoryState,
                itemDefinitionDatabase,
                equipmentLoadoutConfig,
                identity.ItemInstanceId,
                slotId);
        }

        private void RefreshCategoryButtons()
        {
            EnsureUi();
            if (categoryListRoot == null)
            {
                return;
            }

            ClearChildren(categoryListRoot);
            Array categories = Enum.GetValues(typeof(InventoryUiCategory));
            for (int i = 0; i < categories.Length; i++)
            {
                InventoryUiCategory category = (InventoryUiCategory)categories.GetValue(i);
                InventoryCategoryButtonView view = categoryButtonPrefab != null
                    ? Instantiate(categoryButtonPrefab, categoryListRoot)
                    : CreateDefaultCategoryButton(categoryListRoot);
                view.Bind(this, category, category == activeCategory);
            }
        }

        private void RebuildInventoryRows()
        {
            if (inventoryListRoot == null)
            {
                return;
            }

            ClearChildren(inventoryListRoot);
            for (int i = 0; i < visibleEntries.Count; i++)
            {
                InventoryUiEntry entry = visibleEntries[i];
                InventoryItemRowView row = inventoryItemRowPrefab != null
                    ? Instantiate(inventoryItemRowPrefab, inventoryListRoot)
                    : CreateDefaultInventoryRow(inventoryListRoot);
                row.Bind(this, entry, entry.Selection == selectedItem);
            }
        }

        private void RebuildEquipmentSlots()
        {
            if (equipmentSlotRoot == null)
            {
                return;
            }

            equipmentSlotViews.Clear();
            ClearChildren(equipmentSlotRoot);

            if (equipmentLoadoutConfig == null ||
                itemDefinitionDatabase == null ||
                !TryGetInventoryState(out PlayerInventoryState inventoryState))
            {
                return;
            }

            List<EquipmentRuntimeSlot> slots = equipmentLoadoutConfig.BuildRuntimeSlots();
            for (int i = 0; i < slots.Count; i++)
            {
                EquipmentRuntimeSlot slot = slots[i];
                ItemDefinition equippedDefinition = null;
                ItemInstanceState equippedInstance = null;
                if (inventoryState.EquipmentLoadout.TryGetEquippedItem(slot.SlotId, out ItemInstanceId equippedItemId) &&
                    inventoryState.TryGetInstance(equippedItemId, out equippedInstance) &&
                    equippedInstance != null)
                {
                    itemDefinitionDatabase.TryGet(equippedInstance.ItemDefId, out equippedDefinition);
                }

                EquipmentSlotView slotView = equipmentSlotPrefab != null
                    ? Instantiate(equipmentSlotPrefab, equipmentSlotRoot)
                    : CreateDefaultEquipmentSlot(equipmentSlotRoot);
                slotView.Bind(this, slot, equippedDefinition, equippedInstance);
                equipmentSlotViews.Add(slotView);
            }
        }

        private void RefreshPortalBinding()
        {
            if (portalBindingView != null)
            {
                portalBindingView.Bind(this);
                portalBindingView.SetSelectedEntry(selectedEntry);
            }
        }

        private void EnsureUi()
        {
            if (!autoCreateMissingUi)
            {
                return;
            }

            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.GetComponent<Canvas>();
                if (canvas == null)
                {
                    canvas = gameObject.AddComponent<Canvas>();
                }
            }

            if (enforceWorldSpaceCanvas)
            {
                canvas.renderMode = RenderMode.WorldSpace;
            }

            if (canvas.GetComponent<GraphicRaycaster>() == null)
            {
                canvas.gameObject.AddComponent<GraphicRaycaster>();
            }

            if (canvas.GetComponent<CanvasScaler>() == null)
            {
                CanvasScaler scaler = canvas.gameObject.AddComponent<CanvasScaler>();
                scaler.dynamicPixelsPerUnit = 10f;
            }

            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            if (canvasRect != null)
            {
                canvasRect.sizeDelta = new Vector2(1200f, 720f);
            }

            RectTransform leftPanel = null;
            RectTransform middlePanel = null;
            RectTransform rightPanel = null;

            if (categoryListRoot == null || inventoryListRoot == null)
            {
                leftPanel = FindOrCreatePanel(canvas.transform, "Left Inventory Panel", new Vector2(-410f, 0f), new Vector2(360f, 650f));
                categoryListRoot ??= FindOrCreatePanel(leftPanel, "Categories", new Vector2(0f, 210f), new Vector2(330f, 180f));
                inventoryListRoot ??= FindOrCreatePanel(leftPanel, "Inventory List", new Vector2(0f, -90f), new Vector2(330f, 410f));
            }

            if (portalBindingView == null)
            {
                middlePanel = FindOrCreatePanel(canvas.transform, "Manifestation Portal Panel", new Vector2(0f, 0f), new Vector2(330f, 650f));
                portalBindingView = CreateDefaultPortalBinding(middlePanel);
            }

            if (equipmentSlotRoot == null)
            {
                rightPanel = FindOrCreatePanel(canvas.transform, "Equipment Panel", new Vector2(410f, 0f), new Vector2(360f, 650f));
                equipmentSlotRoot = FindOrCreatePanel(rightPanel, "Equipment Slots", Vector2.zero, new Vector2(330f, 600f));
            }

            ConfigureVerticalLayout(categoryListRoot, 4f);
            ConfigureVerticalLayout(inventoryListRoot, 5f);
            ConfigureVerticalLayout(equipmentSlotRoot, 5f);
        }

        private static RectTransform FindOrCreatePanel(Transform parent, string panelName, Vector2 anchoredPosition, Vector2 size)
        {
            Transform existing = parent.Find(panelName);
            GameObject panelObject = existing != null ? existing.gameObject : new GameObject(panelName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rectTransform = panelObject.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = size;

            Image image = panelObject.GetComponent<Image>();
            if (image != null)
            {
                image.color = new Color(0.055f, 0.06f, 0.065f, 0.78f);
            }

            return rectTransform;
        }

        private static void ConfigureVerticalLayout(RectTransform root, float spacing)
        {
            if (root == null)
            {
                return;
            }

            VerticalLayoutGroup layout = root.GetComponent<VerticalLayoutGroup>();
            if (layout == null)
            {
                layout = root.gameObject.AddComponent<VerticalLayoutGroup>();
            }

            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.spacing = spacing;
            layout.padding = new RectOffset(6, 6, 6, 6);
        }

        private static InventoryCategoryButtonView CreateDefaultCategoryButton(RectTransform parent)
        {
            GameObject row = CreateUiBox("Category", parent, new Vector2(0f, 28f));
            Button button = row.AddComponent<Button>();
            Image background = row.GetComponent<Image>();
            button.targetGraphic = background;
            Text label = CreateText("Label", row.transform, 13, TextAnchor.MiddleLeft);
            InventoryCategoryButtonView view = row.AddComponent<InventoryCategoryButtonView>();
            view.AssignControls(button, background, label);
            return view;
        }

        private static InventoryItemRowView CreateDefaultInventoryRow(RectTransform parent)
        {
            GameObject row = CreateUiBox("Inventory Item", parent, new Vector2(0f, 40f));
            Button button = row.AddComponent<Button>();
            Image background = row.GetComponent<Image>();
            button.targetGraphic = background;

            Image icon = CreateIcon("Icon", row.transform, new Vector2(-145f, 0f), new Vector2(28f, 28f));
            Text name = CreateText("Name", row.transform, 13, TextAnchor.MiddleLeft);
            SetRect(name.rectTransform, new Vector2(-35f, 7f), new Vector2(190f, 18f));
            Text detail = CreateText("Detail", row.transform, 11, TextAnchor.MiddleRight);
            SetRect(detail.rectTransform, new Vector2(100f, -10f), new Vector2(90f, 16f));

            InventoryItemRowView view = row.AddComponent<InventoryItemRowView>();
            view.AssignControls(button, background, icon, name, detail);
            return view;
        }

        private static EquipmentSlotView CreateDefaultEquipmentSlot(RectTransform parent)
        {
            GameObject row = CreateUiBox("Equipment Slot", parent, new Vector2(0f, 42f));
            Button button = row.AddComponent<Button>();
            Image background = row.GetComponent<Image>();
            button.targetGraphic = background;

            Image icon = CreateIcon("Icon", row.transform, new Vector2(-145f, 0f), new Vector2(28f, 28f));
            Text slot = CreateText("Slot", row.transform, 12, TextAnchor.MiddleLeft);
            SetRect(slot.rectTransform, new Vector2(-40f, 8f), new Vector2(185f, 18f));
            Text item = CreateText("Item", row.transform, 11, TextAnchor.MiddleLeft);
            SetRect(item.rectTransform, new Vector2(-40f, -10f), new Vector2(185f, 16f));

            EquipmentSlotView view = row.AddComponent<EquipmentSlotView>();
            view.AssignControls(button, background, icon, slot, item);
            return view;
        }

        private static ManifestationPortalBindingView CreateDefaultPortalBinding(RectTransform parent)
        {
            GameObject root = CreateUiBox("Portal Binding", parent, new Vector2(0f, 180f));
            SetRect(root.GetComponent<RectTransform>(), Vector2.zero, new Vector2(300f, 220f));
            Image icon = CreateIcon("Selected Icon", root.transform, new Vector2(0f, 50f), new Vector2(70f, 70f));
            Text name = CreateText("Selected Name", root.transform, 16, TextAnchor.MiddleCenter);
            SetRect(name.rectTransform, new Vector2(0f, 5f), new Vector2(280f, 28f));
            Text status = CreateText("Status", root.transform, 12, TextAnchor.MiddleCenter);
            SetRect(status.rectTransform, new Vector2(0f, -26f), new Vector2(280f, 22f));
            GameObject buttonObject = CreateUiBox("Manifest Button", root.GetComponent<RectTransform>(), new Vector2(0f, 34f));
            SetRect(buttonObject.GetComponent<RectTransform>(), new Vector2(0f, -70f), new Vector2(180f, 34f));
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = buttonObject.GetComponent<Image>();
            Text buttonText = CreateText("Label", buttonObject.transform, 13, TextAnchor.MiddleCenter);
            buttonText.text = "Manifest";

            ManifestationPortalBindingView view = root.AddComponent<ManifestationPortalBindingView>();
            view.AssignControls(icon, name, status, button);
            return view;
        }

        private static GameObject CreateUiBox(string objectName, Transform parent, Vector2 size)
        {
            GameObject gameObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
            RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            rectTransform.sizeDelta = size;
            LayoutElement layout = gameObject.GetComponent<LayoutElement>();
            layout.minHeight = size.y;
            layout.preferredHeight = size.y;
            Image image = gameObject.GetComponent<Image>();
            image.color = new Color(0.1f, 0.11f, 0.12f, 0.84f);
            return gameObject;
        }

        private static Text CreateText(string objectName, Transform parent, int fontSize, TextAnchor alignment)
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            RectTransform rectTransform = textObject.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = new Vector2(280f, 20f);

            Text text = textObject.GetComponent<Text>();
            text.font = GetBuiltInFont();
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = new Color(0.93f, 0.95f, 0.96f, 1f);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static Image CreateIcon(string objectName, Transform parent, Vector2 anchoredPosition, Vector2 size)
        {
            GameObject iconObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconObject.transform.SetParent(parent, false);
            Image image = iconObject.GetComponent<Image>();
            image.color = Color.white;
            image.preserveAspect = true;
            SetRect(iconObject.GetComponent<RectTransform>(), anchoredPosition, size);
            return image;
        }

        private static void SetRect(RectTransform rectTransform, Vector2 anchoredPosition, Vector2 size)
        {
            if (rectTransform == null)
            {
                return;
            }

            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = size;
        }

        private static Font GetBuiltInFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            return font;
        }

        private static void ClearChildren(RectTransform root)
        {
            if (root == null)
            {
                return;
            }

            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform child = root.GetChild(i);
                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }

        private static bool MatchesCategory(ItemDefinition itemDefinition, InventoryUiCategory category)
        {
            if (itemDefinition == null)
            {
                return false;
            }

            switch (category)
            {
                case InventoryUiCategory.All:
                    return true;
                case InventoryUiCategory.Equipment:
                    return itemDefinition.HasFlag(ItemFlags.Equipment);
                case InventoryUiCategory.Weapons:
                    return itemDefinition.HasFlag(ItemFlags.Weapon);
                case InventoryUiCategory.Armor:
                    return itemDefinition.HasFlag(ItemFlags.Armor);
                case InventoryUiCategory.Accessories:
                    return itemDefinition.HasFlag(ItemFlags.Accessory);
                case InventoryUiCategory.Tools:
                    return itemDefinition.HasFlag(ItemFlags.Tool);
                case InventoryUiCategory.Resources:
                    return itemDefinition.HasFlag(ItemFlags.Resource);
                case InventoryUiCategory.Placeables:
                    return itemDefinition.HasFlag(ItemFlags.Placeable);
                case InventoryUiCategory.Electrical:
                    return itemDefinition.HasFlag(ItemFlags.Electrical);
                case InventoryUiCategory.Crafting:
                    return itemDefinition.HasFlag(ItemFlags.Material) || itemDefinition.HasFlag(ItemFlags.CanBeCrafted);
                case InventoryUiCategory.Favorites:
                    return false;
                default:
                    return true;
            }
        }

        private void DestroyWorldItem(WorldItemView worldItemView)
        {
            if (worldItemView == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(worldItemView.gameObject);
            }
            else
            {
                DestroyImmediate(worldItemView.gameObject);
            }
        }

        private void Log(string message)
        {
            if (verboseLogs)
            {
                Debug.Log(message, this);
            }
        }
    }
}
