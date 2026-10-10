using System;
using UnityEngine;

namespace AsciiActionGame.Core
{
    [Serializable]
    public struct AsciiCell : IEquatable<AsciiCell>
    {
        public char Glyph;
        public Color32 ForegroundColor;
        public Color32 BackgroundColor;
        public int Layer;
        public bool IsDirty;

        public static readonly AsciiCell Empty = new AsciiCell(' ', new Color32(0, 0, 0, 0), new Color32(0, 0, 0, 0), -1);

        public AsciiCell(char glyph, Color32 foregroundColor, Color32 backgroundColor, int layer = 0)
        {
            Glyph = glyph;
            ForegroundColor = foregroundColor;
            BackgroundColor = backgroundColor;
            Layer = layer;
            IsDirty = true;
        }

        public void Set(char glyph, Color32 foregroundColor, Color32 backgroundColor, int layer)
        {
            if (Glyph != glyph || !ForegroundColor.Equals(foregroundColor) || !BackgroundColor.Equals(backgroundColor) || Layer != layer)
            {
                Glyph = glyph;
                ForegroundColor = foregroundColor;
                BackgroundColor = backgroundColor;
                Layer = layer;
                IsDirty = true;
            }
        }

        public void Clear()
        {
            Glyph = ' ';
            ForegroundColor = new Color32(0, 0, 0, 0);
            BackgroundColor = new Color32(0, 0, 0, 0);
            Layer = -1;
            IsDirty = true;
        }

        public bool Equals(AsciiCell other)
        {
            return Glyph == other.Glyph &&
                   ForegroundColor.Equals(other.ForegroundColor) &&
                   BackgroundColor.Equals(other.BackgroundColor) &&
                   Layer == other.Layer;
        }

        public override bool Equals(object obj)
        {
            return obj is AsciiCell other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Glyph, ForegroundColor, BackgroundColor, Layer);
        }

        public static bool operator ==(AsciiCell left, AsciiCell right) => left.Equals(right);
        public static bool operator !=(AsciiCell left, AsciiCell right) => !left.Equals(right);
    }
}
