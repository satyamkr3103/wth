using UnityEngine;

namespace AsciiActionGame.Core
{
    [CreateAssetMenu(fileName = "AsciiColorPalette", menuName = "ASCII Game/Color Palette")]
    public class AsciiColorPalette : ScriptableObject
    {
        [Header("Global Background")]
        public Color32 CanvasBackground = new Color32(8, 10, 15, 255);

        [Header("Environment & Terrain")]
        public Color32 GroundPrimary = new Color32(95, 185, 80, 255);
        public Color32 GroundSecondary = new Color32(75, 160, 65, 255);
        public Color32 PathDirt = new Color32(165, 140, 95, 255);
        public Color32 RockObstacle = new Color32(145, 165, 180, 255);
        public Color32 WallSolid = new Color32(120, 160, 145, 255);
        public Color32 WallDense = new Color32(90, 135, 120, 255);
        public Color32 DataStream = new Color32(40, 180, 110, 255);
        public Color32 Vegetation = new Color32(80, 195, 90, 255);
        public Color32 CanopyLeaves = new Color32(70, 195, 90, 255);
        public Color32 CanopyDense = new Color32(45, 130, 60, 255);
        public Color32 WaterPrimary = new Color32(35, 105, 200, 255);
        public Color32 WaterSecondary = new Color32(55, 145, 235, 255);
        public Color32 WaterWave = new Color32(110, 200, 255, 255);

        [Header("Boss Arena Palette")]
        public Color32 ArenaSand = new Color32(211, 178, 117, 255);
        public Color32 ArenaSandLight = new Color32(223, 193, 138, 255);
        public Color32 ArenaGrass = new Color32(94, 152, 77, 255);
        public Color32 ArenaGrassDark = new Color32(75, 131, 60, 255);
        public Color32 ArenaStoneWall = new Color32(184, 171, 160, 255);
        public Color32 ArenaStonePillar = new Color32(200, 188, 176, 255);
        public Color32 ArenaStoneShadow = new Color32(127, 115, 106, 255);
        public Color32 ArenaGate = new Color32(138, 130, 117, 255);

        [Header("Player")]
        public Color32 PlayerPrimary = new Color32(0, 240, 255, 255);
        public Color32 PlayerSecondary = new Color32(255, 255, 255, 255);
        public Color32 PlayerTrail = new Color32(0, 140, 180, 180);

        [Header("Enemies")]
        public Color32 EnemyCore = new Color32(255, 50, 80, 255);
        public Color32 EnemyArmor = new Color32(200, 70, 40, 255);
        public Color32 EnemyLimb = new Color32(255, 140, 30, 255);
        public Color32 EnemyFragment = new Color32(255, 90, 40, 255);

        [Header("Combat & Weapons")]
        public Color32 EraserBeam = new Color32(120, 255, 180, 255);
        public Color32 EraserTrail = new Color32(60, 200, 120, 180);
        public Color32 ImpactVFX = new Color32(255, 255, 200, 255);

        [Header("Corruption & Distortion")]
        public Color32 CorruptionPrimary = new Color32(200, 40, 255, 255);
        public Color32 CorruptionSecondary = new Color32(130, 20, 180, 255);

        public static AsciiColorPalette CreateDefault()
        {
            var palette = CreateInstance<AsciiColorPalette>();
            return palette;
        }
    }
}
