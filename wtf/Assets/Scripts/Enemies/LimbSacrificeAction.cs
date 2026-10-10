using System;
using UnityEngine;
using AsciiActionGame.Combat;
using AsciiActionGame.Core;
using AsciiActionGame.Projectiles;
using AsciiActionGame.Player;

namespace AsciiActionGame.Enemies
{
    [RequireComponent(typeof(EnemyEntity))]
    public class LimbSacrificeAction : MonoBehaviour
    {
        [Header("Fragmentation Tuning")]
        [Tooltip("Number of shrapnel fragments spawned when a limb is sacrificed (4 to 5).")]
        [Range(4, 8)]
        [SerializeField] private int _fragmentsPerLimb = 5;

        [Tooltip("Spread angle in degrees for the fragment cone.")]
        [SerializeField] private float _spreadAngleDegrees = 42f;

        [SerializeField] private float _fragmentSpeed = 9.5f;
        [SerializeField] private float _fragmentDamage = 16f;
        [SerializeField] private float _fragmentLifetime = 2.2f;

        [Header("Tactical Trigger Conditions")]
        [SerializeField] private bool _autoTrigger = true;
        [Tooltip("Trigger sacrifice if core health drops below this percentage.")]
        [Range(0.1f, 0.9f)]
        [SerializeField] private float _coreHealthTriggerThreshold = 0.5f;

        [Tooltip("Minimum distance to player required to execute sacrifice.")]
        [SerializeField] private float _maxTriggerDistance = 8.0f;

        [SerializeField] private float _cooldown = 3.5f;

        [Header("Physics Recoil")]
        [SerializeField] private float _recoilImpulse = 4.5f;

        private EnemyEntity _enemy;
        private Rigidbody2D _rb;
        private Transform _playerTransform;
        private float _lastSacrificeTime = -999f;

        public event Action<EnemyBodyPart, int> OnLimbSacrificed;
        public event Action<string> OnCyberTacticalDialogue;

        private void Awake()
        {
            _enemy = GetComponent<EnemyEntity>();
            _rb = GetComponent<Rigidbody2D>();
        }

        private void Start()
        {
            var player = FindFirstObjectByType<PlayerController>();
            if (player != null)
            {
                _playerTransform = player.transform;
            }
        }

        private void Update()
        {
            if (!_autoTrigger || _enemy.IsDead) return;

            if (Time.time < _lastSacrificeTime + _cooldown) return;

            CheckAutonomousTrigger();
        }

        private void CheckAutonomousTrigger()
        {
            if (_playerTransform == null)
            {
                var player = FindFirstObjectByType<PlayerController>();
                if (player != null) _playerTransform = player.transform;
                else return;
            }

            float dist = Vector2.Distance(_enemy.Position, _playerTransform.position);
            if (dist > _maxTriggerDistance) return;

            if (_enemy.CorePart != null && _enemy.CorePart.Damageable != null)
            {
                float coreRatio = _enemy.CorePart.Damageable.CurrentHealth / _enemy.CorePart.Damageable.MaxHealth;
                if (coreRatio <= _coreHealthTriggerThreshold && CanSacrificeLimb())
                {
                    ExecuteSacrifice();
                }
            }
        }

        public bool CanSacrificeLimb()
        {
            return GetNextSacrificableLimb() != null;
        }

        public EnemyBodyPart GetNextSacrificableLimb()
        {
            var leftArm = _enemy.GetBodyPart(BodyPartType.LeftArm);
            if (leftArm != null && leftArm.IsActivePart) return leftArm;

            var rightArm = _enemy.GetBodyPart(BodyPartType.RightArm);
            if (rightArm != null && rightArm.IsActivePart) return rightArm;

            var armor = _enemy.GetBodyPart(BodyPartType.ChestArmor);
            if (armor != null && armor.IsActivePart) return armor;

            return null;
        }

        public bool ExecuteSacrifice(BodyPartType? specificPart = null, Vector2? customDirection = null)
        {
            if (_enemy.IsDead) return false;

            EnemyBodyPart partToSacrifice = null;

            if (specificPart.HasValue)
            {
                partToSacrifice = _enemy.GetBodyPart(specificPart.Value);
            }

            if (partToSacrifice == null || !partToSacrifice.IsActivePart)
            {
                partToSacrifice = GetNextSacrificableLimb();
            }

            if (partToSacrifice == null) return false;

            _lastSacrificeTime = Time.time;

            Vector2 origin = partToSacrifice.GetWorldPosition(_enemy.Position, _enemy.FacingDirection);
            Vector2 aimDir;

            if (customDirection.HasValue && customDirection.Value.sqrMagnitude > 0.01f)
            {
                aimDir = customDirection.Value.normalized;
            }
            else if (_playerTransform != null)
            {
                aimDir = ((Vector2)_playerTransform.position - origin).normalized;
            }
            else
            {
                aimDir = _enemy.FacingDirection;
            }

            partToSacrifice.Detach(-aimDir * 2f);

            SpawnFragmentFan(origin, aimDir, _fragmentsPerLimb);

            if (_rb != null)
            {
                _rb.AddForce(-aimDir * _recoilImpulse, ForceMode2D.Impulse);
            }

            string dialogue = $"ERR: RECONFIGURING {partToSacrifice.PartType.ToString().ToUpper()} TO SHRAPNEL CLUSTER.";
            OnCyberTacticalDialogue?.Invoke(dialogue);
            Debug.Log($"[LimbSacrificeAction] {dialogue}");

            AsciiActionGame.Rendering.AsciiPostProcessing.TriggerScreenShake(0.35f);
            AsciiActionGame.Rendering.AsciiPostProcessing.TriggerGlitch(0.12f, 0.7f);

            OnLimbSacrificed?.Invoke(partToSacrifice, _fragmentsPerLimb);
            return true;
        }

        private void SpawnFragmentFan(Vector2 origin, Vector2 centerDirection, int count)
        {
            if (FragmentPool.Instance == null)
            {
                var poolObj = new GameObject("FragmentPool");
                poolObj.AddComponent<FragmentPool>();
            }

            float baseAngle = Mathf.Atan2(centerDirection.y, centerDirection.x) * Mathf.Rad2Deg;
            float halfSpread = _spreadAngleDegrees * 0.5f;

            for (int i = 0; i < count; i++)
            {
                float t = count > 1 ? (float)i / (count - 1) : 0.5f;
                float angleOffset = Mathf.Lerp(-halfSpread, halfSpread, t) + UnityEngine.Random.Range(-3f, 3f);
                float finalAngle = (baseAngle + angleOffset) * Mathf.Deg2Rad;

                Vector2 launchDir = new Vector2(Mathf.Cos(finalAngle), Mathf.Sin(finalAngle));
                float speed = _fragmentSpeed * UnityEngine.Random.Range(0.88f, 1.15f);
                Vector2 velocity = launchDir * speed;

                FragmentPool.Instance.Spawn(
                    origin,
                    velocity,
                    _fragmentDamage,
                    _fragmentLifetime,
                    gameObject,
                    null,
                    GameConstants.GLYPH_FRAGMENT
                );
            }
        }
    }
}
