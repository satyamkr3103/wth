using System;
using UnityEngine;
using AsciiActionGame.Combat;

namespace AsciiActionGame.Weapons
{
    public abstract class WeaponBase : MonoBehaviour, IWeapon
    {
        [Header("Weapon Identity")]
        [SerializeField] protected string _weaponName = "Weapon";

        [Header("Weapon Parameters")]
        [SerializeField] protected float _damage = 25f;
        [SerializeField] protected float _fireRate = 6f;
        [SerializeField] protected float _maxRange = 20f;
        [SerializeField] protected LayerMask _hitLayers = ~0;

        protected float _nextFireTime = 0f;
        protected bool _isFiring = false;

        public string WeaponName => _weaponName;
        public float Damage => _damage;
        public float FireRate => _fireRate;
        public float MaxRange => _maxRange;
        public LayerMask HitLayers => _hitLayers;

        public virtual bool CanFire => Time.time >= _nextFireTime;
        public virtual bool IsFiring => _isFiring;

        public event Action<Vector2, Vector2> OnWeaponFired;
        public event Action<RaycastHit2D, DamageInfo> OnWeaponHit;

        public virtual void Fire(Vector2 origin, Vector2 direction)
        {
            if (!CanFire) return;

            _nextFireTime = Time.time + (1f / Mathf.Max(0.1f, _fireRate));
            _isFiring = true;

            ExecuteFire(origin, direction.normalized);
            OnWeaponFired?.Invoke(origin, direction);
        }

        public virtual void StopFiring()
        {
            _isFiring = false;
        }

        protected abstract void ExecuteFire(Vector2 origin, Vector2 direction);

        protected void NotifyHit(RaycastHit2D hit, DamageInfo info)
        {
            OnWeaponHit?.Invoke(hit, info);
        }
    }
}
