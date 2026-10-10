using System;
using UnityEngine;

namespace AsciiActionGame.World
{
    [Serializable]
    public struct WorldTileData
    {
        public char Glyph;
        public bool IsWalkable;
        public Color32 Color;
        public Color32 BackgroundColor;
        public byte TileType;

        public WorldTileData(char glyph, bool isWalkable, Color32 color, Color32 bgColor, byte tileType = 1)
        {
            Glyph = glyph;
            IsWalkable = isWalkable;
            Color = color;
            BackgroundColor = bgColor;
            TileType = tileType;
        }

        public static WorldTileData Empty => new WorldTileData(' ', true, new Color32(0, 0, 0, 0), new Color32(0, 0, 0, 0), 0);
        public static WorldTileData DefaultGround(Color32 color) => new WorldTileData('.', true, color, new Color32(0, 0, 0, 0), 1);
        public static WorldTileData DefaultWall(Color32 color) => new WorldTileData('#', false, color, new Color32(0, 0, 0, 0), 2);
    }
}
