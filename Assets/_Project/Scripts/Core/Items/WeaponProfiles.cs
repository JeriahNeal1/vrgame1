using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRGame.Items
{
    public enum WeaponFamily
    {
        None,
        Melee,
        Magic,
        Summoner,
        Ranged
    }

    [Serializable]
    public sealed class WeaponProfile
    {
        [SerializeField]
        private WeaponFamily family = WeaponFamily.Melee;

        [Tooltip("Held weapons are usable inventory items and do not need to occupy equipment slots.")]
        [SerializeField]
        private bool heldItem = true;

        [Tooltip("Leave false for Terraria-style held weapons unless a future loadout slot explicitly supports this.")]
        [SerializeField]
        private bool occupiesEquipmentSlot = false;

        [SerializeField]
        private List<string> weaponTags = new List<string>();

        public WeaponFamily Family
        {
            get { return family; }
        }

        public bool HeldItem
        {
            get { return heldItem; }
        }

        public bool OccupiesEquipmentSlot
        {
            get { return occupiesEquipmentSlot; }
        }

        public IReadOnlyList<string> WeaponTags
        {
            get { return weaponTags ?? (IReadOnlyList<string>)Array.Empty<string>(); }
        }
    }

    [Serializable]
    public sealed class MeleeWeaponProfile
    {
        [Min(0)]
        [SerializeField]
        private float baseDamage = 1f;

        [Range(0f, 1f)]
        [SerializeField]
        private float critChance = 0f;

        [Min(0f)]
        [SerializeField]
        private float knockback = 0f;

        [Min(0f)]
        [SerializeField]
        private float swingSpeed = 1f;

        [SerializeField]
        private bool trueMelee = false;

        [Min(0f)]
        [SerializeField]
        private float minimumHitVelocity = 1f;

        [Min(0f)]
        [SerializeField]
        private float hitCooldownSeconds = 0.25f;

        [SerializeField]
        private List<MeleeDamageZone> damageZones = new List<MeleeDamageZone>();

        public float BaseDamage
        {
            get { return Mathf.Max(0f, baseDamage); }
        }

        public float CritChance
        {
            get { return Mathf.Clamp01(critChance); }
        }

        public float Knockback
        {
            get { return Mathf.Max(0f, knockback); }
        }

        public float SwingSpeed
        {
            get { return Mathf.Max(0f, swingSpeed); }
        }

        public bool TrueMelee
        {
            get { return trueMelee; }
        }

        public float MinimumHitVelocity
        {
            get { return Mathf.Max(0f, minimumHitVelocity); }
        }

        public float HitCooldownSeconds
        {
            get { return Mathf.Max(0f, hitCooldownSeconds); }
        }

        public IReadOnlyList<MeleeDamageZone> DamageZones
        {
            get { return damageZones ?? (IReadOnlyList<MeleeDamageZone>)Array.Empty<MeleeDamageZone>(); }
        }
    }

    [Serializable]
    public sealed class MeleeDamageZone
    {
        [SerializeField]
        private string zoneId = string.Empty;

        [Min(0f)]
        [SerializeField]
        private float damageMultiplier = 1f;

        [Min(0f)]
        [SerializeField]
        private float minimumHitVelocityOverride = 0f;

        public string ZoneId
        {
            get { return StableIdUtility.Normalize(zoneId); }
        }

        public float DamageMultiplier
        {
            get { return Mathf.Max(0f, damageMultiplier); }
        }

        public float MinimumHitVelocityOverride
        {
            get { return Mathf.Max(0f, minimumHitVelocityOverride); }
        }

        public bool HasVelocityOverride
        {
            get { return minimumHitVelocityOverride > 0f; }
        }
    }
}
