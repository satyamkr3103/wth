using UnityEngine;
using AsciiActionGame.Core;

namespace AsciiActionGame.Rendering
{
    public class AsciiPostProcessing : MonoBehaviour
    {
        public static AsciiPostProcessing Instance { get; private set; }

        [Header("CRT Scanlines")]
        [SerializeField] private bool _enableScanlines = false;
        [Range(0.5f, 1.0f)]
        [SerializeField] private float _scanlineDarkening = 0.82f;
        [SerializeField] private bool _enableCrtFlicker = false;

        [Header("Digital Glitch")]
        [SerializeField] private bool _enableGlitch = false;
        [SerializeField] private float _glitchFrequency = 0.08f;

        [Header("Screen Shake")]
        [SerializeField] private float _maxShakeOffset = 0.8f;
        [SerializeField] private float _shakeFrequency = 24.0f;
        [SerializeField] private float _traumaDecayRate = 2.2f;

        private float _trauma = 0f;
        private float _glitchTimer = 0f;
        private float _glitchIntensity = 0f;
        private Vector2 _currentShakeOffset = Vector2.zero;

        private static readonly char[] GLITCH_GLYPHS = { '@', '#', '$', '&', '?', '!', '%', 'X', '>', '<' };
        private static readonly Color32 GLITCH_CYAN = new Color32(0, 255, 240, 255);
        private static readonly Color32 GLITCH_MAGENTA = new Color32(255, 30, 160, 255);

        public Vector2 CurrentShakeOffset => _currentShakeOffset;
        public bool EnableScanlines { get => _enableScanlines; set => _enableScanlines = value; }
        public bool EnableCrtFlicker { get => _enableCrtFlicker; set => _enableCrtFlicker = value; }
        public bool EnableGlitch { get => _enableGlitch; set => _enableGlitch = value; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
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
            AsciiGridRenderer.OnPostProcessBuffer += ApplyPostProcessing;
        }

        private void OnDisable()
        {
            AsciiGridRenderer.OnPostProcessBuffer -= ApplyPostProcessing;
        }

        private void Update()
        {
            UpdateTrauma();
            UpdateGlitch();
        }

        private void UpdateTrauma()
        {
            if (_trauma > 0f)
            {
                _trauma = Mathf.Max(0f, _trauma - (_traumaDecayRate * Time.deltaTime));

                float shakeMagnitude = _trauma * _trauma * _maxShakeOffset;
                float time = Time.time * _shakeFrequency;

                float offsetX = (Mathf.PerlinNoise(time, 0f) - 0.5f) * 2f * shakeMagnitude;
                float offsetY = (Mathf.PerlinNoise(0f, time) - 0.5f) * 2f * shakeMagnitude;

                _currentShakeOffset = new Vector2(offsetX, offsetY);
            }
            else
            {
                _currentShakeOffset = Vector2.zero;
            }
        }

        private void UpdateGlitch()
        {
            if (_glitchTimer > 0f)
            {
                _glitchTimer -= Time.deltaTime;
                if (_glitchTimer <= 0f)
                {
                    _glitchIntensity = 0f;
                }
            }
            else if (_enableGlitch && Random.value < (_glitchFrequency * Time.deltaTime))
            {
                TriggerGlitch(0.08f, 0.4f);
            }
        }

        public static void TriggerScreenShake(float amount)
        {
            if (Instance != null)
            {
                Instance.AddTrauma(amount);
            }
        }

        public void AddTrauma(float amount)
        {
            _trauma = Mathf.Clamp01(_trauma + amount);
        }

        public static void TriggerGlitch(float duration = 0.18f, float intensity = 1.0f)
        {
            if (Instance != null)
            {
                Instance._glitchTimer = duration;
                Instance._glitchIntensity = intensity;
            }
        }

        private void ApplyPostProcessing(AsciiCell[,] buffer, int columns, int rows)
        {
            if (_glitchTimer > 0f && _glitchIntensity > 0.05f)
            {
                ApplyGlitchDistortion(buffer, columns, rows);
            }

            if (_enableScanlines)
            {
                ApplyScanlines(buffer, columns, rows);
            }
        }

        private void ApplyGlitchDistortion(AsciiCell[,] buffer, int columns, int rows)
        {
            int glitchRowCount = Mathf.CeilToInt(rows * 0.15f * _glitchIntensity);

            for (int g = 0; g < glitchRowCount; g++)
            {
                int targetRow = Random.Range(0, rows);
                int shift = Random.Range(-3, 4);
                bool useColorShift = Random.value > 0.4f;
                Color32 tint = Random.value > 0.5f ? GLITCH_CYAN : GLITCH_MAGENTA;

                if (shift > 0)
                {
                    for (int x = columns - 1; x >= shift; x--)
                    {
                        buffer[x, targetRow] = buffer[x - shift, targetRow];
                    }
                }
                else if (shift < 0)
                {
                    int absShift = -shift;
                    for (int x = 0; x < columns - absShift; x++)
                    {
                        buffer[x, targetRow] = buffer[x + absShift, targetRow];
                    }
                }

                for (int x = 0; x < columns; x++)
                {
                    if (Random.value < 0.25f)
                    {
                        char corruptChar = GLITCH_GLYPHS[Random.Range(0, GLITCH_GLYPHS.Length)];
                        Color32 color = useColorShift ? tint : buffer[x, targetRow].ForegroundColor;
                        buffer[x, targetRow].Set(corruptChar, color, buffer[x, targetRow].BackgroundColor, GameConstants.LAYER_UI_OVERLAY);
                    }
                }
            }
        }

        private void ApplyScanlines(AsciiCell[,] buffer, int columns, int rows)
        {
            float flicker = _enableCrtFlicker ? (1f + (Mathf.Sin(Time.time * 28f) * 0.03f)) : 1f;

            for (int y = 0; y < rows; y++)
            {
                float lineFactor = (y % 2 == 1) ? (_scanlineDarkening * flicker) : flicker;

                if (Mathf.Approximately(lineFactor, 1f)) continue;

                for (int x = 0; x < columns; x++)
                {
                    Color32 fg = buffer[x, y].ForegroundColor;
                    if (fg.a > 0)
                    {
                        byte r = (byte)Mathf.Clamp(fg.r * lineFactor, 0, 255);
                        byte g = (byte)Mathf.Clamp(fg.g * lineFactor, 0, 255);
                        byte b = (byte)Mathf.Clamp(fg.b * lineFactor, 0, 255);
                        buffer[x, y].ForegroundColor = new Color32(r, g, b, fg.a);
                    }
                }
            }
        }
    }
}
