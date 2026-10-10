using System;
using System.Collections.Generic;
using UnityEngine;
using AsciiActionGame.Rendering;

namespace AsciiActionGame.Animation
{
    [Serializable]
    public class AsciiFrameData
    {
        public int Width;
        public int Height;
        public char[] Glyphs;
        public Color32[] Colors;

        public AsciiFrameData(int width, int height)
        {
            Width = width;
            Height = height;
            Glyphs = new char[width * height];
            Colors = new Color32[width * height];
        }

        public char GetGlyph(int x, int y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height) return ' ';
            return Glyphs[(y * Width) + x];
        }

        public Color32 GetColor(int x, int y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height) return Color.clear;
            return Colors[(y * Width) + x];
        }
}

    [CreateAssetMenu(fileName = "AsciiAnimationClip", menuName = "ASCII Game/Animation Clip")]
    public class AsciiAnimationClip : ScriptableObject
    {
        [Header("Playback Settings")]
        [Tooltip("Frames per second.")]
        [SerializeField] private float _frameRate = 8f;

        [Tooltip("Should the animation loop automatically.")]
        [SerializeField] private bool _loop = true;

        [Header("Aseprite Sprite Sheet Input (Preferred)")]
        [Tooltip("Assign sliced sprite frames from an Aseprite export. Pixels are converted to ASCII matching pixel colors!")]
        [SerializeField] private Sprite[] _spriteFrames;

        [Header("ASCII Density Ramp")]
        [Tooltip("Characters ordered from lightest to densest for sprite luminance conversion.")]
        [SerializeField] private string _densityRamp = " .,:;!|+*ox%#@";

        [Header("Pure ASCII Text Frames (Alternative)")]
        [Tooltip("Multiline text frames (e.g. 3x3 or 5x5 ASCII characters per frame).")]
        [TextArea(3, 8)]
        [SerializeField] private string[] _textFrames;

        [Tooltip("Default color applied to text frames if no sprites are used.")]
        [SerializeField] private Color32 _defaultTextColor = new Color32(0, 240, 255, 255);

        [NonSerialized] private List<AsciiFrameData> _bakedFrames;
        [NonSerialized] private bool _isBaked = false;

        public float FrameRate => _frameRate;
        public bool Loop => _loop;
        public int FrameCount => (_spriteFrames != null && _spriteFrames.Length > 0) ? _spriteFrames.Length : (_textFrames != null ? _textFrames.Length : 0);

        private void OnEnable()
        {
            _isBaked = false;
        }

        public AsciiFrameData Evaluate(float time)
        {
            EnsureBaked();

            if (_bakedFrames == null || _bakedFrames.Count == 0) return null;

            int count = _bakedFrames.Count;
            if (count == 1) return _bakedFrames[0];

            int frameIndex = Mathf.FloorToInt(time * _frameRate);

            if (_loop)
            {
                frameIndex = ((frameIndex % count) + count) % count;
            }
            else
            {
                frameIndex = Mathf.Clamp(frameIndex, 0, count - 1);
            }

            return _bakedFrames[frameIndex];
        }

        public void InvalidateCache()
        {
            _isBaked = false;
        }

        private void EnsureBaked()
        {
            if (_isBaked && _bakedFrames != null && _bakedFrames.Count > 0) return;

            _bakedFrames = new List<AsciiFrameData>();

            if (_spriteFrames != null && _spriteFrames.Length > 0)
            {
                foreach (var sprite in _spriteFrames)
                {
                    if (sprite == null) continue;
                    var frame = BakeSpriteToAscii(sprite);
                    if (frame != null) _bakedFrames.Add(frame);
                }
            }
            else if (_textFrames != null && _textFrames.Length > 0)
            {
                foreach (var text in _textFrames)
                {
                    if (string.IsNullOrEmpty(text)) continue;
                    var frame = BakeTextToAscii(text);
                    if (frame != null) _bakedFrames.Add(frame);
                }
            }

            _isBaked = true;
        }

        private AsciiFrameData BakeSpriteToAscii(Sprite sprite)
        {
            Texture2D texture = sprite.texture;
            if (texture == null) return null;

            Rect rect = sprite.textureRect;
            int width = Mathf.RoundToInt(rect.width);
            int height = Mathf.RoundToInt(rect.height);

            if (width <= 0 || height <= 0) return null;

            var frame = new AsciiFrameData(width, height);
            string ramp = string.IsNullOrEmpty(_densityRamp) ? AsciiShadingRamp.DENSITY_RAMP_STANDARD : _densityRamp;
            int rampLen = ramp.Length;

            int startX = Mathf.RoundToInt(rect.x);
            int startY = Mathf.RoundToInt(rect.y);

            Color[] pixels;
            try
            {
                pixels = texture.GetPixels(startX, startY, width, height);
            }
            catch
            {
                Debug.LogWarning($"[AsciiAnimationClip] Texture '{texture.name}' must have 'Read/Write Enabled' checked in Texture Import Settings.");
                return null;
            }

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int pixelIdx = (y * width) + x;
                    int targetY = height - 1 - y;
                    int targetIdx = (targetY * width) + x;

                    Color pixel = pixels[pixelIdx];

                    if (pixel.a < 0.15f)
                    {
                        frame.Glyphs[targetIdx] = ' ';
                        frame.Colors[targetIdx] = Color.clear;
                        continue;
                    }

                    float lum = 0.2126f * pixel.r + 0.7152f * pixel.g + 0.0722f * pixel.b;
                    int rampIndex = Mathf.Clamp(Mathf.FloorToInt(lum * (rampLen - 1)), 0, rampLen - 1);

                    frame.Glyphs[targetIdx] = ramp[rampIndex];
                    frame.Colors[targetIdx] = (Color32)pixel;
                }
            }

            return frame;
        }

        private AsciiFrameData BakeTextToAscii(string text)
        {
            string[] lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            if (lines.Length == 0) return null;

            int height = lines.Length;
            int width = 0;
            foreach (var l in lines)
            {
                if (l.Length > width) width = l.Length;
            }

            if (width <= 0) return null;

            var frame = new AsciiFrameData(width, height);

            for (int y = 0; y < height; y++)
            {
                string line = lines[y];
                for (int x = 0; x < width; x++)
                {
                    int idx = (y * width) + x;
                    char ch = x < line.Length ? line[x] : ' ';
                    frame.Glyphs[idx] = ch;
                    frame.Colors[idx] = ch == ' ' ? (Color32)Color.clear : _defaultTextColor;
                }
            }

            return frame;
        }
    }
}
