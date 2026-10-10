using UnityEngine;

namespace AsciiActionGame.Weapons
{
    public interface IWeapon
    {
        string WeaponName { get; }
        bool CanFire { get; }
        bool IsFiring { get; }
        void Fire(Vector2 origin, Vector2 direction);
        void StopFiring();
    }
}
