using System;
using System.Collections.Generic;
using UnityEngine;
using AsciiActionGame.Enemies;

namespace AsciiActionGame.AI
{
    public enum AIActionType
    {
        Idle,
        Advance,
        Strafe,
        Retreat,
        SacrificeLimb,
        Ambush,
        Reposition,
        DodgeDash,
        TeleportBehind
    }

    [Serializable]
    public struct EnemyGameState
    {
        public float enemy_health;
        public float player_dist;
        public string player_weapon;
        public string[] intact_limbs;
        public bool can_sacrifice;
        public Vector2 enemy_position;
        public Vector2 player_position;
    }

    [Serializable]
    public struct AIDecisionResult
    {
        public AIActionType action;
        public string target_limb;
        public string attack_type;
        public string dialogue;
        public Vector2 desired_move_direction;
        public Vector2 desired_aim_direction;
        public bool should_sacrifice;

        public static AIDecisionResult Default => new AIDecisionResult
        {
            action = AIActionType.Advance,
            target_limb = "",
            attack_type = "Standard",
            dialogue = "",
            desired_move_direction = Vector2.zero,
            desired_aim_direction = Vector2.zero,
            should_sacrifice = false
        };
    }

    public interface IEnemyDecisionMaker
    {
        string DecisionMakerName { get; }
        bool IsReady { get; }

        void Initialize(EnemyEntity enemy);
        AIDecisionResult MakeDecision(EnemyGameState context);
        void Tick(float deltaTime);
    }
}
