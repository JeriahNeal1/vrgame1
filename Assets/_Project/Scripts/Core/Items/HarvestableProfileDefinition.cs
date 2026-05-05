using UnityEngine;

namespace VRGame.Items
{
    [CreateAssetMenu(menuName = "VRGame/Items/Harvestable Profile", fileName = "HarvestableProfile")]
    public sealed class HarvestableProfileDefinition : ScriptableObject
    {
        [SerializeField]
        private HarvestableProfile profile = new HarvestableProfile();

        public HarvestableProfile Profile
        {
            get
            {
                profile ??= new HarvestableProfile();
                return profile;
            }
        }

        private void OnValidate()
        {
            profile ??= new HarvestableProfile();
        }
    }
}
