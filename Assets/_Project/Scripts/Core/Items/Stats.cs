using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRGame.Items
{
    [Serializable]
    public struct StatId : IEquatable<StatId>
    {
        [SerializeField]
        private string value;

        public StatId(string value)
        {
            this.value = StableIdUtility.Normalize(value);
        }

        public string Value
        {
            get { return StableIdUtility.Normalize(value); }
        }

        public bool IsEmpty
        {
            get { return string.IsNullOrEmpty(Value); }
        }

        public static StatId FromString(string value)
        {
            return new StatId(value);
        }

        public bool Equals(StatId other)
        {
            return StableIdUtility.EqualsNormalized(Value, other.Value);
        }

        public override bool Equals(object obj)
        {
            return obj is StatId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return StableIdUtility.GetNormalizedHashCode(Value);
        }

        public override string ToString()
        {
            return Value;
        }

        public static bool operator ==(StatId left, StatId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(StatId left, StatId right)
        {
            return !left.Equals(right);
        }

        public static implicit operator string(StatId id)
        {
            return id.Value;
        }
    }

    public static class StatIds
    {
        public static readonly StatId HealthMax = new StatId("stat.health_max");
        public static readonly StatId Defense = new StatId("stat.defense");
        public static readonly StatId MeleeDamage = new StatId("stat.melee_damage");
        public static readonly StatId MeleeAttackSpeed = new StatId("stat.melee_attack_speed");
        public static readonly StatId MeleeCritChance = new StatId("stat.melee_crit_chance");
        public static readonly StatId MeleeKnockback = new StatId("stat.melee_knockback");
        public static readonly StatId MagicDamage = new StatId("stat.magic_damage");
        public static readonly StatId RangedDamage = new StatId("stat.ranged_damage");
        public static readonly StatId SummonDamage = new StatId("stat.summon_damage");
        public static readonly StatId MiningPower = new StatId("stat.mining_power");
        public static readonly StatId MiningSpeed = new StatId("stat.mining_speed");
        public static readonly StatId LumberPower = new StatId("stat.lumber_power");
        public static readonly StatId LumberSpeed = new StatId("stat.lumber_speed");
        public static readonly StatId ConstructionPower = new StatId("stat.construction_power");
        public static readonly StatId FishingPower = new StatId("stat.fishing_power");
        public static readonly StatId ToolHardness = new StatId("stat.tool_hardness");
        public static readonly StatId MovementSpeed = new StatId("stat.movement_speed");
        public static readonly StatId BuildRange = new StatId("stat.build_range");
        public static readonly StatId WireRange = new StatId("stat.wire_range");
    }

    public enum StatModifierOperation
    {
        Flat,
        AdditivePercent,
        MultiplicativePercent,
        Override,
        MinClamp,
        MaxClamp
    }

    [Serializable]
    public sealed class StatModifier
    {
        [SerializeField]
        private StatId statId;

        [SerializeField]
        private StatModifierOperation operation = StatModifierOperation.Flat;

        [SerializeField]
        private float value = 0f;

        [SerializeField]
        private string sourceId = string.Empty;

        [SerializeField]
        private int order = 0;

        public StatModifier()
        {
        }

        public StatModifier(StatId statId, StatModifierOperation operation, float value, string sourceId = "", int order = 0)
        {
            this.statId = statId;
            this.operation = operation;
            this.value = value;
            this.sourceId = StableIdUtility.Normalize(sourceId);
            this.order = order;
        }

        public StatId StatId
        {
            get { return statId; }
        }

        public StatModifierOperation Operation
        {
            get { return operation; }
        }

        public float Value
        {
            get { return value; }
        }

        public string SourceId
        {
            get { return StableIdUtility.Normalize(sourceId); }
        }

        public int Order
        {
            get { return order; }
        }

        public bool IsValid
        {
            get { return !statId.IsEmpty; }
        }
    }

    [Serializable]
    public sealed class StatValueRecord
    {
        [SerializeField]
        private StatId statId;

        [SerializeField]
        private float value = 0f;

        public StatValueRecord(StatId statId, float value)
        {
            this.statId = statId;
            this.value = value;
        }

        public StatId StatId
        {
            get { return statId; }
        }

        public float Value
        {
            get { return value; }
        }

        internal void SetValue(float newValue)
        {
            value = newValue;
        }
    }

    [Serializable]
    public sealed class StatBlock
    {
        [SerializeField]
        private List<StatValueRecord> values = new List<StatValueRecord>();

        public IReadOnlyList<StatValueRecord> Values
        {
            get { return values ?? (IReadOnlyList<StatValueRecord>)Array.Empty<StatValueRecord>(); }
        }

        public float GetValue(StatId statId, float fallback = 0f)
        {
            int index = FindIndex(statId);
            return index >= 0 ? values[index].Value : fallback;
        }

        public void SetValue(StatId statId, float value)
        {
            EnsureList();
            if (statId.IsEmpty)
            {
                return;
            }

            int index = FindIndex(statId);
            if (index < 0)
            {
                values.Add(new StatValueRecord(statId, value));
            }
            else
            {
                values[index].SetValue(value);
            }
        }

        public void Clear()
        {
            EnsureList();
            values.Clear();
        }

        public void CopyFrom(StatBlock other)
        {
            Clear();
            if (other == null)
            {
                return;
            }

            IReadOnlyList<StatValueRecord> otherValues = other.Values;
            for (int i = 0; i < otherValues.Count; i++)
            {
                StatValueRecord record = otherValues[i];
                if (record != null && !record.StatId.IsEmpty)
                {
                    SetValue(record.StatId, record.Value);
                }
            }
        }

        internal int FindIndex(StatId statId)
        {
            EnsureList();
            if (statId.IsEmpty)
            {
                return -1;
            }

            for (int i = 0; i < values.Count; i++)
            {
                StatValueRecord record = values[i];
                if (record != null && record.StatId == statId)
                {
                    return i;
                }
            }

            return -1;
        }

        private void EnsureList()
        {
            values ??= new List<StatValueRecord>();
        }
    }

    public interface IItemInstanceStatModifierProvider
    {
        void AddStatModifiers(ItemInstanceState itemInstance, ItemDefinition itemDefinition, IList<StatModifier> modifiers);
    }
}
