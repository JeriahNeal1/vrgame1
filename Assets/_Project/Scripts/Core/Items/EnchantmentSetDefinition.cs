using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRGame.Items
{
    [CreateAssetMenu(menuName = "VRGame/Items/Enchantment Set Definition", fileName = "EnchantmentSetDefinition")]
    public sealed class EnchantmentSetDefinition : ScriptableObject
    {
        [SerializeField]
        private string enchantmentSetId = string.Empty;

        [SerializeField]
        private string displayName = string.Empty;

        [TextArea]
        [SerializeField]
        private string description = string.Empty;

        [SerializeField]
        private List<EnchantmentDefinition> enchantments = new List<EnchantmentDefinition>();

        public string EnchantmentSetId
        {
            get { return StableIdUtility.Normalize(enchantmentSetId); }
        }

        public string DisplayName
        {
            get { return string.IsNullOrWhiteSpace(displayName) ? name : displayName.Trim(); }
        }

        public string Description
        {
            get { return description ?? string.Empty; }
        }

        public IReadOnlyList<EnchantmentDefinition> Enchantments
        {
            get { return enchantments ?? (IReadOnlyList<EnchantmentDefinition>)Array.Empty<EnchantmentDefinition>(); }
        }

        private void OnValidate()
        {
            enchantments ??= new List<EnchantmentDefinition>();
        }
    }
}
