using UnityEngine;

namespace VRGame.Runtime
{
    [DisallowMultipleComponent]
    public sealed class MeleeDamageDummy : MonoBehaviour, IDamageable
    {
        [SerializeField]
        private string damageableId = string.Empty;

        [SerializeField]
        private float maxHealth = 100f;

        [SerializeField]
        private bool resetHealthOnEnable = true;

        [SerializeField]
        private bool destroyAtZeroHealth = false;

        [SerializeField]
        private DamageContextEvent onDamaged = new DamageContextEvent();

        [SerializeField]
        private DamageContextEvent onKilled = new DamageContextEvent();

        private float currentHealth;
        private int receivedHitCount;
        private DamageContext lastDamageContext;

        public string DamageableId
        {
            get { return string.IsNullOrWhiteSpace(damageableId) ? System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this).ToString(System.Globalization.CultureInfo.InvariantCulture) : damageableId.Trim(); }
        }

        public float MaxHealth
        {
            get { return Mathf.Max(1f, maxHealth); }
        }

        public float CurrentHealth
        {
            get { return Mathf.Clamp(currentHealth, 0f, MaxHealth); }
        }

        public int ReceivedHitCount
        {
            get { return receivedHitCount; }
        }

        public DamageContext LastDamageContext
        {
            get { return lastDamageContext; }
        }

        public bool CanReceiveDamage(DamageContext context)
        {
            return CurrentHealth > 0f && context != null && context.DamageAmount > 0f;
        }

        public DamageResult ApplyDamage(DamageContext context)
        {
            if (!CanReceiveDamage(context))
            {
                return DamageResult.Rejected("Damage dummy cannot receive this damage context.");
            }

            float appliedDamage = Mathf.Min(CurrentHealth, Mathf.Max(0f, context.DamageAmount));
            currentHealth = Mathf.Max(0f, CurrentHealth - appliedDamage);
            receivedHitCount++;
            lastDamageContext = context;
            onDamaged.Invoke(context);

            if (CurrentHealth <= 0f)
            {
                onKilled.Invoke(context);
                if (destroyAtZeroHealth)
                {
                    Destroy(gameObject);
                }
            }

            return DamageResult.Applied(appliedDamage, $"Damage dummy received {appliedDamage:0.##} melee damage.");
        }

        public void ResetHealth()
        {
            currentHealth = MaxHealth;
            receivedHitCount = 0;
            lastDamageContext = null;
        }

        private void OnEnable()
        {
            if (resetHealthOnEnable)
            {
                ResetHealth();
            }
        }

        private void OnValidate()
        {
            maxHealth = Mathf.Max(1f, maxHealth);
            if (!Application.isPlaying)
            {
                currentHealth = MaxHealth;
            }
        }
    }
}
