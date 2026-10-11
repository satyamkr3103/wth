using UnityEngine;

namespace AsciiActionGame.Rendering
{
    public interface IAsciiRenderer
    {
        int Columns { get; }
        int Rows { get; }
        float CellWidth { get; }
        float CellHeight { get; }
        float GlyphAspect { get; }

        void ClearBuffer();

        void DrawGlyph(int viewX, int viewY, char glyph, Color32 foreground, Color32 background, int layer);

        Vector2Int WorldToViewport(Vector2 worldPos);

        Vector2 ViewportToWorld(int viewX, int viewY);

        void DrawWorldGlyph(Vector2 worldPos, char glyph, Color32 foreground, Color32 background, int layer);

        void Present();
    }
}
