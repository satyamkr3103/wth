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

                RenderIntactBodyParts(renderer);
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
            float cellW = renderer.CellWidth > 0f ? renderer.CellWidth : 0.22f;
            float cellH = renderer.CellHeight > 0f ? renderer.CellHeight : 0.22f;

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

        private void RenderIntactBodyParts(IAsciiRenderer renderer)
        {
            Vector2 enemyPos = _enemy.Position;
            Vector2 facing = _enemy.FacingDirection.sqrMagnitude > 0.01f ? _enemy.FacingDirection.normalized : Vector2.up;

            float cellW = renderer.CellWidth > 0f ? renderer.CellWidth : 0.22f;
            float cellH = renderer.CellHeight > 0f ? renderer.CellHeight : 0.22f;

            foreach (var part in _enemy.BodyParts)
            {
                if (part == null || !part.IsActivePart) continue;

                Vector2 partWorldPos = part.GetWorldPosition(enemyPos, facing);
                Color32 color = GetPartColor(part.PartType);
                int layer = GetPartLayer(part.PartType);

                if (part.Damageable != null && part.Damageable.IsFlashing)
                {
                    color = _flashColor;
                }

                switch (part.PartType)
                {
                    case BodyPartType.Core:
                        renderer.DrawWorldGlyph(partWorldPos, 'X', color, Color.clear, layer + 2);
                        renderer.DrawWorldGlyph(partWorldPos + new Vector2(-cellW * 0.9f, 0f), '[', color, Color.clear, layer + 1);
                        renderer.DrawWorldGlyph(partWorldPos + new Vector2(cellW * 0.9f, 0f), ']', color, Color.clear, layer + 1);
                        break;

                    case BodyPartType.ChestArmor:
                        renderer.DrawWorldGlyph(partWorldPos, '#', color, Color.clear, layer + 1);
                        renderer.DrawWorldGlyph(partWorldPos + new Vector2(-cellW * 0.8f, 0f), '#', color, Color.clear, layer);
                        renderer.DrawWorldGlyph(partWorldPos + new Vector2(cellW * 0.8f, 0f), '#', color, Color.clear, layer);
                        break;

                    case BodyPartType.LeftArm:
                        renderer.DrawWorldGlyph(partWorldPos, '{', color, Color.clear, layer + 1);
                        renderer.DrawWorldGlyph(partWorldPos + new Vector2(cellW * 0.7f, 0f), '=', color, Color.clear, layer);
                        break;

                    case BodyPartType.RightArm:
                        renderer.DrawWorldGlyph(partWorldPos, '}', color, Color.clear, layer + 1);
                        renderer.DrawWorldGlyph(partWorldPos + new Vector2(-cellW * 0.7f, 0f), '=', color, Color.clear, layer);
                        break;

                    case BodyPartType.LeftLeg:
                    case BodyPartType.RightLeg:
                        renderer.DrawWorldGlyph(partWorldPos, '|', color, Color.clear, layer);
                        renderer.DrawWorldGlyph(partWorldPos + new Vector2(0f, -cellH * 0.7f), '/', color, Color.clear, layer);
                        break;

                    default:
                        renderer.DrawWorldGlyph(partWorldPos, part.Glyph, color, Color.clear, layer);
                        break;
                }
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

        private Color32 GetPartColor(BodyPartType type)
        {
            switch (type)
            {
                case BodyPartType.Core:
                    return _palette.EnemyCore;
                case BodyPartType.ChestArmor:
                    return _palette.EnemyArmor;
                case BodyPartType.LeftArm:
                case BodyPartType.RightArm:
                case BodyPartType.LeftLeg:
                case BodyPartType.RightLeg:
                default:
                    return _palette.EnemyLimb;
            }
        }

        private int GetPartLayer(BodyPartType type)
        {
            switch (type)
            {
                case BodyPartType.Core:
                    return GameConstants.LAYER_ENEMY_CORE;
                case BodyPartType.ChestArmor:
                    return GameConstants.LAYER_ENEMY_ARMOR;
                case BodyPartType.LeftArm:
                case BodyPartType.RightArm:
                case BodyPartType.LeftLeg:
                case BodyPartType.RightLeg:
                default:
                    return GameConstants.LAYER_ENEMY_LIMB;
            }
        }
    }
}
