using System;
using System.Collections.Generic;
using UnityEngine;
using AsciiActionGame.Combat;
using AsciiActionGame.Core;
using AsciiActionGame.Player;
using AsciiActionGame.Rendering;

namespace AsciiActionGame.Enemies
{
    public enum EnemyState
    {
        Idle,
        Patrol,
        Chasing,
        Attacking,
        Stunned,
        Dead
    }

    [RequireComponent(typeof(Rigidbody2D))]
    public class EnemyEntity : MonoBehaviour, IDamageable
    {
        [Header("State")]
        [SerializeField] private EnemyState _currentState = EnemyState.Idle;

        [Header("Movement & Physics")]
        [SerializeField] private float _baseMoveSpeed = 4.5f;
        [SerializeField] private float _acceleration = 30.0f;
        [SerializeField] private float _detectionRadius = 14.0f;
        [SerializeField] private float _attackRadius = 4.0f;
        [SerializeField] private float _stopRadius = 2.0f;

        [Header("Contact Damage")]
        [SerializeField] private float _contactDamage = 14f;
        [SerializeField] private float _contactCooldown = 0.6f;

        [Header("Body Hierarchy")]
        [SerializeField] private List<EnemyBodyPart> _bodyParts = new List<EnemyBodyPart>();
        [SerializeField] private EnemyBodyPart _corePart;

        private Rigidbody2D _rb;
        private Transform _targetPlayer;
        private Vector2 _facingDirection = Vector2.down;
        private Vector2 _moveVelocity;
        private float _stateTimer = 0f;
        private float _lastContactDamageTime = -999f;

        public EnemyState CurrentState => _currentState;
        public Vector2 Position => _rb != null ? _rb.position : (Vector2)transform.position;
        public Vector2 FacingDirection => _facingDirection;
        public bool IsDead => _currentState == EnemyState.Dead;
        public IReadOnlyList<EnemyBodyPart> BodyParts => _bodyParts;
        public EnemyBodyPart CorePart => _corePart;

        public float CurrentHealth => _corePart != null && _corePart.Damageable != null ? _corePart.Damageable.CurrentHealth : 0f;
        public float MaxHealth => _corePart != null && _corePart.Damageable != null ? _corePart.Damageable.MaxHealth : 60f;
        public float Armor => _corePart != null && _corePart.Damageable != null ? _corePart.Damageable.Armor : 0f;

        public event Action<EnemyState> OnStateChanged;
        public event Action<EnemyBodyPart> OnPartDestroyedEvent;
        public event Action<EnemyEntity> OnEnemyDied;
        public event Action<DamageInfo> OnDamaged;
        public event Action<DamageInfo> OnDied;
        public event Action<float> OnHealed;
        public event Action<float, float> OnHealthChanged;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.gravityScale = 0f;
            _rb.freezeRotation = true;
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            CollectBodyParts();
        }

        private void Start()
        {
            FindPlayerTarget();
        }

        private void Update()
        {
            if (_currentState == EnemyState.Dead) return;

            if (_targetPlayer == null)
            {
                FindPlayerTarget();
            }

            UpdateStateTransitions();
            UpdateFacing();
        }

        private void FixedUpdate()
        {
            if (_currentState == EnemyState.Dead)
            {
                _rb.linearVelocity = Vector2.zero;
                return;
            }

            ApplyMovementPhysics();
        }

        private void FindPlayerTarget()
        {
            var player = FindFirstObjectByType<PlayerController>();
            if (player != null)
            {
                _targetPlayer = player.transform;
            }
        }

        public void CollectBodyParts()
        {
            _bodyParts.Clear();
            var parts = GetComponentsInChildren<EnemyBodyPart>();
            foreach (var part in parts)
            {
                _bodyParts.Add(part);
                part.SetParentEnemy(this);
                if (part.PartType == BodyPartType.Core)
                {
                    _corePart = part;
                }
            }

            if (_bodyParts.Count == 0)
            {
                CreateDefaultPart("Core", BodyPartType.Core, GameConstants.GLYPH_ENEMY_CORE, Vector2.zero, 60f, 0f, 0.25f);
                CreateDefaultPart("ChestArmor", BodyPartType.ChestArmor, GameConstants.GLYPH_ENEMY_ARMOR, new Vector2(0f, 0.4f), 45f, 10f, 0.22f);
                CreateDefaultPart("LeftArm", BodyPartType.LeftArm, GameConstants.GLYPH_ENEMY_ARM_LEFT, new Vector2(-0.5f, 0.1f), 25f, 2f, 0.2f);
                CreateDefaultPart("RightArm", BodyPartType.RightArm, GameConstants.GLYPH_ENEMY_ARM_RIGHT, new Vector2(0.5f, 0.1f), 25f, 2f, 0.2f);
                CreateDefaultPart("LeftLeg", BodyPartType.LeftLeg, GameConstants.GLYPH_ENEMY_LEG, new Vector2(-0.3f, -0.4f), 20f, 0f, 0.18f);
                CreateDefaultPart("RightLeg", BodyPartType.RightLeg, GameConstants.GLYPH_ENEMY_LEG, new Vector2(0.3f, -0.4f), 20f, 0f, 0.18f);
            }
        }

        private void CreateDefaultPart(string partName, BodyPartType type, char glyph, Vector2 offset, float health, float armor, float colRadius)
        {
            GameObject partObj = new GameObject(partName);
            partObj.transform.SetParent(transform, false);
            partObj.transform.localPosition = offset;

            var col = partObj.AddComponent<CircleCollider2D>();
            col.radius = colRadius;

            var dmg = partObj.AddComponent<Damageable>();
            var bodyPart = partObj.AddComponent<EnemyBodyPart>();
            bodyPart.Configure(type, glyph, offset, health, armor);

            _bodyParts.Add(bodyPart);
            bodyPart.SetParentEnemy(this);
            if (type == BodyPartType.Core)
            {
                _corePart = bodyPart;
            }
        }

        private void UpdateStateTransitions()
        {
            _stateTimer += Time.deltaTime;
            if (_targetPlayer == null)
            {
                ChangeState(EnemyState.Idle);
                return;
            }

            float distToPlayer = Vector2.Distance(Position, _targetPlayer.position);

            switch (_currentState)
            {
                case EnemyState.Idle:
                case EnemyState.Patrol:
                    if (distToPlayer <= _detectionRadius)
                    {
                        ChangeState(EnemyState.Chasing);
                    }
                    break;

                case EnemyState.Chasing:
                    if (distToPlayer > _detectionRadius * 1.3f)
                    {
                        ChangeState(EnemyState.Idle);
                    }
                    else if (distToPlayer <= _attackRadius)
                    {
                        ChangeState(EnemyState.Attacking);
                    }
                    break;

                case EnemyState.Attacking:
                    if (distToPlayer > _attackRadius * 1.25f)
                    {
                        ChangeState(EnemyState.Chasing);
                    }
                    break;

                case EnemyState.Stunned:
                    if (_stateTimer >= 0.4f)
                    {
                        ChangeState(distToPlayer <= _attackRadius ? EnemyState.Attacking : EnemyState.Chasing);
                    }
                    break;
            }
        }

        private void UpdateFacing()
        {
            if (_targetPlayer != null && (_currentState == EnemyState.Chasing || _currentState == EnemyState.Attacking))
            {
                Vector2 dir = ((Vector2)_targetPlayer.position - Position).normalized;
                if (dir.sqrMagnitude > 0.01f)
                {
                    _facingDirection = dir;
                }
            }
            else if (_rb.linearVelocity.sqrMagnitude > 0.05f)
            {
                _facingDirection = _rb.linearVelocity.normalized;
            }
        }

        private void ApplyMovementPhysics()
        {
            Vector2 targetVel = Vector2.zero;

            if (_targetPlayer != null && _currentState == EnemyState.Chasing)
            {
                float dist = Vector2.Distance(Position, _targetPlayer.position);
                if (dist > _stopRadius)
                {
                    Vector2 desiredDir = ((Vector2)_targetPlayer.position - Position).normalized;
                    targetVel = desiredDir * CalculateCurrentMoveSpeed();
                }
            }
            else if (_targetPlayer != null && _currentState == EnemyState.Attacking)
            {
                Vector2 desiredDir = ((Vector2)_targetPlayer.position - Position).normalized;
                Vector2 perp = new Vector2(-desiredDir.y, desiredDir.x);
                targetVel = perp * (CalculateCurrentMoveSpeed() * 0.4f);
            }

            _rb.linearVelocity = Vector2.MoveTowards(_rb.linearVelocity, targetVel, _acceleration * Time.fixedDeltaTime);
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            CheckContactDamage(collision.collider);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            CheckContactDamage(other);
        }

        private void CheckContactDamage(Collider2D other)
        {
            if (_currentState == EnemyState.Dead) return;
            if (Time.time < _lastContactDamageTime + _contactCooldown) return;

            var player = other.GetComponent<PlayerController>() ?? other.GetComponentInParent<PlayerController>();
            if (player != null && !player.IsDead)
            {
                _lastContactDamageTime = Time.time;
                Vector2 pushDir = (player.Position - Position).normalized;
                DamageInfo info = new DamageInfo(_contactDamage, DamageType.Kinetic, Position, pushDir, gameObject);
                player.Damageable?.TakeDamage(info);
            }
        }

        public float CalculateCurrentMoveSpeed()
        {
            int intactLegs = 0;
            foreach (var part in _bodyParts)
            {
                if (part.IsActivePart && (part.PartType == BodyPartType.LeftLeg || part.PartType == BodyPartType.RightLeg))
                {
                    intactLegs++;
                }
            }

            if (intactLegs >= 2) return _baseMoveSpeed;
            if (intactLegs == 1) return _baseMoveSpeed * 0.55f;
            return _baseMoveSpeed * 0.25f;
        }

        public void ChangeState(EnemyState newState)
        {
            if (_currentState == newState || _currentState == EnemyState.Dead) return;

            _currentState = newState;
            _stateTimer = 0f;
            OnStateChanged?.Invoke(newState);
        }

        public void OnPartTookDamage(EnemyBodyPart part, DamageInfo info)
        {
            if (info.Amount >= 30f && _currentState != EnemyState.Dead)
            {
                ChangeState(EnemyState.Stunned);
            }
        }

        public void OnPartLost(EnemyBodyPart part)
        {
            OnPartDestroyedEvent?.Invoke(part);

            if (part == _corePart || part.PartType == BodyPartType.Core)
            {
                Die();
            }
        }

        public void TakeDamage(DamageInfo info)
        {
            EnemyBodyPart part = GetClosestActiveBodyPart(info.HitPoint);
            if (part != null)
            {
                part.Disappear(info.HitPoint, true);
                var playerCombat = FindFirstObjectByType<Player.PlayerCombat>();
                if (playerCombat != null)
                {
                    playerCombat.AddCollectedBodyPart();
                }
                OnDamaged?.Invoke(info);
                OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
            }
            else if (_corePart != null && _corePart.IsActivePart)
            {
                _corePart.Damageable?.TakeDamage(info);
                OnDamaged?.Invoke(info);
                OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
            }
        }

        public void Heal(float amount)
        {
            if (_corePart != null && _corePart.Damageable != null)
            {
                _corePart.Damageable.Heal(amount);
                OnHealed?.Invoke(amount);
                OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
            }
        }

        public EnemyBodyPart GetClosestActiveBodyPart(Vector2 hitPoint)
        {
            EnemyBodyPart closest = null;
            float closestDist = float.MaxValue;

            foreach (var part in _bodyParts)
            {
                if (part != null && part.IsActivePart && part.PartType != BodyPartType.Core)
                {
                    Vector2 partPos = part.GetWorldPosition(Position, _facingDirection);
                    float d = Vector2.Distance(hitPoint, partPos);
                    if (d < closestDist)
                    {
                        closestDist = d;
                        closest = part;
                    }
                }
            }

            return closest ?? (_corePart != null && _corePart.IsActivePart ? _corePart : null);
        }

        public void Die()
        {
            if (_currentState == EnemyState.Dead) return;

            ChangeState(EnemyState.Dead);
            _rb.linearVelocity = Vector2.zero;

            foreach (var part in _bodyParts)
            {
                if (part != null && part.PartCollider != null)
                {
                    part.PartCollider.enabled = false;
                }
            }

            var rootCol = GetComponent<Collider2D>();
            if (rootCol != null) rootCol.enabled = false;

            DisintegrationEffect.TriggerBurst(Position, 22);
            AsciiPostProcessing.TriggerScreenShake(0.4f);
            AsciiPostProcessing.TriggerGlitch(0.18f, 0.9f);

            OnDied?.Invoke(new DamageInfo(0f, DamageType.Kinetic, Position));
            OnEnemyDied?.Invoke(this);

            Destroy(gameObject, 0.7f);
        }

        public EnemyBodyPart GetBodyPart(BodyPartType type)
        {
            foreach (var part in _bodyParts)
            {
                if (part != null && part.PartType == type && part.IsActivePart)
                {
                    return part;
                }
            }
            return null;
        }

        public List<EnemyBodyPart> GetActiveBodyParts()
        {
            var active = new List<EnemyBodyPart>();
            foreach (var part in _bodyParts)
            {
                if (part != null && part.IsActivePart)
                {
                    active.Add(part);
                }
            }
            return active;
        }
    }
}
