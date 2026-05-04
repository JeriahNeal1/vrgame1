using System;
using UnityEngine;
using UnityEngine.Events;
using VRGame.Items;

namespace VRGame.Runtime
{
    public enum DamageType
    {
        Melee,
        TrueMelee
    }

    [Serializable]
    public sealed class DamageContext
    {
        public string AttackerId { get; set; } = string.Empty;

        public GameObject AttackerObject { get; set; }

        public WorldItemIdentity SourceItemIdentity { get; set; }

        public ItemDefinition SourceItemDefinition { get; set; }

        public ItemInstanceState SourceItemInstance { get; set; }

        public ItemDefId ItemDefId { get; set; }

        public ItemInstanceId ItemInstanceId { get; set; }

        public string DamageZoneId { get; set; } = string.Empty;

        public DamageType DamageType { get; set; } = DamageType.Melee;

        public float DamageAmount { get; set; }

        public bool Critical { get; set; }

        public float Knockback { get; set; }

        public Vector3 HitPoint { get; set; }

        public Vector3 HitDirection { get; set; }

        public Vector3 SourceVelocity { get; set; }

        public float HitVelocity { get; set; }

        public float VelocityMultiplier { get; set; } = 1f;

        public float MinimumHitVelocity { get; set; }

        public float HitCooldownSeconds { get; set; }
    }

    public sealed class DamageResult
    {
        public DamageResult(bool accepted, float appliedDamage, string message = "")
        {
            Accepted = accepted;
            AppliedDamage = Mathf.Max(0f, appliedDamage);
            Message = message ?? string.Empty;
        }

        public bool Accepted { get; }

        public float AppliedDamage { get; }

        public string Message { get; }

        public static DamageResult Applied(float damage, string message = "")
        {
            return new DamageResult(true, damage, message);
        }

        public static DamageResult Rejected(string message)
        {
            return new DamageResult(false, 0f, message);
        }
    }

    public interface IDamageable
    {
        string DamageableId { get; }

        bool CanReceiveDamage(DamageContext context);

        DamageResult ApplyDamage(DamageContext context);
    }

    public sealed class MeleeHitActionContext
    {
        public MeleeHitActionContext(DamageContext damageContext, DamageResult damageResult)
        {
            DamageContext = damageContext;
            DamageResult = damageResult;
        }

        public DamageContext DamageContext { get; }

        public DamageResult DamageResult { get; }

        public ItemDefinition ItemDefinition
        {
            get { return DamageContext != null ? DamageContext.SourceItemDefinition : null; }
        }

        public ItemInstanceState ItemInstance
        {
            get { return DamageContext != null ? DamageContext.SourceItemInstance : null; }
        }
    }

    public interface IMeleeHitActionHandler
    {
        void OnMeleeHit(MeleeHitActionContext context);
    }

    [Serializable]
    public sealed class DamageContextEvent : UnityEvent<DamageContext>
    {
    }
}
