using UnityEngine;
using UnityEngine.UI;

namespace VRGame.Runtime
{
    public sealed class ManifestationPortalBindingView : MonoBehaviour
    {
        [SerializeField]
        private Image iconImage = null;

        [SerializeField]
        private Text selectedNameText = null;

        [SerializeField]
        private Text statusText = null;

        [SerializeField]
        private Button manifestButton = null;

        [SerializeField]
        private string noSelectionText = "No item selected";

        private VRInventoryUIController controller;
        private InventoryUiEntry selectedEntry;

        public void AssignControls(Image newIconImage, Text newSelectedNameText, Text newStatusText, Button newManifestButton)
        {
            iconImage = newIconImage;
            selectedNameText = newSelectedNameText;
            statusText = newStatusText;
            manifestButton = newManifestButton;
        }

        public void Bind(VRInventoryUIController newController)
        {
            controller = newController;
            if (manifestButton != null)
            {
                manifestButton.onClick.RemoveListener(RequestManifest);
                manifestButton.onClick.AddListener(RequestManifest);
            }
        }

        public void SetSelectedEntry(InventoryUiEntry entry)
        {
            selectedEntry = entry;

            if (selectedNameText != null)
            {
                selectedNameText.text = entry != null ? entry.DisplayName : noSelectionText;
            }

            if (statusText != null)
            {
                if (entry == null)
                {
                    statusText.text = "Select an item from the inventory list.";
                }
                else if (entry.CanManifest)
                {
                    statusText.text = "Ready to manifest";
                }
                else
                {
                    statusText.text = "Cannot manifest";
                }
            }

            if (iconImage != null)
            {
                iconImage.sprite = entry != null ? entry.Icon : null;
                iconImage.enabled = iconImage.sprite != null;
            }

            if (manifestButton != null)
            {
                manifestButton.interactable = entry != null && entry.CanManifest;
            }
        }

        public void RequestManifest()
        {
            controller?.RequestManifestSelectedItem();
        }

        private void OnDestroy()
        {
            if (manifestButton != null)
            {
                manifestButton.onClick.RemoveListener(RequestManifest);
            }
        }
    }
}
