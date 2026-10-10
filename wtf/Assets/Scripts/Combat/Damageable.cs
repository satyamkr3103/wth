using System;
using UnityEngine;

namespace AsciiActionGame.Combat
{
    public class Damageable : MonoBehaviour, IDamageable
    {
        [Header("Health & Defense")]
        [SerializeField] private float _maxHealth = 100f;
        [SerializeField] private float _currentHealth = 100f;
        [SerializeField] private float _armor = 0f;

        [Header("Damage Mitigation")]
        [Tooltip("Minimum damage applied regardless of armor defense.")]
        [SerializeField] private float _minDamageFloor = 1f;

        [Tooltip("Invulnerability time in seconds after taking damage.")]
        [SerializeField] private float _invulnerabilityDuration = 0.05f;

        [Header("Hit Flash")]
        [SerializeField] private float _hitFlashDuration = 0.08f;

        private float _lastDamageTime = -999f;
        private float _flashTimer = 0f;
        private bool _isDead;

        public float CurrentHealth => _currentHealth;
        public float MaxHealth => _maxHealth;
        public float Armor => _armor;
        public bool IsDead => _isDead;
        public bool IsFlashing => _flashTimer > 0f;

        public event Action<DamageInfo> OnDamaged;
        public event Action<float> OnHealed;
        public event Action<DamageInfo> OnDied;
        public event Action<float, float> OnHealthChanged;

        private void Awake()
        {
            _currentHealth = _maxHealth;
            _isDead = false;
        }

        private void Update()
        {
            if (_flashTimer > 0f)
            {
                _flashTimer -= Time.deltaTime;
            }
        }

        public void Initialize(float maxHealth, float armor = 0f)
        {
            _maxHealth = Mathf.Max(1f, maxHealth);
            _currentHealth = _maxHealth;
            _armor = Mathf.Max(0f, armor);
            _isDead = false;
            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
        }

        public void TakeDamage(DamageInfo damageInfo)
        {
            if (_isDead) return;

            if (Time.time < _lastDamageTime + _invulnerabilityDuration)
            {
                return;
            }

            _lastDamageTime = Time.time;
            _flashTimer = _hitFlashDuration;

            float effectiveDamage = Mathf.Max(_minDamageFloor, damageInfo.Amount - _armor);

            if (damageInfo.Type == DamageType.Eraser)
            {
                effectiveDamage = Mathf.Max(_minDamageFloor, damageInfo.Amount - (_armor * 0.5f));
            }

            _currentHealth = Mathf.Max(0f, _currentHealth - effectiveDamage);
            damageInfo.Amount = effectiveDamage;

            OnDamaged?.Invoke(damageInfo);
            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);

            if (_currentHealth <= 0f)
            {
                _isDead = true;
                OnDied?.Invoke(damageInfo);
            }
        }

        public void Heal(float amount)
        {
            if (_isDead || amount <= 0f) return;

            _currentHealth = Mathf.Min(_maxHealth, _currentHealth + amount);
            OnHealed?.Invoke(amount);
            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
        }

        public void SetArmor(float armor)
        {
            _armor = Mathf.Max(0f, armor);
        }
    }
}
