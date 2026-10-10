using System.Collections.Generic;
using UnityEngine;
using AsciiActionGame.Core;
using AsciiActionGame.Rendering;

namespace AsciiActionGame.Weapons
{
    public class EraserBeamVfx : MonoBehaviour
    {
        public static EraserBeamVfx Instance { get; private set; }

        [Header("Beam Style")]
        [SerializeField] private float _defaultDuration = 0.08f;
        [SerializeField] private float _stepDistance = 0.35f;

        [Header("Palette")]
        [SerializeField] private AsciiColorPalette _palette;

        private readonly List<ActiveBeam> _beams = new List<ActiveBeam>();

        private struct ActiveBeam
        {
            public Vector2 StartPos;
            public Vector2 EndPos;
            public float RemainingTime;
            public float MaxTime;
            public Color32 Color;
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }

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

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
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
            for (int i = _beams.Count - 1; i >= 0; i--)
            {
                var beam = _beams[i];
                beam.RemainingTime -= dt;
                if (beam.RemainingTime <= 0f)
                {
                    _beams.RemoveAt(i);
                }
                else
                {
                    _beams[i] = beam;
                }
            }
        }

        public void ShowBeam(Vector2 startPos, Vector2 endPos, float duration = -1f, Color32? color = null)
        {
            float maxTime = duration > 0f ? duration : _defaultDuration;
            Color32 beamColor = color ?? (_palette != null ? _palette.EraserBeam : new Color32(120, 255, 180, 255));

            _beams.Add(new ActiveBeam
            {
                StartPos = startPos,
                EndPos = endPos,
                RemainingTime = maxTime,
                MaxTime = maxTime,
                Color = beamColor
            });
        }

        private void OnRenderAscii(IAsciiRenderer renderer)
        {
            foreach (var beam in _beams)
            {
                RenderSingleBeam(renderer, beam);
            }
        }

        private void RenderSingleBeam(IAsciiRenderer renderer, ActiveBeam beam)
        {
            Vector2 delta = beam.EndPos - beam.StartPos;
            float totalDist = delta.magnitude;
            if (totalDist < 0.05f) return;

            Vector2 dir = delta / totalDist;
            char beamChar = DetermineBeamGlyph(dir);

            float alphaRatio = Mathf.Clamp01(beam.RemainingTime / beam.MaxTime);
            Color32 currentColor = new Color32(
                beam.Color.r,
                beam.Color.g,
                beam.Color.b,
                (byte)(beam.Color.a * alphaRatio)
            );

            renderer.DrawWorldGlyph(
                beam.StartPos,
                GameConstants.GLYPH_BEAM_CORE,
                currentColor,
                Color.clear,
                GameConstants.LAYER_PROJECTILES
            );

            float step = (renderer != null && renderer.CellWidth > 0f) ? Mathf.Min(_stepDistance, renderer.CellWidth * 0.95f) : _stepDistance;
            int stepCount = Mathf.FloorToInt(totalDist / step);
            for (int i = 1; i < stepCount; i++)
            {
                Vector2 samplePos = beam.StartPos + dir * (i * step);
                renderer.DrawWorldGlyph(
                    samplePos,
                    beamChar,
                    currentColor,
                    Color.clear,
                    GameConstants.LAYER_PROJECTILES
                );
            }

            renderer.DrawWorldGlyph(
                beam.EndPos,
                GameConstants.GLYPH_FRAGMENT,
                currentColor,
                Color.clear,
                GameConstants.LAYER_PROJECTILES
            );
        }

        private char DetermineBeamGlyph(Vector2 direction)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            if (angle < 0) angle += 360f;

            if ((angle >= 337.5f || angle < 22.5f) || (angle >= 157.5f && angle < 202.5f))
            {
                return GameConstants.GLYPH_BEAM_HORIZONTAL;
            }
            if ((angle >= 67.5f && angle < 112.5f) || (angle >= 247.5f && angle < 292.5f))
            {
                return GameConstants.GLYPH_BEAM_VERTICAL;
            }
            if ((angle >= 22.5f && angle < 67.5f) || (angle >= 202.5f && angle < 247.5f))
            {
                return GameConstants.GLYPH_BEAM_DIAGONAL_FWD;
            }

            return GameConstants.GLYPH_BEAM_DIAGONAL_BACK;
        }
    }
}
