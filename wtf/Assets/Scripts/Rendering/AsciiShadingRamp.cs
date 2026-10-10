using UnityEngine;
using AsciiActionGame.World;

namespace AsciiActionGame.Rendering
{
    public static class AsciiShadingRamp
    {
        public const string DENSITY_RAMP_STANDARD = " .,:;!|+*ox%#@";
        public const string DENSITY_RAMP_EXTENDED = " `.-':_,^=;><+!rc*/z?sLTv)J7(|Fi{C}fI31tlu[neoZ5Yxjya]2ESwqkP6h9d4VpOGbUAKXHm8RD#$BG0MNWQ%&@";
        public const string DENSITY_RAMP_HIGH_CONTRAST = " .:-=+*#%@";

        public static float CalculateAmbientOcclusion(AsciiWorldMap worldMap, int gridX, int gridY)
        {
            if (worldMap == null) return 1.0f;

            if (!worldMap.IsWalkableGrid(gridX, gridY)) return 1.0f;

            int solidCardinal = 0;
            int solidDiagonal = 0;

            if (!worldMap.IsWalkableGrid(gridX, gridY + 1)) solidCardinal++;
            if (!worldMap.IsWalkableGrid(gridX, gridY - 1)) solidCardinal++;
            if (!worldMap.IsWalkableGrid(gridX - 1, gridY)) solidCardinal++;
            if (!worldMap.IsWalkableGrid(gridX + 1, gridY)) solidCardinal++;

            if (!worldMap.IsWalkableGrid(gridX - 1, gridY + 1)) solidDiagonal++;
            if (!worldMap.IsWalkableGrid(gridX + 1, gridY + 1)) solidDiagonal++;
            if (!worldMap.IsWalkableGrid(gridX - 1, gridY - 1)) solidDiagonal++;
            if (!worldMap.IsWalkableGrid(gridX + 1, gridY - 1)) solidDiagonal++;

            float occlusion = (solidCardinal * 0.12f) + (solidDiagonal * 0.05f);
            return Mathf.Clamp(1.0f - occlusion, 0.65f, 1.0f);
        }

        public static Color32 ApplyShading(Color32 baseColor, float aoFactor, float lightFactor)
        {
            float combined = Mathf.Clamp(aoFactor * lightFactor, 0.70f, 1.0f);

            float curved = Mathf.Pow(combined, 1.05f);

            byte r = (byte)Mathf.Clamp(baseColor.r * curved, 0f, 255f);
            byte g = (byte)Mathf.Clamp(baseColor.g * curved, 0f, 255f);
            byte b = (byte)Mathf.Clamp(baseColor.b * curved, 0f, 255f);

            return new Color32(r, g, b, baseColor.a);
        }

        public static char DensityToGlyph(float luminance01, string ramp = DENSITY_RAMP_STANDARD)
        {
            if (string.IsNullOrEmpty(ramp)) ramp = DENSITY_RAMP_STANDARD;

            int index = Mathf.Clamp(Mathf.FloorToInt(luminance01 * (ramp.Length - 1)), 0, ramp.Length - 1);
            return ramp[index];
        }

        public static char ShadedTerrainGlyph(char original, float lightFactor)
        {
            if (original == '|' || original == '/' || original == '\\') return original;

            if (lightFactor < 0.45f)
            {
                if (original == ':' || original == ';') return ',';
                if (original == ',') return '.';
            }
            return original;
        }

        public static float CalculateLightIntensity(Vector2 worldPos, Vector2 lightPos, float lightRadius, float minAmbient = 0.55f)
        {
            float dist = Vector2.Distance(worldPos, lightPos);
            if (dist >= lightRadius) return minAmbient;

            float t = Mathf.Clamp01(dist / lightRadius);
            float falloff = 1f - (t * t * (3f - 2f * t));
            return Mathf.Lerp(minAmbient, 1.0f, falloff);
        }
    }
}
