using UnityEngine;
using AsciiActionGame.Enemies;
using AsciiActionGame.Projectiles;

#if ENABLE_ML_AGENTS
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
#endif

namespace AsciiActionGame.AI
{
    public class EnemyMLAgent :
#if ENABLE_ML_AGENTS
        Agent,
#else
        MonoBehaviour,
#endif
        IEnemyDecisionMaker
    {
        [Header("Reflex Parameters")]
        [SerializeField] private float _evasionSensorRadius = 5.0f;
        [SerializeField] private float _evasionStrength = 1.4f;
        [SerializeField] private float _predictiveAimLead = 0.25f;

        public string DecisionMakerName => "ML-Agents Reflex Policy";
        public bool IsReady => true;

        private EnemyEntity _enemy;
        private Rigidbody2D _rb;

        private void Awake()
        {
            _enemy = GetComponent<EnemyEntity>();
            _rb = GetComponent<Rigidbody2D>();
        }

        public void Initialize(EnemyEntity enemy)
        {
            _enemy = enemy;
            _rb = enemy.GetComponent<Rigidbody2D>();
        }

        public void Tick(float deltaTime)
        {
        }

        public AIDecisionResult MakeDecision(EnemyGameState context)
        {
            AIDecisionResult result = AIDecisionResult.Default;

            Vector2 toPlayer = context.player_position - context.enemy_position;
            float dist = toPlayer.magnitude;
            Vector2 normToPlayer = dist > 0.01f ? toPlayer / dist : Vector2.down;

            Vector2 playerVel = Vector2.zero;
            var player = FindFirstObjectByType<Player.PlayerController>();
            if (player != null)
            {
                playerVel = player.Velocity;
            }

            Vector2 leadPos = context.player_position + (playerVel * _predictiveAimLead);
            result.desired_aim_direction = (leadPos - context.enemy_position).normalized;

            Vector2 evasionForce = CalculateEvasionVector(context.enemy_position);

            Vector2 baseMove = normToPlayer * 0.7f;
            if (dist < 3f) baseMove = -normToPlayer * 0.6f;

            result.desired_move_direction = (baseMove + (evasionForce * _evasionStrength)).normalized;
            result.action = evasionForce.sqrMagnitude > 0.1f ? AIActionType.Strafe : AIActionType.Advance;

            return result;
        }

        private Vector2 CalculateEvasionVector(Vector2 enemyPos)
        {
            Vector2 evasion = Vector2.zero;

            var projectiles = FindObjectsByType<FragmentProjectile>(FindObjectsSortMode.None);
            float nearestDist = _evasionSensorRadius;

            foreach (var proj in projectiles)
            {
                if (proj == null || !proj.IsActive) continue;

                Vector2 projPos = proj.Position;
                float d = Vector2.Distance(enemyPos, projPos);
                if (d < nearestDist)
                {
                    nearestDist = d;
                    Vector2 away = (enemyPos - projPos).normalized;
                    evasion += away * (1f - (d / _evasionSensorRadius));
                }
            }

            return evasion;
        }

#if ENABLE_ML_AGENTS
        public override void CollectObservations(VectorSensor sensor)
        {
            if (_enemy == null) return;

            var player = FindFirstObjectByType<Player.PlayerController>();
            Vector2 relPlayerPos = player != null ? (player.Position - _enemy.Position) : Vector2.zero;
            Vector2 playerVel = player != null ? player.Velocity : Vector2.zero;

            sensor.AddObservation(relPlayerPos);
            sensor.AddObservation(playerVel);
            sensor.AddObservation(_enemy.CorePart != null ? _enemy.CorePart.Damageable.CurrentHealth / 100f : 0f);
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            var continuousActions = actions.ContinuousActions;
            Vector2 moveDir = new Vector2(continuousActions[0], continuousActions[1]);
        }
#endif
    }
}
