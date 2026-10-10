using UnityEngine;

namespace AsciiActionGame.World
{
    public static class ImageToAsciiConverter
    {
        public const string DEFAULT_DENSITY_RAMP = " .,:;!|+*ox%#@";
        public const string HIGH_CONTRAST_RAMP = " .:-=+*#%@";

        public static WorldTileData[,] ConvertTextureToTiles(
            Texture2D texture,
            int targetWidth,
            int targetHeight,
            string densityRamp = DEFAULT_DENSITY_RAMP,
            Color? tint = null,
            float solidThreshold = 0.82f,
            bool invertDensity = false,
            bool preserveEdges = true,
            float gamma = 2.2f)
        {
            if (texture == null)
            {
                Debug.LogError("[ImageToAsciiConverter] Source texture is null!");
                return null;
            }

            if (string.IsNullOrEmpty(densityRamp))
            {
                densityRamp = DEFAULT_DENSITY_RAMP;
            }

            Color baseTint = tint ?? Color.white;
            WorldTileData[,] result = new WorldTileData[targetWidth, targetHeight];
            int rampLength = densityRamp.Length;
            float stepX = 1.0f / targetWidth;
            float stepY = 1.0f / targetHeight;

            for (int y = 0; y < targetHeight; y++)
            {
                float normY = (float)y / targetHeight;

                for (int x = 0; x < targetWidth; x++)
                {
                    float normX = (float)x / targetWidth;

                    Color center = texture.GetPixelBilinear(normX, normY);

                    float lum = 0.2126f * center.r + 0.7152f * center.g + 0.0722f * center.b;

                    if (gamma > 0.01f)
                    {
                        lum = Mathf.Pow(Mathf.Clamp01(lum), 1.0f / gamma);
                    }

                    if (preserveEdges)
                    {
                        Color right = texture.GetPixelBilinear(normX + stepX, normY);
                        Color up = texture.GetPixelBilinear(normX, normY + stepY);

                        float lumR = 0.2126f * right.r + 0.7152f * right.g + 0.0722f * right.b;
                        float lumU = 0.2126f * up.r + 0.7152f * up.g + 0.0722f * up.b;

                        float edgeGradient = Mathf.Abs(lum - lumR) + Mathf.Abs(lum - lumU);
                        if (edgeGradient > 0.18f)
                        {
                            lum = Mathf.Clamp01(lum + (edgeGradient * 0.35f));
                        }
                    }

                    if (invertDensity)
                    {
                        lum = 1.0f - lum;
                    }

                    int rampIndex = Mathf.Clamp(Mathf.FloorToInt(lum * (rampLength - 1)), 0, rampLength - 1);
                    char glyph = densityRamp[rampIndex];

                    bool isWalkable = lum < solidThreshold;
                    Color finalColor = center * baseTint;
                    Color32 color32 = new Color32(
                        (byte)(Mathf.Clamp01(finalColor.r) * 255),
                        (byte)(Mathf.Clamp01(finalColor.g) * 255),
                        (byte)(Mathf.Clamp01(finalColor.b) * 255),
                        255
                    );

                    byte tileType = isWalkable ? (byte)1 : (byte)2;
                    result[x, y] = new WorldTileData(glyph, isWalkable, color32, new Color32(0, 0, 0, 0), tileType);
                }
            }

            return result;
        }
    }
}
