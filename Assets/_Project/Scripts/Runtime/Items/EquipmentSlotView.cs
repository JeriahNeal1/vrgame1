using UnityEngine;
using UnityEngine.UI;
using VRGame.Items;

namespace VRGame.Runtime
{
    public sealed class EquipmentSlotView : MonoBehaviour
    {
        [SerializeField]
        private Button button = null;

        [SerializeField]
        private Image background = null;

        [SerializeField]
        private Image iconImage = null;

        [SerializeField]
        private Text slotNameText = null;

        [SerializeField]
        private Text itemNameText = null;

        [SerializeField]
        private Color normalColor = new Color(0.09f, 0.095f, 0.1f, 0.86f);

        [SerializeField]
        private Color validHoverColor = new Color(0.12f, 0.32f, 0.2f, 0.92f);

        [SerializeField]
        private Color invalidHoverColor = new Color(0.35f, 0.12f, 0.12f, 0.92f);

        private VRInventoryUIController controller;
        private EquipmentRuntimeSlot runtimeSlot;
        private ItemDefinition equippedDefinition;

        public string SlotId
        {
            get { return runtimeSlot != null ? runtimeSlot.SlotId : string.Empty; }
        }

        public ItemDefinition EquippedDefinition
        {
            get { return equippedDefinition; }
        }

        public void AssignControls(Button newButton, Image newBackground, Image newIconImage, Text newSlotNameText, Text newItemNameText)
        {
            button = newButton;
            background = newBackground;
            iconImage = newIconImage;
            slotNameText = newSlotNameText;
            itemNameText = newItemNameText;
        }

        public void Bind(
            VRInventoryUIController newController,
            EquipmentRuntimeSlot newRuntimeSlot,
            ItemDefinition newEquippedDefinition,
            ItemInstanceState equippedInstance)
        {
            controller = newController;
            runtimeSlot = newRuntimeSlot;
            equippedDefinition = newEquippedDefinition;

            if (button != null)
            {
                button.onClick.RemoveListener(UnequipSlot);
                button.onClick.AddListener(UnequipSlot);
            }

            if (slotNameText != null)
            {
                slotNameText.text = runtimeSlot != null ? runtimeSlot.DisplayName : "(slot)";
            }

            if (itemNameText != null)
            {
                itemNameText.text = equippedDefinition != null ? equippedDefinition.DisplayName : "Empty";
            }

            if (iconImage != null)
            {
                iconImage.sprite = equippedDefinition != null ? equippedDefinition.GeneratedIcon : null;
                iconImage.enabled = iconImage.sprite != null;
            }

            SetHoverState(null);
        }

        public void NotifyHeldItemHoverSlot(WorldItemView heldItemView)
        {
            InventoryOperationResult result = controller != null
                ? controller.NotifyHeldItemHoverSlot(heldItemView, SlotId)
                : null;

            SetHoverState(result);
        }

        public void NotifyHeldItemReleaseOverSlot(WorldItemView heldItemView)
        {
            controller?.NotifyHeldItemReleaseOverSlot(heldItemView, SlotId);
            SetHoverState(null);
        }

        public void ClearHover()
        {
            SetHoverState(null);
        }

        private void UnequipSlot()
        {
            if (controller != null && equippedDefinition != null && !string.IsNullOrEmpty(SlotId))
            {
                controller.UnequipSlot(SlotId);
            }
        }

        private void SetHoverState(InventoryOperationResult hoverResult)
        {
            if (background == null)
            {
                return;
            }

            if (hoverResult == null)
            {
                background.color = normalColor;
            }
            else
            {
                background.color = hoverResult.Success ? validHoverColor : invalidHoverColor;
            }
        }

        private void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(UnequipSlot);
            }
        }
    }
}
