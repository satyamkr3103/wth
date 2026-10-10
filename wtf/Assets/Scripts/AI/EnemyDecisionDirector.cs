using System;
using UnityEngine;
using AsciiActionGame.Enemies;
using AsciiActionGame.Rendering;
using AsciiActionGame.Core;

namespace AsciiActionGame.AI
{
    public enum AIMode
    {
        ScriptedBaseline,
        GemmaStrategic,
        MLAgentsReflex,
        HybridGemmaPlusReflex
    }

    [RequireComponent(typeof(EnemyEntity))]
    public class EnemyDecisionDirector : MonoBehaviour
    {
        [Header("AI Orchestration Mode")]
        [SerializeField] private AIMode _aiMode = AIMode.HybridGemmaPlusReflex;

        [Header("Macro Decision Interval")]
        [SerializeField] private float _macroDecisionInterval = 1.8f;

        [Header("Dialogue HUD")]
        [SerializeField] private bool _renderAsciiDialogueBark = true;
        [SerializeField] private float _barkDuration = 2.5f;

        private EnemyEntity _enemy;
        private Rigidbody2D _rb;
        private LimbSacrificeAction _sacrificeAction;

        private IEnemyDecisionMaker _macroDecisionMaker;
        private EnemyMLAgent _reflexAgent;

        private AIDecisionResult _latestMacroDecision;
        private float _macroTimer = 0f;
        private string _activeBarkText = "";
        private float _barkTimer = 0f;

        public AIMode ActiveAIMode => _aiMode;
        public string ActiveBarkText => _activeBarkText;

        public event Action<string> OnCyberDialogueBroadcast;

        private void Awake()
        {
            _enemy = GetComponent<EnemyEntity>();
            _rb = GetComponent<Rigidbody2D>();
            _sacrificeAction = GetComponent<LimbSacrificeAction>();

            SetupDecisionEngines();
        }

        private void OnEnable()
        {
            if (_renderAsciiDialogueBark)
            {
                AsciiGridRenderer.OnRenderDynamicEntities += RenderDialogueBark;
            }
        }

        private void OnDisable()
        {
            if (_renderAsciiDialogueBark)
            {
                AsciiGridRenderer.OnRenderDynamicEntities -= RenderDialogueBark;
            }
        }

        private void SetupDecisionEngines()
        {
            var gemma = GetComponent<GemmaEnemyAI>();
            if (gemma == null)
            {
                gemma = gameObject.AddComponent<GemmaEnemyAI>();
            }
            gemma.Initialize(_enemy);
            gemma.OnGemmaDialogueReceived += HandleDialogueReceived;

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
            if (_enemy.IsDead) return;

            float dt = Time.deltaTime;

            if (_barkTimer > 0f)
            {
                _barkTimer -= dt;
                if (_barkTimer <= 0f)
                {
                    _activeBarkText = "";
                }
            }

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

            if (_latestMacroDecision.should_sacrifice && _sacrificeAction != null)
            {
                BodyPartType? partType = null;
                if (!string.IsNullOrEmpty(_latestMacroDecision.target_limb) &&
                    Enum.TryParse<BodyPartType>(_latestMacroDecision.target_limb, out var parsedType))
                {
                    partType = parsedType;
                }

                _sacrificeAction.ExecuteSacrifice(partType, _latestMacroDecision.desired_aim_direction);
            }

            if (!string.IsNullOrEmpty(_latestMacroDecision.dialogue))
            {
                SetDialogueBark(_latestMacroDecision.dialogue);
            }
        }

        private void ApplyMicroReflexes()
        {
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

        private void HandleDialogueReceived(string dialogue)
        {
            SetDialogueBark(dialogue);
        }

        public void SetDialogueBark(string bark)
        {
            _activeBarkText = bark;
            _barkTimer = _barkDuration;
            OnCyberDialogueBroadcast?.Invoke(bark);
        }

        private void RenderDialogueBark(IAsciiRenderer renderer)
        {
            if (string.IsNullOrEmpty(_activeBarkText) || _enemy.IsDead) return;

            Vector2 barkPos = _enemy.Position + new Vector2(-(_activeBarkText.Length * 0.15f), 1.1f);
            Color32 barkColor = new Color32(0, 240, 255, 255);

            for (int i = 0; i < _activeBarkText.Length && i < 28; i++)
            {
                renderer.DrawWorldGlyph(
                    barkPos + new Vector2(i * 0.35f, 0f),
                    _activeBarkText[i],
                    barkColor,
                    Color.clear,
                    GameConstants.LAYER_UI_OVERLAY
                );
            }
        }
    }
}
