using System;
using UnityEngine;
using AsciiActionGame.Core;
using AsciiActionGame.Enemies;
using AsciiActionGame.Rendering;

namespace AsciiActionGame.AI
{
    public class EnemyDecisionDirector : MonoBehaviour
    {
        public enum AIMode
        {
            ScriptedBaseline,
            MLAgentsReflex,
            GemmaStrategic,
            HybridGemmaPlusReflex
        }

        [Header("AI Orchestration Mode")]
        [SerializeField] private AIMode _aiMode = AIMode.HybridGemmaPlusReflex;

        [Header("Macro Decision Interval")]
        [SerializeField] private float _macroDecisionInterval = 1.8f;

        private EnemyEntity _enemy;
        private Rigidbody2D _rb;
        private LimbSacrificeAction _sacrificeAction;

        private IEnemyDecisionMaker _macroDecisionMaker;
        private EnemyMLAgent _reflexAgent;

        private AIDecisionResult _latestMacroDecision;
        private float _macroTimer = 0f;

        public AIMode ActiveAIMode => _aiMode;

        private void Awake()
        {
            _enemy = GetComponent<EnemyEntity>();
            _rb = GetComponent<Rigidbody2D>();
            _sacrificeAction = GetComponent<LimbSacrificeAction>();

            SetupDecisionEngines();
        }

        private void SetupDecisionEngines()
        {
            var gemma = GetComponent<GemmaEnemyAI>();
            if (gemma == null)
            {
                gemma = gameObject.AddComponent<GemmaEnemyAI>();
            }
            gemma.Initialize(_enemy);

            _macroDecisionMaker = gemma;

            _reflexAgent = GetComponent<EnemyMLAgent>();
            if (_reflexAgent == null)
            {
                _reflexAgent = gameObject.AddComponent<EnemyMLAgent>();
            }
            _reflexAgent.Initialize(_enemy);
        }

        private void Update()
        {
            if (_enemy == null || _enemy.IsDead) return;

            float dt = Time.deltaTime;

            if (_macroDecisionMaker != null) _macroDecisionMaker.Tick(dt);
            if (_reflexAgent != null) _reflexAgent.Tick(dt);

            _macroTimer += dt;
            if (_macroTimer >= _macroDecisionInterval)
            {
                _macroTimer = 0f;
                EvaluateMacroStrategy();
            }

            ApplyMicroReflexes();
        }

        private void EvaluateMacroStrategy()
        {
            EnemyGameState state = BuildGameStateSnapshot();

            switch (_aiMode)
            {
                case AIMode.ScriptedBaseline:
                    var scripted = new ScriptedEnemyAI();
                    scripted.Initialize(_enemy);
                    _latestMacroDecision = scripted.MakeDecision(state);
                    break;

                case AIMode.GemmaStrategic:
                case AIMode.HybridGemmaPlusReflex:
                    if (_macroDecisionMaker != null)
                    {
                        _latestMacroDecision = _macroDecisionMaker.MakeDecision(state);
                    }
                    break;

                case AIMode.MLAgentsReflex:
                    if (_reflexAgent != null)
                    {
                        _latestMacroDecision = _reflexAgent.MakeDecision(state);
                    }
                    break;
            }

            if (_latestMacroDecision.action == AIActionType.TeleportBehind && _enemy.CanTeleport)
            {
                var player = FindFirstObjectByType<Player.PlayerController>();
                if (player != null)
                {
                    _enemy.PerformTeleportBehind(player.transform);
                }
            }
            else if (_latestMacroDecision.action == AIActionType.DodgeDash && _enemy.CanDash)
            {
                var player = FindFirstObjectByType<Player.PlayerController>();
                Vector2 aim = player != null ? player.AimDirection : Vector2.right;
                Vector2 perp = new Vector2(-aim.y, aim.x);
                _enemy.PerformDodgeDash(perp);
            }
            else if (_latestMacroDecision.should_sacrifice && _sacrificeAction != null)
            {
                BodyPartType? partType = null;
                if (!string.IsNullOrEmpty(_latestMacroDecision.target_limb) &&
                    Enum.TryParse<BodyPartType>(_latestMacroDecision.target_limb, out var parsedType))
                {
                    partType = parsedType;
                }

                _sacrificeAction.ExecuteSacrifice(partType, _latestMacroDecision.desired_aim_direction);
            }
        }

        private void ApplyMicroReflexes()
        {
            if (_enemy.IsDashing) return;

            EnemyGameState state = BuildGameStateSnapshot();

            Vector2 moveDir = _latestMacroDecision.desired_move_direction;

            if ((_aiMode == AIMode.HybridGemmaPlusReflex || _aiMode == AIMode.MLAgentsReflex) && _reflexAgent != null)
            {
                AIDecisionResult reflexDecision = _reflexAgent.MakeDecision(state);

                if (reflexDecision.desired_move_direction.sqrMagnitude > 0.05f)
                {
                    moveDir = Vector2.Lerp(moveDir, reflexDecision.desired_move_direction, 0.75f);
                }
            }

            if (_rb != null && moveDir.sqrMagnitude > 0.01f)
            {
                float speed = _enemy.CalculateCurrentMoveSpeed();
                _rb.linearVelocity = Vector2.MoveTowards(_rb.linearVelocity, moveDir.normalized * speed, 35f * Time.deltaTime);
            }
        }

        private EnemyGameState BuildGameStateSnapshot()
        {
            var player = FindFirstObjectByType<Player.PlayerController>();
            Vector2 playerPos = player != null ? player.Position : Vector2.zero;
            float dist = player != null ? Vector2.Distance(_enemy.Position, playerPos) : 10f;

            var limbs = _enemy.GetActiveBodyParts();
            string[] limbNames = new string[limbs.Count];
            for (int i = 0; i < limbs.Count; i++)
            {
                limbNames[i] = limbs[i].PartType.ToString();
            }

            float hp = _enemy.CorePart != null && _enemy.CorePart.Damageable != null ? _enemy.CorePart.Damageable.CurrentHealth : 100f;

            return new EnemyGameState
            {
                enemy_health = hp,
                player_dist = dist,
                player_weapon = "EraserBeam",
                intact_limbs = limbNames,
                can_sacrifice = _sacrificeAction != null && _sacrificeAction.CanSacrificeLimb(),
                enemy_position = _enemy.Position,
                player_position = playerPos
            };
        }
    }
}
