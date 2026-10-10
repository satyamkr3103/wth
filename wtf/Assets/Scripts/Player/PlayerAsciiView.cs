using System.Collections.Generic;
using UnityEngine;
using AsciiActionGame.Core;
using AsciiActionGame.Rendering;
using AsciiActionGame.Combat;
using AsciiActionGame.Animation;

namespace AsciiActionGame.Player
{
    [RequireComponent(typeof(PlayerController))]
    public class PlayerAsciiView : MonoBehaviour
    {
        [Header("Custom Animation Clips (Aseprite / Sprites / Text)")]
        [Tooltip("Idle animation facing Down / South.")]
        [SerializeField] private AsciiAnimationClip _clipIdleDown;
        [Tooltip("Idle animation facing Up / North.")]
        [SerializeField] private AsciiAnimationClip _clipIdleUp;
        [Tooltip("Idle animation facing Side (flips automatically for Left).")]
        [SerializeField] private AsciiAnimationClip _clipIdleSide;

        [Tooltip("Running / Walking animation facing Down / South.")]
        [SerializeField] private AsciiAnimationClip _clipRunDown;
        [Tooltip("Running / Walking animation facing Up / North.")]
        [SerializeField] private AsciiAnimationClip _clipRunUp;
        [Tooltip("Running / Walking animation facing Side (flips automatically for Left).")]
        [SerializeField] private AsciiAnimationClip _clipRunSide;

        [Tooltip("Weapon firing / attack recoil animation clip.")]
        [SerializeField] private AsciiAnimationClip _clipFire;
        [Tooltip("Death dissolution animation clip.")]
        [SerializeField] private AsciiAnimationClip _clipDeath;

        [Header("Built-in Procedural Fallback Silhouette")]
        [SerializeField] private char _coreGlyph = '@';
        [SerializeField] private bool _drawStabilizers = true;

        [Header("Trail Settings")]
        [SerializeField] private bool _enableTrail = true;
        [SerializeField] private int _maxTrailPoints = 8;
        [SerializeField] private float _trailSpawnDistance = 0.25f;
        [SerializeField] private float _trailLifetime = 0.28f;

        [Header("Palette Reference")]
        [SerializeField] private AsciiColorPalette _palette;

        private PlayerController _controller;
        private Damageable _damageable;
        private IAsciiRenderer _renderer;
        private readonly List<TrailPoint> _trailPoints = new List<TrailPoint>();
        private Vector2 _lastTrailPos;

        private float _animTime = 0f;
        private float _fireTimer = 0f;
        private const float FIRE_DURATION = 0.12f;

        private struct TrailPoint
        {
            public Vector2 Position;
            public float RemainingTime;
            public float MaxTime;
            public char Glyph;
        }

        private void Awake()
        {
            _controller = GetComponent<PlayerController>();
            _damageable = GetComponent<Damageable>();
            _renderer = FindFirstObjectByType<AsciiGridRenderer>();

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
            AsciiGridRenderer.OnRenderDynamicEntities += OnRenderDynamic;
        }

        private void OnDisable()
        {
            AsciiGridRenderer.OnRenderDynamicEntities -= OnRenderDynamic;
        }

        private void Start()
        {
            _lastTrailPos = _controller.Position;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _animTime += dt;

            if (_fireTimer > 0f)
            {
                _fireTimer -= dt;
            }

            UpdateTrail();
        }

        public void TriggerFire()
        {
            _fireTimer = FIRE_DURATION;
        }

        private void OnRenderDynamic(IAsciiRenderer renderer)
        {
            RenderTrail(renderer);

            if (HasCustomClips())
            {
                RenderCustomAnimationClip(renderer);
            }
            else
            {
                RenderProceduralSilhouette(renderer);
            }
        }

        private bool HasCustomClips()
        {
            return _clipIdleDown != null || _clipIdleUp != null || _clipIdleSide != null ||
                   _clipRunDown != null || _clipRunUp != null || _clipRunSide != null;
        }

        private void RenderCustomAnimationClip(IAsciiRenderer renderer)
        {
            Vector2 aim = _controller.AimDirection.normalized;
            bool isMoving = _controller.IsMoving;
            bool isFacingLeft = aim.x < -0.1f;

            AsciiAnimationClip activeClip = null;

            if (_damageable != null && _damageable.IsDead && _clipDeath != null)
            {
                activeClip = _clipDeath;
            }
            else if (_fireTimer > 0f && _clipFire != null)
            {
                activeClip = _clipFire;
            }
            else if (isMoving)
            {
                if (Mathf.Abs(aim.x) >= Mathf.Abs(aim.y))
                {
                    activeClip = _clipRunSide ?? _clipRunDown;
                }
                else
                {
                    activeClip = aim.y > 0 ? (_clipRunUp ?? _clipRunDown) : _clipRunDown;
                }
            }
            else
            {
                if (Mathf.Abs(aim.x) >= Mathf.Abs(aim.y))
                {
                    activeClip = _clipIdleSide ?? _clipIdleDown;
                }
                else
                {
                    activeClip = aim.y > 0 ? (_clipIdleUp ?? _clipIdleDown) : _clipIdleDown;
                }
            }

            if (activeClip == null)
            {
                RenderProceduralSilhouette(renderer);
                return;
            }

            var frame = activeClip.Evaluate(_animTime);
            if (frame == null) return;

            Vector2 centerPos = _controller.Position;
            float cellW = renderer.CellWidth > 0f ? renderer.CellWidth : 0.22f;
            float cellH = renderer.CellHeight > 0f ? renderer.CellHeight : 0.22f;

            int halfW = frame.Width / 2;
            int halfH = frame.Height / 2;

            bool isFlashing = _damageable != null && _damageable.IsFlashing;
            Color32 flashCol = Color.white;

            for (int y = 0; y < frame.Height; y++)
            {
                for (int x = 0; x < frame.Width; x++)
                {
                    int sampleX = isFacingLeft ? (frame.Width - 1 - x) : x;
                    char g = frame.GetGlyph(sampleX, y);
                    if (g == ' ' || g == '\0') continue;

                    Color32 col = frame.GetColor(sampleX, y);
                    if (col.a == 0) continue;

                    if (isFlashing) col = flashCol;

                    float offsetX = (x - halfW) * cellW;
                    float offsetY = (halfH - y) * cellH;

                    renderer.DrawWorldGlyph(
                        centerPos + new Vector2(offsetX, offsetY),
                        g,
                        col,
                        Color.clear,
                        GameConstants.LAYER_PLAYER
                    );
                }
            }
        }

        private void RenderProceduralSilhouette(IAsciiRenderer renderer)
        {
            Vector2 pos = _controller.Position;
            Vector2 aim = _controller.AimDirection.normalized;

            bool isFlashing = _damageable != null && _damageable.IsFlashing;
            Color32 coreColor = isFlashing ? Color.white : _palette.PlayerPrimary;
            Color32 wingColor = isFlashing ? Color.white : _palette.PlayerSecondary;

            float cellW = renderer.CellWidth > 0f ? renderer.CellWidth : 0.22f;
            float cellH = renderer.CellHeight > 0f ? renderer.CellHeight : 0.22f;

            if (_fireTimer > 0f)
            {
                pos -= aim * (cellW * 0.4f);
            }

            renderer.DrawWorldGlyph(pos, _coreGlyph, coreColor, Color.clear, GameConstants.LAYER_PLAYER + 2);

            if (Mathf.Abs(aim.x) >= Mathf.Abs(aim.y))
            {
                if (aim.x > 0)
                {
                    renderer.DrawWorldGlyph(pos + new Vector2(cellW, 0f), '>', coreColor, Color.clear, GameConstants.LAYER_PLAYER + 3);
                    renderer.DrawWorldGlyph(pos + new Vector2(-cellW, 0f), '<', wingColor, Color.clear, GameConstants.LAYER_PLAYER + 1);
                    if (_drawStabilizers)
                    {
                        renderer.DrawWorldGlyph(pos + new Vector2(0f, cellH), '-', wingColor, Color.clear, GameConstants.LAYER_PLAYER);
                        renderer.DrawWorldGlyph(pos + new Vector2(0f, -cellH), '-', wingColor, Color.clear, GameConstants.LAYER_PLAYER);
                    }
                }
                else
                {
                    renderer.DrawWorldGlyph(pos + new Vector2(-cellW, 0f), '<', coreColor, Color.clear, GameConstants.LAYER_PLAYER + 3);
                    renderer.DrawWorldGlyph(pos + new Vector2(cellW, 0f), '>', wingColor, Color.clear, GameConstants.LAYER_PLAYER + 1);
                    if (_drawStabilizers)
                    {
                        renderer.DrawWorldGlyph(pos + new Vector2(0f, cellH), '-', wingColor, Color.clear, GameConstants.LAYER_PLAYER);
                        renderer.DrawWorldGlyph(pos + new Vector2(0f, -cellH), '-', wingColor, Color.clear, GameConstants.LAYER_PLAYER);
                    }
                }
            }
            else
            {
                if (aim.y > 0)
                {
                    renderer.DrawWorldGlyph(pos + new Vector2(0f, cellH), '^', coreColor, Color.clear, GameConstants.LAYER_PLAYER + 3);
                    renderer.DrawWorldGlyph(pos + new Vector2(0f, -cellH), 'v', wingColor, Color.clear, GameConstants.LAYER_PLAYER + 1);
                    if (_drawStabilizers)
                    {
                        renderer.DrawWorldGlyph(pos + new Vector2(-cellW, 0f), '|', wingColor, Color.clear, GameConstants.LAYER_PLAYER);
                        renderer.DrawWorldGlyph(pos + new Vector2(cellW, 0f), '|', wingColor, Color.clear, GameConstants.LAYER_PLAYER);
                    }
                }
                else
                {
                    renderer.DrawWorldGlyph(pos + new Vector2(0f, -cellH), 'v', coreColor, Color.clear, GameConstants.LAYER_PLAYER + 3);
                    renderer.DrawWorldGlyph(pos + new Vector2(0f, cellH), '^', wingColor, Color.clear, GameConstants.LAYER_PLAYER + 1);
                    if (_drawStabilizers)
                    {
                        renderer.DrawWorldGlyph(pos + new Vector2(-cellW, 0f), '|', wingColor, Color.clear, GameConstants.LAYER_PLAYER);
                        renderer.DrawWorldGlyph(pos + new Vector2(cellW, 0f), '|', wingColor, Color.clear, GameConstants.LAYER_PLAYER);
                    }
                }
            }

            if (_fireTimer > 0f)
            {
                renderer.DrawWorldGlyph(pos + (aim * (cellW * 1.8f)), '*', Color.white, Color.clear, GameConstants.LAYER_VFX_PARTICLES);
            }
        }

        private void UpdateTrail()
        {
            if (!_enableTrail) return;

            float dt = Time.deltaTime;

            for (int i = _trailPoints.Count - 1; i >= 0; i--)
            {
                var pt = _trailPoints[i];
                pt.RemainingTime -= dt;
                if (pt.RemainingTime <= 0f)
                {
                    _trailPoints.RemoveAt(i);
                }
                else
                {
                    _trailPoints[i] = pt;
                }
            }

            Vector2 currentPos = _controller.Position;
            if (Vector2.Distance(currentPos, _lastTrailPos) >= _trailSpawnDistance && _controller.IsMoving)
            {
                char trailGlyph = (_trailPoints.Count % 2 == 0) ? '.' : '+';
                _trailPoints.Add(new TrailPoint
                {
                    Position = _lastTrailPos,
                    RemainingTime = _trailLifetime,
                    MaxTime = _trailLifetime,
                    Glyph = trailGlyph
                });

                if (_trailPoints.Count > _maxTrailPoints)
                {
                    _trailPoints.RemoveAt(0);
                }

                _lastTrailPos = currentPos;
            }
        }

        private void RenderTrail(IAsciiRenderer renderer)
        {
            Color32 trailBase = _palette.PlayerTrail;

            foreach (var pt in _trailPoints)
            {
                float alphaRatio = Mathf.Clamp01(pt.RemainingTime / pt.MaxTime);
                Color32 fadedColor = new Color32(trailBase.r, trailBase.g, trailBase.b, (byte)(trailBase.a * alphaRatio));
                renderer.DrawWorldGlyph(pt.Position, pt.Glyph, fadedColor, Color.clear, GameConstants.LAYER_PLAYER_TRAIL);
            }
        }
    }
}
