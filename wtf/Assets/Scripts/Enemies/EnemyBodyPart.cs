using System;
using UnityEngine;
using AsciiActionGame.Combat;
using AsciiActionGame.Core;

namespace AsciiActionGame.Enemies
{
    public enum BodyPartType
    {
        Core,
        ChestArmor,
        LeftArm,
        RightArm,
        LeftLeg,
        RightLeg
    }

    [RequireComponent(typeof(Damageable))]
    public class EnemyBodyPart : MonoBehaviour
    {
        [Header("Part Identity")]
        [SerializeField] private BodyPartType _partType = BodyPartType.Core;
        [SerializeField] private char _glyph = 'X';
        [SerializeField] private Vector2 _localOffset = Vector2.zero;

        [Header("Hitbox")]
        [SerializeField] private Collider2D _partCollider;

        private Damageable _damageable;
        private EnemyEntity _parentEnemy;
        private bool _isDetached = false;
        private bool _severedByPlayer = false;
        private float _maxHealthCached = 30f;
        private float _armorCached = 0f;

        public BodyPartType PartType => _partType;
        public char Glyph => _glyph;
        public Vector2 LocalOffset => _localOffset;
        public Damageable Damageable => _damageable;
        public Collider2D PartCollider => _partCollider;
        public bool IsDetached => _isDetached;
        public bool IsDestroyed => _damageable != null && _damageable.IsDead;
        public bool IsActivePart => !_isDetached && !IsDestroyed && !_severedByPlayer;
        public bool IsSeveredByPlayer => _severedByPlayer;

        public event Action<EnemyBodyPart, DamageInfo> OnPartDamaged;
        public event Action<EnemyBodyPart> OnPartDestroyed;
        public event Action<EnemyBodyPart> OnPartDetached;

        private void Awake()
        {
            _damageable = GetComponent<Damageable>();

            if (_partCollider == null)
            {
                _partCollider = GetComponent<Collider2D>();
            }

            _parentEnemy = GetComponentInParent<EnemyEntity>();
        }

        private void OnEnable()
        {
            if (_damageable != null)
            {
                _damageable.OnDamaged += HandleDamageTaken;
                _damageable.OnDied += HandlePartDied;
            }
        }

        private void OnDisable()
        {
            if (_damageable != null)
            {
                _damageable.OnDamaged -= HandleDamageTaken;
                _damageable.OnDied -= HandlePartDied;
            }
        }

        public void Configure(BodyPartType partType, char glyph, Vector2 localOffset, float health, float armor)
        {
            _partType = partType;
            _glyph = glyph;
            _localOffset = localOffset;
            _maxHealthCached = health;
            _armorCached = armor;

            if (_damageable == null)
            {
                _damageable = GetComponent<Damageable>();
            }

            _damageable.Initialize(health, armor);
        }

        public void SetParentEnemy(EnemyEntity enemy)
        {
            _parentEnemy = enemy;
        }

        private void HandleDamageTaken(DamageInfo info)
        {
            OnPartDamaged?.Invoke(this, info);
            if (_parentEnemy != null)
            {
                _parentEnemy.OnPartTookDamage(this, info);
            }
        }

        private void HandlePartDied(DamageInfo info)
        {
            Disappear(info.HitPoint, true);
        }

        public void Disappear(Vector2 hitPoint, bool byPlayer = true)
        {
            if (_isDetached || _severedByPlayer) return;

            _severedByPlayer = byPlayer;
            _isDetached = true;

            if (_partCollider != null)
            {
                _partCollider.enabled = false;
            }

            AsciiActionGame.Rendering.DisintegrationEffect.TriggerBurst(hitPoint, 10);
            AsciiActionGame.Rendering.AsciiPostProcessing.TriggerScreenShake(0.18f);

            OnPartDestroyed?.Invoke(this);

            if (_parentEnemy != null)
            {
                _parentEnemy.OnPartLost(this);
            }
        }

        public void Detach(Vector2 ejectVelocity)
        {
            if (_isDetached) return;

            _isDetached = true;

            if (_partCollider != null)
            {
                _partCollider.enabled = false;
            }

            OnPartDetached?.Invoke(this);

            if (_parentEnemy != null)
            {
                _parentEnemy.OnPartLost(this);
            }
        }

        public void Regenerate()
        {
            if (_severedByPlayer && _partType != BodyPartType.Core) return;

            _isDetached = false;
            _severedByPlayer = false;

            if (_damageable != null)
            {
                _damageable.Initialize(_maxHealthCached, _armorCached);
            }

            if (_partCollider != null)
            {
                _partCollider.enabled = true;
            }

            Vector2 spawnWorld = _parentEnemy != null ? GetWorldPosition(_parentEnemy.Position, _parentEnemy.FacingDirection) : (Vector2)transform.position;
            AsciiActionGame.Rendering.DisintegrationEffect.TriggerBurst(spawnWorld, 6, new Color32(0, 255, 180, 255), 2.5f, 0.2f);
        }

        public Vector2 GetWorldPosition(Vector2 enemyPos, Vector2 forwardDir)
        {
            Vector2 rightDir = new Vector2(forwardDir.y, -forwardDir.x);
            return enemyPos + (rightDir * _localOffset.x) + (forwardDir * _localOffset.y);
        }
    }
}
