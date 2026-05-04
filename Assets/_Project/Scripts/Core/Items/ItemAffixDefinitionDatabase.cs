using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRGame.Items
{
    [CreateAssetMenu(menuName = "VRGame/Items/Item Affix Definition Database", fileName = "ItemAffixDefinitionDatabase")]
    public sealed class ItemAffixDefinitionDatabase : ScriptableObject
    {
        [SerializeField]
        private List<ModifierDefinition> modifierDefinitions = new List<ModifierDefinition>();

        [SerializeField]
        private List<EnchantmentDefinition> enchantmentDefinitions = new List<EnchantmentDefinition>();

        [SerializeField]
        private List<ModifierSetDefinition> modifierSets = new List<ModifierSetDefinition>();

        [SerializeField]
        private List<EnchantmentSetDefinition> enchantmentSets = new List<EnchantmentSetDefinition>();

        [NonSerialized]
        private Dictionary<string, ModifierDefinition> modifiersById;

        [NonSerialized]
        private Dictionary<string, EnchantmentDefinition> enchantmentsById;

        [NonSerialized]
        private Dictionary<string, ModifierSetDefinition> modifierSetsById;

        [NonSerialized]
        private Dictionary<string, EnchantmentSetDefinition> enchantmentSetsById;

        [NonSerialized]
        private bool lookupDirty = true;

        public IReadOnlyList<ModifierDefinition> ModifierDefinitions
        {
            get { return modifierDefinitions ?? (IReadOnlyList<ModifierDefinition>)Array.Empty<ModifierDefinition>(); }
        }

        public IReadOnlyList<EnchantmentDefinition> EnchantmentDefinitions
        {
            get { return enchantmentDefinitions ?? (IReadOnlyList<EnchantmentDefinition>)Array.Empty<EnchantmentDefinition>(); }
        }

        public IReadOnlyList<ModifierSetDefinition> ModifierSets
        {
            get { return modifierSets ?? (IReadOnlyList<ModifierSetDefinition>)Array.Empty<ModifierSetDefinition>(); }
        }

        public IReadOnlyList<EnchantmentSetDefinition> EnchantmentSets
        {
            get { return enchantmentSets ?? (IReadOnlyList<EnchantmentSetDefinition>)Array.Empty<EnchantmentSetDefinition>(); }
        }

        public bool TryGetModifier(ModifierId modifierId, out ModifierDefinition modifierDefinition)
        {
            EnsureLookup();
            if (modifierId.IsEmpty)
            {
                modifierDefinition = null;
                return false;
            }

            return modifiersById.TryGetValue(modifierId.Value, out modifierDefinition);
        }

        public bool TryGetModifier(string modifierId, out ModifierDefinition modifierDefinition)
        {
            return TryGetModifier(ModifierId.FromString(modifierId), out modifierDefinition);
        }

        public bool TryGetEnchantment(EnchantmentId enchantmentId, out EnchantmentDefinition enchantmentDefinition)
        {
            EnsureLookup();
            if (enchantmentId.IsEmpty)
            {
                enchantmentDefinition = null;
                return false;
            }

            return enchantmentsById.TryGetValue(enchantmentId.Value, out enchantmentDefinition);
        }

        public bool TryGetEnchantment(string enchantmentId, out EnchantmentDefinition enchantmentDefinition)
        {
            return TryGetEnchantment(EnchantmentId.FromString(enchantmentId), out enchantmentDefinition);
        }

        public bool TryGetModifierSet(string modifierSetId, out ModifierSetDefinition modifierSetDefinition)
        {
            EnsureLookup();
            string normalizedId = StableIdUtility.Normalize(modifierSetId);
            if (string.IsNullOrEmpty(normalizedId))
            {
                modifierSetDefinition = null;
                return false;
            }

            return modifierSetsById.TryGetValue(normalizedId, out modifierSetDefinition);
        }

        public bool TryGetEnchantmentSet(string enchantmentSetId, out EnchantmentSetDefinition enchantmentSetDefinition)
        {
            EnsureLookup();
            string normalizedId = StableIdUtility.Normalize(enchantmentSetId);
            if (string.IsNullOrEmpty(normalizedId))
            {
                enchantmentSetDefinition = null;
                return false;
            }

            return enchantmentSetsById.TryGetValue(normalizedId, out enchantmentSetDefinition);
        }

        public void AddModifierCandidatesFromReferences(IReadOnlyList<DefinitionIdReference> references, List<ModifierDefinition> target)
        {
            if (target == null || references == null)
            {
                return;
            }

            for (int i = 0; i < references.Count; i++)
            {
                DefinitionIdReference reference = references[i];
                if (!reference.IsValid)
                {
                    continue;
                }

                if (TryGetModifier(reference.Id, out ModifierDefinition modifierDefinition) && modifierDefinition != null)
                {
                    AddUniqueModifier(target, modifierDefinition);
                }

                if (TryGetModifierSet(reference.Id, out ModifierSetDefinition modifierSet) && modifierSet != null)
                {
                    IReadOnlyList<ModifierDefinition> setModifiers = modifierSet.Modifiers;
                    for (int setIndex = 0; setIndex < setModifiers.Count; setIndex++)
                    {
                        AddUniqueModifier(target, setModifiers[setIndex]);
                    }
                }
            }
        }

        public void AddEnchantmentCandidatesFromReferences(IReadOnlyList<DefinitionIdReference> references, List<EnchantmentDefinition> target)
        {
            if (target == null || references == null)
            {
                return;
            }

            for (int i = 0; i < references.Count; i++)
            {
                DefinitionIdReference reference = references[i];
                if (!reference.IsValid)
                {
                    continue;
                }

                if (TryGetEnchantment(reference.Id, out EnchantmentDefinition enchantmentDefinition) && enchantmentDefinition != null)
                {
                    AddUniqueEnchantment(target, enchantmentDefinition);
                }

                if (TryGetEnchantmentSet(reference.Id, out EnchantmentSetDefinition enchantmentSet) && enchantmentSet != null)
                {
                    IReadOnlyList<EnchantmentDefinition> setEnchantments = enchantmentSet.Enchantments;
                    for (int setIndex = 0; setIndex < setEnchantments.Count; setIndex++)
                    {
                        AddUniqueEnchantment(target, setEnchantments[setIndex]);
                    }
                }
            }
        }

        public IReadOnlyList<ItemAffixDefinitionValidationIssue> ValidateDefinitions()
        {
            return ItemAffixDefinitionValidator.ValidateDatabase(this);
        }

        public void RebuildLookup()
        {
            modifiersById = modifiersById ?? new Dictionary<string, ModifierDefinition>(StableIdUtility.Comparer);
            enchantmentsById = enchantmentsById ?? new Dictionary<string, EnchantmentDefinition>(StableIdUtility.Comparer);
            modifierSetsById = modifierSetsById ?? new Dictionary<string, ModifierSetDefinition>(StableIdUtility.Comparer);
            enchantmentSetsById = enchantmentSetsById ?? new Dictionary<string, EnchantmentSetDefinition>(StableIdUtility.Comparer);

            modifiersById.Clear();
            enchantmentsById.Clear();
            modifierSetsById.Clear();
            enchantmentSetsById.Clear();

            IReadOnlyList<ModifierDefinition> modifiers = ModifierDefinitions;
            for (int i = 0; i < modifiers.Count; i++)
            {
                ModifierDefinition modifier = modifiers[i];
                if (modifier != null && !modifier.ModifierId.IsEmpty && !modifiersById.ContainsKey(modifier.ModifierId.Value))
                {
                    modifiersById.Add(modifier.ModifierId.Value, modifier);
                }
            }

            IReadOnlyList<EnchantmentDefinition> enchantments = EnchantmentDefinitions;
            for (int i = 0; i < enchantments.Count; i++)
            {
                EnchantmentDefinition enchantment = enchantments[i];
                if (enchantment != null && !enchantment.EnchantmentId.IsEmpty && !enchantmentsById.ContainsKey(enchantment.EnchantmentId.Value))
                {
                    enchantmentsById.Add(enchantment.EnchantmentId.Value, enchantment);
                }
            }

            IReadOnlyList<ModifierSetDefinition> modifierSetDefinitions = ModifierSets;
            for (int i = 0; i < modifierSetDefinitions.Count; i++)
            {
                ModifierSetDefinition modifierSet = modifierSetDefinitions[i];
                if (modifierSet != null && StableIdUtility.IsValid(modifierSet.ModifierSetId) && !modifierSetsById.ContainsKey(modifierSet.ModifierSetId))
                {
                    modifierSetsById.Add(modifierSet.ModifierSetId, modifierSet);
                }
            }

            IReadOnlyList<EnchantmentSetDefinition> enchantmentSetDefinitions = EnchantmentSets;
            for (int i = 0; i < enchantmentSetDefinitions.Count; i++)
            {
                EnchantmentSetDefinition enchantmentSet = enchantmentSetDefinitions[i];
                if (enchantmentSet != null && StableIdUtility.IsValid(enchantmentSet.EnchantmentSetId) && !enchantmentSetsById.ContainsKey(enchantmentSet.EnchantmentSetId))
                {
                    enchantmentSetsById.Add(enchantmentSet.EnchantmentSetId, enchantmentSet);
                }
            }

            lookupDirty = false;
        }

        private void OnValidate()
        {
            lookupDirty = true;
            modifierDefinitions ??= new List<ModifierDefinition>();
            enchantmentDefinitions ??= new List<EnchantmentDefinition>();
            modifierSets ??= new List<ModifierSetDefinition>();
            enchantmentSets ??= new List<EnchantmentSetDefinition>();
        }

        private void EnsureLookup()
        {
            if (lookupDirty || modifiersById == null || enchantmentsById == null || modifierSetsById == null || enchantmentSetsById == null)
            {
                RebuildLookup();
            }
        }

        private static void AddUniqueModifier(List<ModifierDefinition> target, ModifierDefinition modifierDefinition)
        {
            if (target == null || modifierDefinition == null || modifierDefinition.ModifierId.IsEmpty)
            {
                return;
            }

            for (int i = 0; i < target.Count; i++)
            {
                if (target[i] != null && target[i].ModifierId == modifierDefinition.ModifierId)
                {
                    return;
                }
            }

            target.Add(modifierDefinition);
        }

        private static void AddUniqueEnchantment(List<EnchantmentDefinition> target, EnchantmentDefinition enchantmentDefinition)
        {
            if (target == null || enchantmentDefinition == null || enchantmentDefinition.EnchantmentId.IsEmpty)
            {
                return;
            }

            for (int i = 0; i < target.Count; i++)
            {
                if (target[i] != null && target[i].EnchantmentId == enchantmentDefinition.EnchantmentId)
                {
                    return;
                }
            }

            target.Add(enchantmentDefinition);
        }
    }
}
