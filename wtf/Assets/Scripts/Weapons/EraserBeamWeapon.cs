using UnityEngine;
using AsciiActionGame.Combat;
using AsciiActionGame.Enemies;

namespace AsciiActionGame.Weapons
{
    public class EraserBeamWeapon : WeaponBase
    {
        [Header("Beam VFX Reference")]
        [SerializeField] private EraserBeamVfx _beamVfx;

        [Header("Penetration Settings")]
        [SerializeField] private bool _enablePiercing = false;

        private void Awake()
        {
            _weaponName = "Eraser Beam";
            _damage = 35f;
            _fireRate = 3.5f;
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

            RaycastHit2D[] hits = new RaycastHit2D[12];
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

                EnemyEntity enemy = hit.collider.GetComponent<EnemyEntity>() ?? hit.collider.GetComponentInParent<EnemyEntity>();
                if (enemy != null && enemy.IsDashing)
                {
                    AsciiActionGame.Rendering.DisintegrationEffect.TriggerBurst(hit.point, 4, new Color32(0, 255, 255, 255), 2.5f, 0.15f);
                    continue;
                }

                EnemyBodyPart bodyPart = hit.collider.GetComponent<EnemyBodyPart>();
                if (bodyPart != null && bodyPart.IsActivePart)
                {
                    bodyPart.Disappear(hit.point, true);
                    var playerCombat = GetComponentInParent<Player.PlayerCombat>();
                    if (playerCombat != null) playerCombat.AddCollectedBodyPart();

                    AsciiActionGame.Rendering.DisintegrationEffect.TriggerBurst(hit.point, 5, null, 3.5f, 0.25f);
                    AsciiActionGame.Rendering.AsciiPostProcessing.TriggerScreenShake(0.08f);

                    if (!_enablePiercing) break;
                    continue;
                }

                if (enemy != null && !enemy.IsDead)
                {
                    DamageInfo info = new DamageInfo(_damage, DamageType.Eraser, hit.point, hit.normal, gameObject);
                    enemy.TakeDamage(info);

                    AsciiActionGame.Rendering.DisintegrationEffect.TriggerBurst(hit.point, 5, null, 3.5f, 0.25f);
                    AsciiActionGame.Rendering.AsciiPostProcessing.TriggerScreenShake(0.08f);

                    if (!_enablePiercing) break;
                    continue;
                }

                IDamageable damageable = hit.collider.GetComponent<IDamageable>() ?? hit.collider.GetComponentInParent<IDamageable>();
                if (damageable != null && !damageable.IsDead)
                {
                    DamageInfo info = new DamageInfo(_damage, DamageType.Eraser, hit.point, hit.normal, gameObject);
                    damageable.TakeDamage(info);

                    AsciiActionGame.Rendering.DisintegrationEffect.TriggerBurst(hit.point, 3, null, 3.5f, 0.25f);
                    AsciiActionGame.Rendering.AsciiPostProcessing.TriggerScreenShake(0.06f);

                    if (!_enablePiercing) break;
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
