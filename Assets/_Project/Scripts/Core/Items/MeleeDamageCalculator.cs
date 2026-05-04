using System;
using UnityEngine;

namespace VRGame.Items
{
    public enum MeleeDamageFailureReason
    {
        None,
        MissingItemDefinition,
        MissingMeleeProfile,
        BelowMinimumVelocity
    }

    public readonly struct MeleeDamageCalculationInput
    {
        public MeleeDamageCalculationInput(
            ItemDefinition itemDefinition,
            ItemInstanceState itemInstance,
            ItemAffixDefinitionDatabase affixDefinitionDatabase,
            StatBlock attackerStats,
            string damageZoneId,
            float hitVelocity,
            float random01,
            float velocityDamageScale,
            float maxVelocityMultiplier)
        {
            ItemDefinition = itemDefinition;
            ItemInstance = itemInstance;
            AffixDefinitionDatabase = affixDefinitionDatabase;
            AttackerStats = attackerStats;
            DamageZoneId = StableIdUtility.Normalize(damageZoneId);
            HitVelocity = Mathf.Max(0f, hitVelocity);
            Random01 = Mathf.Clamp01(random01);
            VelocityDamageScale = Mathf.Max(0f, velocityDamageScale);
            MaxVelocityMultiplier = Mathf.Max(1f, maxVelocityMultiplier);
        }

        public ItemDefinition ItemDefinition { get; }

        public ItemInstanceState ItemInstance { get; }

        public ItemAffixDefinitionDatabase AffixDefinitionDatabase { get; }

        public StatBlock AttackerStats { get; }

        public string DamageZoneId { get; }

        public float HitVelocity { get; }

        public float Random01 { get; }

        public float VelocityDamageScale { get; }

        public float MaxVelocityMultiplier { get; }
    }

    public readonly struct MeleeDamageCalculationResult
    {
        public MeleeDamageCalculationResult(
            bool success,
            MeleeDamageFailureReason failureReason,
            float damage,
            bool critical,
            float critChance,
            float knockback,
            float minimumHitVelocity,
            float velocityMultiplier,
            float hitCooldownSeconds,
            float swingSpeed,
            float zoneDamageMultiplier,
            bool trueMelee)
        {
            Success = success;
            FailureReason = failureReason;
            Damage = Mathf.Max(0f, damage);
            Critical = critical;
            CritChance = Mathf.Clamp01(critChance);
            Knockback = Mathf.Max(0f, knockback);
            MinimumHitVelocity = Mathf.Max(0f, minimumHitVelocity);
            VelocityMultiplier = Mathf.Max(0f, velocityMultiplier);
            HitCooldownSeconds = Mathf.Max(0f, hitCooldownSeconds);
            SwingSpeed = Mathf.Max(0f, swingSpeed);
            ZoneDamageMultiplier = Mathf.Max(0f, zoneDamageMultiplier);
            TrueMelee = trueMelee;
        }

        public bool Success { get; }

        public MeleeDamageFailureReason FailureReason { get; }

        public float Damage { get; }

        public bool Critical { get; }

        public float CritChance { get; }

        public float Knockback { get; }

        public float MinimumHitVelocity { get; }

        public float VelocityMultiplier { get; }

        public float HitCooldownSeconds { get; }

        public float SwingSpeed { get; }

        public float ZoneDamageMultiplier { get; }

        public bool TrueMelee { get; }
    }

    public static class MeleeDamageCalculator
    {
        private const float CriticalDamageMultiplier = 2f;

        public static MeleeDamageCalculationResult Calculate(MeleeDamageCalculationInput input)
        {
            ItemDefinition itemDefinition = input.ItemDefinition;
            if (itemDefinition == null)
            {
                return Failure(MeleeDamageFailureReason.MissingItemDefinition);
            }

            MeleeWeaponProfile profile = itemDefinition.MeleeWeaponProfile;
            if (profile == null)
            {
                return Failure(MeleeDamageFailureReason.MissingMeleeProfile);
            }

            MeleeDamageZone damageZone = FindDamageZone(profile, input.DamageZoneId);
            float zoneDamageMultiplier = damageZone != null ? damageZone.DamageMultiplier : 1f;
            float minimumHitVelocity = damageZone != null && damageZone.HasVelocityOverride
                ? damageZone.MinimumHitVelocityOverride
                : profile.MinimumHitVelocity;

            if (input.HitVelocity < minimumHitVelocity)
            {
                return new MeleeDamageCalculationResult(
                    false,
                    MeleeDamageFailureReason.BelowMinimumVelocity,
                    0f,
                    false,
                    0f,
                    0f,
                    minimumHitVelocity,
                    0f,
                    profile.HitCooldownSeconds,
                    profile.SwingSpeed,
                    zoneDamageMultiplier,
                    profile.TrueMelee);
            }

            StatBlock weaponBaseStats = new StatBlock();
            weaponBaseStats.SetValue(StatIds.MeleeDamage, profile.BaseDamage);
            weaponBaseStats.SetValue(StatIds.MeleeCritChance, profile.CritChance);
            weaponBaseStats.SetValue(StatIds.MeleeKnockback, profile.Knockback);
            weaponBaseStats.SetValue(StatIds.MeleeAttackSpeed, profile.SwingSpeed);

            StatBlock weaponStats = new StatBlock();
            StatAggregator.RecalculateItemInstanceStats(
                weaponBaseStats,
                input.ItemInstance,
                itemDefinition,
                input.AffixDefinitionDatabase,
                weaponStats);

            float attackerDamageBonus = input.AttackerStats != null ? input.AttackerStats.GetValue(StatIds.MeleeDamage, 0f) : 0f;
            float attackerCritBonus = input.AttackerStats != null ? input.AttackerStats.GetValue(StatIds.MeleeCritChance, 0f) : 0f;
            float attackerKnockbackBonus = input.AttackerStats != null ? input.AttackerStats.GetValue(StatIds.MeleeKnockback, 0f) : 0f;
            float attackerSpeedBonus = input.AttackerStats != null ? input.AttackerStats.GetValue(StatIds.MeleeAttackSpeed, 0f) : 0f;

            float rawDamage = Mathf.Max(0f, weaponStats.GetValue(StatIds.MeleeDamage, profile.BaseDamage) + attackerDamageBonus);
            float velocityMultiplier = CalculateVelocityMultiplier(input.HitVelocity, minimumHitVelocity, input.VelocityDamageScale, input.MaxVelocityMultiplier);
            float critChance = Mathf.Clamp01(weaponStats.GetValue(StatIds.MeleeCritChance, profile.CritChance) + attackerCritBonus);
            bool critical = input.Random01 < critChance;
            float damage = rawDamage * Mathf.Max(0f, zoneDamageMultiplier) * velocityMultiplier;
            if (critical)
            {
                damage *= CriticalDamageMultiplier;
            }

            float knockback = Mathf.Max(0f, weaponStats.GetValue(StatIds.MeleeKnockback, profile.Knockback) + attackerKnockbackBonus);
            float swingSpeed = Mathf.Max(0.01f, weaponStats.GetValue(StatIds.MeleeAttackSpeed, profile.SwingSpeed) + attackerSpeedBonus);
            float cooldown = profile.HitCooldownSeconds > 0f ? profile.HitCooldownSeconds / swingSpeed : 0f;

            return new MeleeDamageCalculationResult(
                true,
                MeleeDamageFailureReason.None,
                damage,
                critical,
                critChance,
                knockback,
                minimumHitVelocity,
                velocityMultiplier,
                cooldown,
                swingSpeed,
                zoneDamageMultiplier,
                profile.TrueMelee);
        }

        private static MeleeDamageCalculationResult Failure(MeleeDamageFailureReason reason)
        {
            return new MeleeDamageCalculationResult(false, reason, 0f, false, 0f, 0f, 0f, 0f, 0f, 0f, 0f, false);
        }

        private static float CalculateVelocityMultiplier(float hitVelocity, float minimumHitVelocity, float velocityDamageScale, float maxVelocityMultiplier)
        {
            if (velocityDamageScale <= 0f)
            {
                return 1f;
            }

            float aboveThreshold = Math.Max(0f, hitVelocity - minimumHitVelocity);
            return Mathf.Clamp(1f + (aboveThreshold * velocityDamageScale), 1f, maxVelocityMultiplier);
        }

        private static MeleeDamageZone FindDamageZone(MeleeWeaponProfile profile, string damageZoneId)
        {
            if (profile == null || string.IsNullOrEmpty(damageZoneId))
            {
                return null;
            }

            var zones = profile.DamageZones;
            for (int i = 0; i < zones.Count; i++)
            {
                MeleeDamageZone zone = zones[i];
                if (zone != null && StableIdUtility.EqualsNormalized(zone.ZoneId, damageZoneId))
                {
                    return zone;
                }
            }

            return null;
        }
    }
}
