using UnityEngine;
using UnityEngine.UI;

namespace VRGame.Runtime
{
    public sealed class InventoryCategoryButtonView : MonoBehaviour
    {
        [SerializeField]
        private Button button = null;

        [SerializeField]
        private Image background = null;

        [SerializeField]
        private Text labelText = null;

        [SerializeField]
        private Color normalColor = new Color(0.08f, 0.09f, 0.1f, 0.82f);

        [SerializeField]
        private Color selectedColor = new Color(0.22f, 0.3f, 0.36f, 0.95f);

        private VRInventoryUIController controller;
        private InventoryUiCategory category;

        public void AssignControls(Button newButton, Image newBackground, Text newLabelText)
        {
            button = newButton;
            background = newBackground;
            labelText = newLabelText;
        }

        public void Bind(VRInventoryUIController newController, InventoryUiCategory newCategory, bool selected)
        {
            controller = newController;
            category = newCategory;

            if (labelText != null)
            {
                labelText.text = GetDisplayName(category);
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

        private void Select()
        {
            controller?.SetCategoryFilter(category);
        }

        private void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(Select);
            }
        }

        public static string GetDisplayName(InventoryUiCategory category)
        {
            switch (category)
            {
                case InventoryUiCategory.All:
                    return "All";
                case InventoryUiCategory.Weapons:
                    return "Weapons";
                case InventoryUiCategory.Accessories:
                    return "Accessories";
                case InventoryUiCategory.Placeables:
                    return "Placeables";
                case InventoryUiCategory.Electrical:
                    return "Electrical";
                case InventoryUiCategory.Crafting:
                    return "Crafting";
                case InventoryUiCategory.Favorites:
                    return "Favorites";
                default:
                    return category.ToString();
            }
        }
    }
}
