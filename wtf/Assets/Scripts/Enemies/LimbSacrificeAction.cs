using System;
using System.Collections.Generic;
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
        [Header("Tactical Limb Launch Settings")]
        [SerializeField] private bool _autoTrigger = true;
        [SerializeField] private float _initialDelay = 1.0f;
        [SerializeField] private float _launchInterval = 2.8f;
        [SerializeField] private float _maxTriggerDistance = 12.0f;

        [Header("Projectile & Blast Tuning")]
        [SerializeField] private float _launchSpeed = 8.8f;
        [SerializeField] private float _maxTravelDistance = 5.4f;
        [SerializeField] private float _blastRadius = 1.4f;
        [SerializeField] private float _blastDamage = 26.0f;
        [SerializeField] private float _recoilImpulse = 4.2f;

        [Header("Regeneration Tuning")]
        [SerializeField] private float _limbRegenDelay = 4.5f;

        private class RegeneratingLimb
        {
            public EnemyBodyPart Part;
            public float RegenTime;
        }

        private EnemyEntity _enemy;
        private Rigidbody2D _rb;
        private Transform _playerTransform;
        private AsciiColorPalette _palette;
        private readonly List<RegeneratingLimb> _regenQueue = new List<RegeneratingLimb>();
        private float _lastSacrificeTime = -999f;
        private float _startTime = 0f;

        public event Action<EnemyBodyPart, int> OnLimbSacrificed;

        private void Awake()
        {
            _enemy = GetComponent<EnemyEntity>();
            _rb = GetComponent<Rigidbody2D>();
            _startTime = Time.time;
        }

        private void Start()
        {
            FindPlayer();

            var map = FindFirstObjectByType<World.AsciiWorldMap>();
            if (map != null) _palette = map.Palette;
            if (_palette == null) _palette = AsciiColorPalette.CreateDefault();
        }

        private void FindPlayer()
        {
            var player = FindFirstObjectByType<PlayerController>();
            if (player != null)
            {
                _playerTransform = player.transform;
            }
        }

        private void Update()
        {
            UpdateRegenerationQueue();

            if (!_autoTrigger || _enemy == null || _enemy.IsDead) return;

            if (_playerTransform == null)
            {
                FindPlayer();
                if (_playerTransform == null) return;
            }

            float dist = Vector2.Distance(_enemy.Position, _playerTransform.position);
            if (dist > _maxTriggerDistance) return;

            if (Time.time < _startTime + _initialDelay) return;
            if (Time.time < _lastSacrificeTime + _launchInterval) return;

            if (CanSacrificeLimb())
            {
                ExecuteSacrifice();
            }
        }

        private void UpdateRegenerationQueue()
        {
            for (int i = _regenQueue.Count - 1; i >= 0; i--)
            {
                if (Time.time >= _regenQueue[i].RegenTime)
                {
                    var part = _regenQueue[i].Part;
                    _regenQueue.RemoveAt(i);
                    if (part != null && !part.IsSeveredByPlayer)
                    {
                        part.Regenerate();
                    }
                }
            }
        }

        public bool CanSacrificeLimb()
        {
            return GetNextSacrificableLimb() != null;
        }

        public EnemyBodyPart GetNextSacrificableLimb()
        {
            if (_playerTransform == null) FindPlayer();

            bool playerOnLeft = _playerTransform != null && _playerTransform.position.x < _enemy.Position.x;
            return GetNextLimbToLaunch(playerOnLeft);
        }

        public EnemyBodyPart GetNextLimbToLaunch(bool playerOnLeft)
        {
            BodyPartType[] priorityOrder;
            if (playerOnLeft)
            {
                priorityOrder = new BodyPartType[]
                {
                    BodyPartType.ChestArmor,
                    BodyPartType.LeftArm,
                    BodyPartType.RightArm,
                    BodyPartType.LeftLeg,
                    BodyPartType.RightLeg
                };
            }
            else
            {
                priorityOrder = new BodyPartType[]
                {
                    BodyPartType.ChestArmor,
                    BodyPartType.RightArm,
                    BodyPartType.LeftArm,
                    BodyPartType.RightLeg,
                    BodyPartType.LeftLeg
                };
            }

            foreach (var type in priorityOrder)
            {
                var part = _enemy.GetBodyPart(type);
                if (part != null && part.IsActivePart)
                {
                    return part;
                }
            }

            return null;
        }

        public bool ExecuteSacrifice(BodyPartType? specificPart = null, Vector2? customDirection = null)
        {
            if (_enemy == null || _enemy.IsDead) return false;

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

            partToSacrifice.Detach(-aimDir * 1.5f);

            SpawnLaunchedBodyPart(partToSacrifice.PartType, origin, aimDir);

            _regenQueue.Add(new RegeneratingLimb
            {
                Part = partToSacrifice,
                RegenTime = Time.time + _limbRegenDelay
            });

            if (_rb != null)
            {
                _rb.AddForce(-aimDir * _recoilImpulse, ForceMode2D.Impulse);
            }

            _enemy.GetComponent<EnemyAsciiView>()?.TriggerAttack();

            AsciiActionGame.Rendering.AsciiPostProcessing.TriggerScreenShake(0.28f);
            AsciiActionGame.Rendering.AsciiPostProcessing.TriggerGlitch(0.12f, 0.65f);

            OnLimbSacrificed?.Invoke(partToSacrifice, 1);
            return true;
        }

        private void SpawnLaunchedBodyPart(BodyPartType partType, Vector2 origin, Vector2 direction)
        {
            GameObject obj = new GameObject($"Launched_{partType}");
            obj.AddComponent<Rigidbody2D>();
            obj.AddComponent<CircleCollider2D>();
            var launched = obj.AddComponent<LaunchedBodyPart>();

            Color32 col = GetPartColor(partType);
            float travelDistance = _maxTravelDistance;
            if (_playerTransform != null)
            {
                float targetDist = Vector2.Distance(origin, _playerTransform.position);
                travelDistance = Mathf.Clamp(targetDist + 0.6f, 3.5f, _maxTravelDistance);
            }

            launched.Initialize(
                origin,
                direction,
                _launchSpeed,
                travelDistance,
                _blastRadius,
                _blastDamage,
                partType,
                col,
                gameObject
            );
        }

        private Color32 GetPartColor(BodyPartType type)
        {
            if (_palette == null)
            {
                var map = FindFirstObjectByType<World.AsciiWorldMap>();
                if (map != null) _palette = map.Palette;
                if (_palette == null) _palette = AsciiColorPalette.CreateDefault();
            }

            switch (type)
            {
                case BodyPartType.ChestArmor:
                    return _palette.EnemyArmor;
                case BodyPartType.LeftArm:
                case BodyPartType.RightArm:
                case BodyPartType.LeftLeg:
                case BodyPartType.RightLeg:
                    return _palette.EnemyLimb;
                default:
                    return _palette.EnemyCore;
            }
        }
    }
}
