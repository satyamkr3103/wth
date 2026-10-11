using System;
using System.Collections;
using UnityEngine;
using AsciiActionGame.Core;
using AsciiActionGame.Combat;
using AsciiActionGame.Player;
using AsciiActionGame.Projectiles;
using AsciiActionGame.Rendering;

namespace AsciiActionGame.Enemies
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Damageable))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class BossEntity : MonoBehaviour
    {
        public static BossEntity Instance { get; private set; }

        [Header("Boss Stats")]
        [SerializeField] private float _maxHealth = 4000f;
        [SerializeField] private float _armor = 15f;
        [SerializeField] private float _moveSpeed = 2.4f;

        [Header("Attacks")]
        [SerializeField] private float _attackInterval = 2.8f;
        [SerializeField] private float _gatlingBulletDamage = 16f;
        [SerializeField] private float _shockwaveDamage = 28f;
        [SerializeField] private float _rocketDamage = 35f;

        private Rigidbody2D _rb;
        private Damageable _damageable;
        private Transform _targetPlayer;
        private float _attackTimer = 2.0f;
        private int _attackPattern = 0;
        private bool _isEnraged = false;
        private bool _isCharging = false;
        private float _chargeTimer = 0f;
        private Vector2 _chargeDir;
        private bool _isExploding = false;

        public Damageable Damageable => _damageable;
        public float CurrentHealth => _damageable != null ? _damageable.CurrentHealth : 0f;
        public float MaxHealth => _maxHealth;
        public bool IsDead => (_damageable != null && _damageable.IsDead) || _isExploding;
        public bool IsEnraged => _isEnraged;
        public bool IsExploding => _isExploding;

        public static event Action OnBossDied;

        private void Awake()
        {
            Instance = this;
            _rb = GetComponent<Rigidbody2D>();
            _rb.gravityScale = 0f;
            _rb.freezeRotation = true;
            _rb.mass = 25f;

            var col = GetComponent<CircleCollider2D>();
            col.radius = 2.2f;

            _damageable = GetComponent<Damageable>();
            _damageable.Initialize(_maxHealth, _armor);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnEnable()
        {
            if (_damageable != null)
            {
                _damageable.OnDied += HandleDeath;
            }
        }

        private void OnDisable()
        {
            if (_damageable != null)
            {
                _damageable.OnDied -= HandleDeath;
            }
        }

        private void Start()
        {
            FindPlayer();
        }

        private void Update()
        {
            if (IsDead) return;

            if (_targetPlayer == null)
            {
                FindPlayer();
                if (_targetPlayer == null) return;
            }

            float hpRatio = CurrentHealth / _maxHealth;
            if (hpRatio < 0.35f && !_isEnraged)
            {
                _isEnraged = true;
                AsciiPostProcessing.TriggerScreenShake(0.8f);
            }

            float dt = Time.deltaTime;
            _attackTimer -= dt;

            if (_isCharging)
            {
                _chargeTimer -= dt;
                if (_chargeTimer <= 0f)
                {
                    _isCharging = false;
                }
            }
            else if (_attackTimer <= 0f)
            {
                ExecuteNextAttack();
                float nextCooldown = _isEnraged ? (_attackInterval * 0.55f) : _attackInterval;
                _attackTimer = nextCooldown;
            }
        }

        private void FixedUpdate()
        {
            if (IsDead || _targetPlayer == null)
            {
                if (_rb != null) _rb.linearVelocity = Vector2.zero;
                return;
            }

            if (_isCharging)
            {
                _rb.linearVelocity = _chargeDir * (_moveSpeed * 3.5f);
                return;
            }

            Vector2 toPlayer = (Vector2)_targetPlayer.position - _rb.position;
            float dist = toPlayer.magnitude;

            if (dist > 3.5f)
            {
                _rb.linearVelocity = toPlayer.normalized * _moveSpeed;
            }
            else
            {
                _rb.linearVelocity = Vector2.zero;
            }
        }

        private void FindPlayer()
        {
            var p = FindFirstObjectByType<PlayerController>();
            if (p != null) _targetPlayer = p.transform;
        }

        private void ExecuteNextAttack()
        {
            switch (_attackPattern)
            {
                case 0:
                    FireGatlingVolley();
                    break;
                case 1:
                    TriggerSeismicShockwave();
                    break;
                case 2:
                    FireRocketBarrage();
                    break;
                case 3:
                    StartChargeAttack();
                    break;
            }

            _attackPattern = (_attackPattern + 1) % 4;
        }

        private void FireGatlingVolley()
        {
            if (_targetPlayer == null) return;
            Vector2 aim = ((Vector2)_targetPlayer.position - _rb.position).normalized;

            for (int i = -3; i <= 3; i++)
            {
                float angle = i * 6.5f;
                Vector2 dir = Quaternion.Euler(0, 0, angle) * aim;
                Vector2 spawnPos = _rb.position + (dir * 2.2f);

                if (FragmentPool.Instance != null)
                {
                    FragmentPool.Instance.Spawn(
                        spawnPos,
                        dir * 11f,
                        _gatlingBulletDamage,
                        4.0f,
                        gameObject,
                        new Color32(255, 60, 40, 255),
                        '='
                    );
                }
            }

            AsciiPostProcessing.TriggerScreenShake(0.3f);
        }

        private void TriggerSeismicShockwave()
        {
            int shockCount = _isEnraged ? 20 : 16;
            for (int i = 0; i < shockCount; i++)
            {
                float angle = i * (360f / shockCount);
                Vector2 dir = Quaternion.Euler(0, 0, angle) * Vector2.right;

                if (FragmentPool.Instance != null)
                {
                    FragmentPool.Instance.Spawn(
                        _rb.position + dir * 1.5f,
                        dir * 7.5f,
                        _shockwaveDamage,
                        3.5f,
                        gameObject,
                        new Color32(255, 170, 0, 255),
                        '*'
                    );
                }
            }

            AsciiPostProcessing.TriggerScreenShake(0.6f);
        }

        private void FireRocketBarrage()
        {
            if (_targetPlayer == null) return;
            Vector2 aim = ((Vector2)_targetPlayer.position - _rb.position).normalized;

            for (int i = -2; i <= 2; i++)
            {
                float angle = i * 18f;
                Vector2 dir = Quaternion.Euler(0, 0, angle) * aim;

                if (FragmentPool.Instance != null)
                {
                    FragmentPool.Instance.Spawn(
                        _rb.position + dir * 2.0f,
                        dir * 9.5f,
                        _rocketDamage,
                        5.0f,
                        gameObject,
                        new Color32(255, 230, 40, 255),
                        '#'
                    );
                }
            }

            AsciiPostProcessing.TriggerScreenShake(0.4f);
        }

        private void StartChargeAttack()
        {
            if (_targetPlayer == null) return;
            _chargeDir = ((Vector2)_targetPlayer.position - _rb.position).normalized;
            _isCharging = true;
            _chargeTimer = 0.9f;
            AsciiPostProcessing.TriggerScreenShake(0.45f);
        }

        private void HandleDeath(DamageInfo info)
        {
            if (_isExploding) return;
            _isExploding = true;

            if (_rb != null) _rb.linearVelocity = Vector2.zero;
            var col = GetComponent<Collider2D>();
            if (col != null) col.enabled = false;

            StartCoroutine(BossExplosionSequenceRoutine());
        }

        private IEnumerator BossExplosionSequenceRoutine()
        {
            int burstCount = 14;
            for (int i = 0; i < burstCount; i++)
            {
                Vector2 burstPos = (Vector2)transform.position + UnityEngine.Random.insideUnitCircle * 2.8f;
                Color32 burstColor = (i % 2 == 0) ? new Color32(255, 60, 20, 255) : new Color32(255, 200, 40, 255);
                DisintegrationEffect.TriggerBurst(burstPos, 28, burstColor);
                AsciiPostProcessing.TriggerScreenShake(0.5f);
                AsciiPostProcessing.TriggerGlitch(0.14f, 0.9f);

                if (FragmentPool.Instance != null && (i % 3 == 0))
                {
                    for (int k = 0; k < 6; k++)
                    {
                        float ang = k * 60f + (i * 15f);
                        Vector2 dir = Quaternion.Euler(0, 0, ang) * Vector2.right;
                        FragmentPool.Instance.Spawn(burstPos, dir * 8f, 0f, 0.6f, gameObject, burstColor, '*');
                    }
                }

                yield return new WaitForSeconds(0.12f);
            }

            DisintegrationEffect.TriggerBurst(transform.position, 120, new Color32(255, 255, 255, 255));
            DisintegrationEffect.TriggerBurst(transform.position, 80, new Color32(255, 40, 20, 255));
            AsciiPostProcessing.TriggerScreenShake(1.6f);
            AsciiPostProcessing.TriggerGlitch(0.4f, 1.0f);

            var asciiView = GetComponent<BossAsciiView>();
            if (asciiView != null)
            {
                asciiView.gameObject.SetActive(false);
            }

            yield return new WaitForSeconds(0.4f);

            OnBossDied?.Invoke();
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (IsDead) return;

            var playerDmg = collision.gameObject.GetComponent<IDamageable>();
            if (playerDmg != null && !playerDmg.IsDead)
            {
                Vector2 hitNormal = collision.contacts.Length > 0 ? collision.contacts[0].normal : Vector2.up;
                playerDmg.TakeDamage(new DamageInfo(
                    _isCharging ? 40f : 20f,
                    DamageType.Kinetic,
                    collision.contacts.Length > 0 ? collision.contacts[0].point : _rb.position,
                    hitNormal,
                    gameObject
                ));
            }
        }
    }
}
