using UnityEngine;
using AsciiActionGame.Core;
using AsciiActionGame.Rendering;
using AsciiActionGame.Combat;
using AsciiActionGame.Animation;

namespace AsciiActionGame.Enemies
{
    [RequireComponent(typeof(EnemyEntity))]
    public class EnemyAsciiView : MonoBehaviour
    {
        [Header("Custom Animation Clips (Aseprite / Sprites / Text)")]
        [Tooltip("Optional custom Idle animation clip.")]
        [SerializeField] private AsciiAnimationClip _clipIdle;
        [Tooltip("Optional custom Move / Patrol animation clip.")]
        [SerializeField] private AsciiAnimationClip _clipMove;
        [Tooltip("Optional custom Attack telegraph / firing clip.")]
        [SerializeField] private AsciiAnimationClip _clipAttack;
        [Tooltip("Optional custom Death explosion clip.")]
        [SerializeField] private AsciiAnimationClip _clipDeath;

        [Header("Palette")]
        [SerializeField] private AsciiColorPalette _palette;

        [Header("Hit Flash Colors")]
        [SerializeField] private Color32 _flashColor = new Color32(255, 255, 255, 255);

        private EnemyEntity _enemy;
        private float _animTime = 0f;
        private float _attackTimer = 0f;
        private float _deathTimer = 0f;
        private const float ATTACK_DURATION = 0.35f;

        private void Awake()
        {
            _enemy = GetComponent<EnemyEntity>();

            if (_palette == null)
            {
                var map = FindFirstObjectByType<World.AsciiWorldMap>();
                if (map != null && map.Palette != null)
                {
                    _palette = map.Palette;
                }
                else
                {
                    _palette = AsciiColorPalette.CreateDefault();
                }
            }
        }

        private void OnEnable()
        {
            AsciiGridRenderer.OnRenderDynamicEntities += OnRenderAscii;
        }

        private void OnDisable()
        {
            AsciiGridRenderer.OnRenderDynamicEntities -= OnRenderAscii;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _animTime += dt;

            if (_attackTimer > 0f)
            {
                _attackTimer -= dt;
            }

            if (_enemy.IsDead)
            {
                _deathTimer += dt;
            }
        }

        public void TriggerAttack()
        {
            _attackTimer = ATTACK_DURATION;
        }

        private void OnRenderAscii(IAsciiRenderer renderer)
        {
            if (_enemy == null) return;

            if (HasCustomClips())
            {
                RenderCustomAnimation(renderer);
            }
            else
            {
                if (_enemy.IsDead)
                {
                    RenderDeathDisintegration(renderer);
                    return;
                }

                RenderBigEnemyCombatMech(renderer);
            }
        }

        private bool HasCustomClips()
        {
            return _clipIdle != null || _clipMove != null || _clipAttack != null || _clipDeath != null;
        }

        private void RenderCustomAnimation(IAsciiRenderer renderer)
        {
            AsciiAnimationClip activeClip = _clipIdle;

            if (_enemy.IsDead && _clipDeath != null)
            {
                activeClip = _clipDeath;
            }
            else if (_attackTimer > 0f && _clipAttack != null)
            {
                activeClip = _clipAttack;
            }
            else if (_clipMove != null)
            {
                var rb = GetComponent<Rigidbody2D>();
                if (rb != null && rb.linearVelocity.sqrMagnitude > 0.05f)
                {
                    activeClip = _clipMove;
                }
            }

            if (activeClip == null) return;

            var frame = activeClip.Evaluate(_animTime);
            if (frame == null) return;

            Vector2 centerPos = _enemy.Position;
            float cellW = renderer.CellWidth > 0f ? renderer.CellWidth : 0.25f;
            float cellH = renderer.CellHeight > 0f ? renderer.CellHeight : 0.25f;

            int halfW = frame.Width / 2;
            int halfH = frame.Height / 2;

            bool isFlashing = false;
            foreach (var part in _enemy.BodyParts)
            {
                if (part != null && part.Damageable != null && part.Damageable.IsFlashing)
                {
                    isFlashing = true;
                    break;
                }
            }

            for (int y = 0; y < frame.Height; y++)
            {
                for (int x = 0; x < frame.Width; x++)
                {
                    char g = frame.GetGlyph(x, y);
                    if (g == ' ' || g == '\0') continue;

                    Color32 col = frame.GetColor(x, y);
                    if (col.a == 0) continue;

                    if (isFlashing) col = _flashColor;

                    float offsetX = (x - halfW) * cellW;
                    float offsetY = (halfH - y) * cellH;

                    renderer.DrawWorldGlyph(
                        centerPos + new Vector2(offsetX, offsetY),
                        g,
                        col,
                        Color.clear,
                        GameConstants.LAYER_ENEMY_CORE
                    );
                }
            }
        }

        /// <summary>
        /// Renders a menacing 22-character heavy combat battle mech whose visual armor,
        /// weapons, and treads accurately reflect modular limb damage and sacrifice.
        /// </summary>
        private void RenderBigEnemyCombatMech(IAsciiRenderer renderer)
        {
            Vector2 enemyPos = _enemy.Position;
            float cellW = renderer.CellWidth > 0f ? renderer.CellWidth : 0.25f;
            float cellH = renderer.CellHeight > 0f ? renderer.CellHeight : 0.25f;

            bool hasCore = false, hasChest = false, hasLeftArm = false, hasRightArm = false, hasLeftLeg = false, hasRightLeg = false;
            bool coreFlash = false, chestFlash = false, leftArmFlash = false, rightArmFlash = false, leftLegFlash = false, rightLegFlash = false;

            foreach (var part in _enemy.BodyParts)
            {
                if (part == null || !part.IsActivePart) continue;
                bool flashing = part.Damageable != null && part.Damageable.IsFlashing;

                switch (part.PartType)
                {
                    case BodyPartType.Core:
                        hasCore = true;
                        coreFlash = flashing;
                        break;
                    case BodyPartType.ChestArmor:
                        hasChest = true;
                        chestFlash = flashing;
                        break;
                    case BodyPartType.LeftArm:
                        hasLeftArm = true;
                        leftArmFlash = flashing;
                        break;
                    case BodyPartType.RightArm:
                        hasRightArm = true;
                        rightArmFlash = flashing;
                        break;
                    case BodyPartType.LeftLeg:
                        hasLeftLeg = true;
                        leftLegFlash = flashing;
                        break;
                    case BodyPartType.RightLeg:
                        hasRightLeg = true;
                        rightLegFlash = flashing;
                        break;
                }
            }

            Color32 coreColor = coreFlash ? _flashColor : _palette.EnemyCore;
            Color32 chestColor = chestFlash ? _flashColor : _palette.EnemyArmor;
            Color32 leftArmColor = leftArmFlash ? _flashColor : _palette.EnemyLimb;
            Color32 rightArmColor = rightArmFlash ? _flashColor : _palette.EnemyLimb;
            Color32 leftLegColor = leftLegFlash ? _flashColor : _palette.EnemyLimb;
            Color32 rightLegColor = rightLegFlash ? _flashColor : _palette.EnemyLimb;

            void Put(float dx, float dy, char glyph, Color32 color, int layer)
            {
                renderer.DrawWorldGlyph(
                    enemyPos + new Vector2(dx * cellW, dy * cellH),
                    glyph,
                    color,
                    Color.clear,
                    layer
                );
            }

            // === ROW +2: Heavy Threat Cowl & Targeting Horns (5 chars) ===
            if (hasLeftArm)   Put(-2f, 2f, '<', leftArmColor, GameConstants.LAYER_ENEMY_LIMB);
            if (hasChest)     Put(-1f, 2f, '[', chestColor, GameConstants.LAYER_ENEMY_ARMOR);
            if (hasCore)      Put( 0f, 2f, 'X', coreColor, GameConstants.LAYER_ENEMY_CORE + 1); // Threat eye
            if (hasChest)     Put( 1f, 2f, ']', chestColor, GameConstants.LAYER_ENEMY_ARMOR);
            if (hasRightArm)  Put( 2f, 2f, '>', rightArmColor, GameConstants.LAYER_ENEMY_LIMB);

            // === ROW +1: Armored Torso & Shoulder Cannons (5 chars) ===
            if (hasLeftArm)   Put(-2f, 1f, '{', leftArmColor, GameConstants.LAYER_ENEMY_LIMB + 1);
            if (hasChest)     Put(-1f, 1f, '#', chestColor, GameConstants.LAYER_ENEMY_ARMOR);
            if (hasChest)     Put( 0f, 1f, '%', chestColor, GameConstants.LAYER_ENEMY_ARMOR + 1);
            if (hasChest)     Put( 1f, 1f, '#', chestColor, GameConstants.LAYER_ENEMY_ARMOR);
            if (hasRightArm)  Put( 2f, 1f, '}', rightArmColor, GameConstants.LAYER_ENEMY_LIMB + 1);

            // === ROW 0: Reactor Core & Battle Pincers (5 chars) ===
            if (hasLeftArm)   Put(-2f, 0f, '/', leftArmColor, GameConstants.LAYER_ENEMY_LIMB);
            if (hasChest)     Put(-1f, 0f, '[', chestColor, GameConstants.LAYER_ENEMY_ARMOR);
            if (hasCore)
            {
                char coreG = (Mathf.Sin(Time.time * 8f) > 0f) ? '*' : '#';
                Put(0f, 0f, coreG, coreColor, GameConstants.LAYER_ENEMY_CORE + 2); // Pulsing core
            }
            if (hasChest)     Put( 1f, 0f, ']', chestColor, GameConstants.LAYER_ENEMY_ARMOR);
            if (hasRightArm)  Put( 2f, 0f, '\\', rightArmColor, GameConstants.LAYER_ENEMY_LIMB);

            // === ROW -1: Reinforced Pelvis & Chassis (3 chars) ===
            if (hasChest)
            {
                Put(-1f, -1f, '/', chestColor, GameConstants.LAYER_ENEMY_ARMOR);
                Put( 0f, -1f, '#', chestColor, GameConstants.LAYER_ENEMY_ARMOR);
                Put( 1f, -1f, '\\', chestColor, GameConstants.LAYER_ENEMY_ARMOR);
            }

            // === ROW -2: Hydraulic Struts / Heavy Treads (4 chars) ===
            if (hasLeftLeg)
            {
                Put(-2f, -2f, '[', leftLegColor, GameConstants.LAYER_ENEMY_LIMB);
                Put(-1f, -2f, '|', leftLegColor, GameConstants.LAYER_ENEMY_LIMB);
            }
            if (hasRightLeg)
            {
                Put( 1f, -2f, '|', rightLegColor, GameConstants.LAYER_ENEMY_LIMB);
                Put( 2f, -2f, ']', rightLegColor, GameConstants.LAYER_ENEMY_LIMB);
            }

            // Attack flash when telegraphing
            if (_attackTimer > 0f)
            {
                Put(0f, 3f, '!', Color.yellow, GameConstants.LAYER_VFX_PARTICLES);
            }
        }

        private void RenderDeathDisintegration(IAsciiRenderer renderer)
        {
            Vector2 pos = _enemy.Position;
            float progress = Mathf.Clamp01(_deathTimer / 0.7f);
            byte alpha = (byte)Mathf.Clamp(255 * (1f - progress), 0, 255);
            Color32 debrisColor = new Color32(_palette.EnemyFragment.r, _palette.EnemyFragment.g, _palette.EnemyFragment.b, alpha);

            char[] scatterGlyphs = { '*', '%', '#', 'x', '+' };

            for (int i = 0; i < 8; i++)
            {
                float angle = (i * 45f + _deathTimer * 200f) * Mathf.Deg2Rad;
                float dist = progress * 1.8f;
                Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;

                char g = scatterGlyphs[(i + Mathf.FloorToInt(_deathTimer * 10f)) % scatterGlyphs.Length];
                renderer.DrawWorldGlyph(pos + offset, g, debrisColor, Color.clear, GameConstants.LAYER_VFX_PARTICLES);
            }
        }
    }
}
