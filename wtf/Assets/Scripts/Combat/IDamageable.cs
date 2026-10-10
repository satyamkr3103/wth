using System;
using UnityEngine;

namespace AsciiActionGame.Combat
{
    public interface IDamageable
    {
        float CurrentHealth { get; }
        float MaxHealth { get; }
        float Armor { get; }
        bool IsDead { get; }

        event Action<DamageInfo> OnDamaged;
        event Action<float> OnHealed;
        event Action<DamageInfo> OnDied;
        event Action<float, float> OnHealthChanged;

        void TakeDamage(DamageInfo damageInfo);
        void Heal(float amount);
    }
}
