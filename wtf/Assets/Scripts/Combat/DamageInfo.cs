using UnityEngine;

namespace AsciiActionGame.Combat
{
    public enum DamageType
    {
        Kinetic,
        Energy,
        Eraser,
        Corruption,
        Explosive
    }

    public struct DamageInfo
    {
        public float Amount;
        public DamageType Type;
        public Vector2 HitPoint;
        public Vector2 HitNormal;
        public GameObject Instigator;

        public DamageInfo(float amount, DamageType type, Vector2 hitPoint, Vector2 hitNormal = default, GameObject instigator = null)
        {
            Amount = amount;
            Type = type;
            HitPoint = hitPoint;
            HitNormal = hitNormal;
            Instigator = instigator;
        }
    }
}
