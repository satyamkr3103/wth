using UnityEngine;
using AsciiActionGame.Core;

namespace AsciiActionGame.Rendering
{
    public class DisintegrationEffect : MonoBehaviour
    {
        public static DisintegrationEffect Instance { get; private set; }

        [Header("Particle Buffer")]
        [SerializeField] private int _maxParticles = 192;
        [SerializeField] private float _defaultDrag = 0.85f;

        [Header("Palette")]
        [SerializeField] private AsciiColorPalette _palette;

        private struct Particle
        {
            public bool IsActive;
            public Vector2 Position;
            public Vector2 Velocity;
            public float RemainingTime;
            public float MaxTime;
            public char Glyph;
            public Color32 Color;
        }

        private Particle[] _particleBuffer;
        private int _activeCount = 0;

        private static readonly char[] BURST_GLYPHS = { '%', '#', '*', 'x', '+', ':', '.' };

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

            _particleBuffer = new Particle[_maxParticles];
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
            if (_activeCount == 0) return;

            float dt = Time.deltaTime;
            float dragFactor = Mathf.Pow(_defaultDrag, dt * 10f);

            int remainingActive = 0;
            for (int i = 0; i < _maxParticles; i++)
            {
                if (!_particleBuffer[i].IsActive) continue;

                _particleBuffer[i].RemainingTime -= dt;
                if (_particleBuffer[i].RemainingTime <= 0f)
                {
                    _particleBuffer[i].IsActive = false;
                    continue;
                }

                _particleBuffer[i].Position += _particleBuffer[i].Velocity * dt;
                _particleBuffer[i].Velocity *= dragFactor;
                remainingActive++;
            }

            _activeCount = remainingActive;
        }

        public static void TriggerBurst(Vector2 center, int count, Color32? customColor = null, float speed = 5.5f, float lifetime = 0.55f)
        {
            if (Instance == null)
            {
                var vfxObj = new GameObject("DisintegrationEffect");
                Instance = vfxObj.AddComponent<DisintegrationEffect>();
            }

            Instance.EmitBurst(center, count, customColor, speed, lifetime);
        }

        public void EmitBurst(Vector2 center, int count, Color32? customColor, float speed, float lifetime)
        {
            Color32 baseColor = customColor ?? (_palette != null ? _palette.EnemyFragment : new Color32(255, 90, 40, 255));

            int spawned = 0;
            for (int i = 0; i < _maxParticles && spawned < count; i++)
            {
                if (_particleBuffer[i].IsActive) continue;

                float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float currentSpeed = speed * Random.Range(0.4f, 1.3f);
                Vector2 vel = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * currentSpeed;

                char glyph = BURST_GLYPHS[Random.Range(0, BURST_GLYPHS.Length)];
                float actualLife = lifetime * Random.Range(0.7f, 1.2f);

                _particleBuffer[i] = new Particle
                {
                    IsActive = true,
                    Position = center,
                    Velocity = vel,
                    RemainingTime = actualLife,
                    MaxTime = actualLife,
                    Glyph = glyph,
                    Color = baseColor
                };

                spawned++;
                _activeCount++;
            }
        }

        private void OnRenderAscii(IAsciiRenderer renderer)
        {
            if (_activeCount == 0) return;

            for (int i = 0; i < _maxParticles; i++)
            {
                if (!_particleBuffer[i].IsActive) continue;

                float t = Mathf.Clamp01(_particleBuffer[i].RemainingTime / _particleBuffer[i].MaxTime);
                byte alpha = (byte)(_particleBuffer[i].Color.a * t);

                Color32 fadedColor = new Color32(
                    _particleBuffer[i].Color.r,
                    _particleBuffer[i].Color.g,
                    _particleBuffer[i].Color.b,
                    alpha
                );

                renderer.DrawWorldGlyph(
                    _particleBuffer[i].Position,
                    _particleBuffer[i].Glyph,
                    fadedColor,
                    Color.clear,
                    GameConstants.LAYER_VFX_PARTICLES
                );
            }
        }
    }
}
