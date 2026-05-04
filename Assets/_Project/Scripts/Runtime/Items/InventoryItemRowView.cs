using UnityEngine;
using UnityEngine.UI;

namespace VRGame.Runtime
{
    public sealed class InventoryItemRowView : MonoBehaviour
    {
        [SerializeField]
        private Button button = null;

        [SerializeField]
        private Image background = null;

        [SerializeField]
        private Image iconImage = null;

        [SerializeField]
        private Text nameText = null;

        [SerializeField]
        private Text detailText = null;

        [SerializeField]
        private Color normalColor = new Color(0.11f, 0.12f, 0.13f, 0.82f);

        [SerializeField]
        private Color selectedColor = new Color(0.2f, 0.35f, 0.42f, 0.9f);

        private VRInventoryUIController controller;
        private InventoryUiEntry entry;

        public InventoryUiEntry Entry
        {
            get { return entry; }
        }

        public void AssignControls(Button newButton, Image newBackground, Image newIconImage, Text newNameText, Text newDetailText)
        {
            button = newButton;
            background = newBackground;
            iconImage = newIconImage;
            nameText = newNameText;
            detailText = newDetailText;
        }

        public void Bind(VRInventoryUIController newController, InventoryUiEntry newEntry, bool selected)
        {
            controller = newController;
            entry = newEntry;

            if (nameText != null)
            {
                nameText.text = entry != null ? entry.DisplayName : string.Empty;
            }

            if (detailText != null)
            {
                detailText.text = entry != null ? entry.DetailText : string.Empty;
            }

            if (iconImage != null)
            {
                iconImage.sprite = entry != null ? entry.Icon : null;
                iconImage.enabled = iconImage.sprite != null;
            }

            if (background != null)
            {
                background.color = selected ? selectedColor : normalColor;
            }

            if (button != null)
            {
                button.onClick.RemoveListener(Select);
                button.onClick.AddListener(Select);
            }
        }

        public void Select()
        {
            if (controller != null && entry != null)
            {
                controller.SelectInventoryItem(entry.Selection);
            }
        }

        private void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(Select);
            }
        }
    }
}
