using UnityEngine;
using AsciiActionGame.Enemies;

namespace AsciiActionGame.AI
{
    public class ScriptedEnemyAI : IEnemyDecisionMaker
    {
        public string DecisionMakerName => "Scripted Heuristic AI";
        public bool IsReady => true;

        private EnemyEntity _enemy;
        private float _strafeDirection = 1f;
        private float _strafeTimer = 0f;

        public void Initialize(EnemyEntity enemy)
        {
            _enemy = enemy;
            _strafeDirection = Random.value > 0.5f ? 1f : -1f;
        }

        public void Tick(float deltaTime)
        {
            _strafeTimer += deltaTime;
            if (_strafeTimer >= 2.5f)
            {
                _strafeTimer = 0f;
                _strafeDirection *= -1f;
            }
        }

        public AIDecisionResult MakeDecision(EnemyGameState context)
        {
            AIDecisionResult result = AIDecisionResult.Default;

            Vector2 toPlayer = context.player_position - context.enemy_position;
            float dist = toPlayer.magnitude;
            Vector2 normToPlayer = dist > 0.01f ? toPlayer / dist : Vector2.down;
            Vector2 perpendicular = new Vector2(-normToPlayer.y, normToPlayer.x) * _strafeDirection;

            result.desired_aim_direction = normToPlayer;

            if (context.enemy_health <= 50f && context.can_sacrifice && dist <= 8.5f)
            {
                result.action = AIActionType.SacrificeLimb;
                result.should_sacrifice = true;
                result.attack_type = "HIGH_SPREAD_SHRAPNEL";

                string targetLimb = "LeftArm";
                if (context.intact_limbs != null && context.intact_limbs.Length > 0)
                {
                    targetLimb = context.intact_limbs[0];
                }

                result.target_limb = targetLimb;
                result.dialogue = $"SYS: HULL COMPROMISED. SACRIFICING {targetLimb.ToUpper()} FOR SHRAPNEL VOLLEY.";
                result.desired_move_direction = -normToPlayer * 0.5f;
                return result;
            }

            if (dist > 7.5f)
            {
                result.action = AIActionType.Advance;
                result.desired_move_direction = (normToPlayer * 0.85f) + (perpendicular * 0.25f);
                result.dialogue = "TACTICAL: PURSUING TARGET VECTOR.";
            }
            else if (dist < 2.5f)
            {
                result.action = AIActionType.Retreat;
                result.desired_move_direction = (-normToPlayer * 0.8f) + (perpendicular * 0.35f);
                result.dialogue = "TACTICAL: PROXIMITY ALERT. BACKSTEPPING.";
            }
            else
            {
                result.action = AIActionType.Strafe;
                result.desired_move_direction = (perpendicular * 0.9f) + (normToPlayer * 0.15f);
                result.dialogue = "TACTICAL: ENGAGING EVASIVE ORBITAL STRAFE.";
            }

            return result;
        }
    }
}
