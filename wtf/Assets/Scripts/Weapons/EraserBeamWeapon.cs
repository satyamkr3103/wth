using UnityEngine;
using AsciiActionGame.Combat;

namespace AsciiActionGame.Weapons
{
    public class EraserBeamWeapon : WeaponBase
    {
        [Header("Beam VFX Reference")]
        [SerializeField] private EraserBeamVfx _beamVfx;

        [Header("Penetration Settings")]
        [Tooltip("If true, beam passes through low-armor limbs to strike inner structures.")]
        [SerializeField] private bool _enablePiercing = false;

        private void Awake()
        {
            _weaponName = "Eraser Beam";
            _damage = 30f;
            _fireRate = 10f;
            _maxRange = 22f;

            if (_beamVfx == null)
            {
                _beamVfx = EraserBeamVfx.Instance ?? FindFirstObjectByType<EraserBeamVfx>();
            }
        }

        protected override void ExecuteFire(Vector2 origin, Vector2 direction)
        {
            if (_beamVfx == null)
            {
                _beamVfx = EraserBeamVfx.Instance ?? FindFirstObjectByType<EraserBeamVfx>();
            }

            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(_hitLayers);
            filter.useTriggers = true;

            RaycastHit2D[] hits = new RaycastHit2D[8];
            int hitCount = Physics2D.Raycast(origin, direction, filter, hits, _maxRange);

            Vector2 endPoint = origin + direction * _maxRange;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit2D hit = hits[i];
                if (hit.collider == null || hit.collider.gameObject == gameObject) continue;

                if (hit.collider.transform.IsChildOf(transform) || (transform.parent != null && hit.collider.transform.IsChildOf(transform.parent)))
                {
                    continue;
                }

                endPoint = hit.point;

                IDamageable damageable = hit.collider.GetComponent<IDamageable>();
                if (damageable == null)
                {
                    damageable = hit.collider.GetComponentInParent<IDamageable>();
                }

                if (damageable != null && !damageable.IsDead)
                {
                    DamageInfo info = new DamageInfo(
                        _damage,
                        DamageType.Eraser,
                        hit.point,
                        hit.normal,
                        gameObject
                    );

                    damageable.TakeDamage(info);
                    NotifyHit(hit, info);

                    AsciiActionGame.Rendering.DisintegrationEffect.TriggerBurst(hit.point, 3, null, 3.5f, 0.25f);
                    AsciiActionGame.Rendering.AsciiPostProcessing.TriggerScreenShake(0.06f);

                    if (!_enablePiercing)
                    {
                        break;
                    }
                }
                else
                {
                    break;
                }
            }

            if (_beamVfx != null)
            {
                _beamVfx.ShowBeam(origin, endPoint, 1f / Mathf.Max(1f, _fireRate));
            }
        }
    }
}
