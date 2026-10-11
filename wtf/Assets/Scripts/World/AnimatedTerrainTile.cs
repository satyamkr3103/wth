using UnityEngine;
using AsciiActionGame.Core;
using AsciiActionGame.Player;

namespace AsciiActionGame.World
{
    public class AnimatedTerrainTile : MonoBehaviour
    {
        public static AnimatedTerrainTile Instance { get; private set; }

        [Header("Grass Patch Settings")]
        [Tooltip("Procedural noise frequency for grass patches. Lower values create cohesive natural patches.")]
        [SerializeField] private float _grassPatchNoiseScale = 0.08f;

        [Tooltip("Threshold above which ground is considered tall grass that animates. Higher values mean fewer, more isolated patches.")]
        [Range(0.4f, 0.9f)]
        [SerializeField] private float _grassPatchThreshold = 0.62f;

        [Tooltip("Speed of the (| -> \\ -> | -> /) swaying cycle.")]
        [SerializeField] private float _grassLoopSpeed = 3.2f;

        [Tooltip("Radius around the player where footsteps agitate the grass.")]
        [SerializeField] private float _playerWakeRadius = 3.0f;

        [Tooltip("Multiplier for grass sway frequency when player moves through it.")]
        [SerializeField] private float _playerAgitationMultiplier = 3.5f;

        [Header("Matrix Stream Settings")]
        [SerializeField] private float _streamSpeed = 12f;
        [SerializeField] private Color32 _streamLeadColor = new Color32(180, 255, 200, 255);
        [SerializeField] private Color32 _streamTrailColor = new Color32(30, 140, 80, 255);

        [Header("Vegetation Pulse Settings")]
        [SerializeField] private float _vegetationPulseSpeed = 2.2f;
        [SerializeField] private float _vegetationMinLuminance = 0.85f;

        private static readonly char[] MATRIX_GLYPHS = { '0', '1', '7', '9', '>', 'v', '!', '$' };
        private static readonly char[] CANOPY_EDGE_GLYPHS = { '+', '~', '*', 'o' };

        private PlayerController _player;
        private Vector2 _playerPos;
        private float _playerSpeed;
        private bool _isPlayerMoving;
        private AsciiWorldMap _worldMap;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
        }

        private void Start()
        {
            _worldMap = FindFirstObjectByType<AsciiWorldMap>();
            _player = FindFirstObjectByType<PlayerController>();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            if (_worldMap == null)
            {
                _worldMap = FindFirstObjectByType<AsciiWorldMap>();
            }

            if (_player == null)
            {
                _player = FindFirstObjectByType<PlayerController>();
            }

            if (_player != null)
            {
                _playerPos = _player.Position;
                _playerSpeed = _player.Velocity.magnitude;
                _isPlayerMoving = _player.IsMoving;
            }
            else
            {
                var playerObj = GameObject.FindWithTag("Player");
                if (playerObj != null)
                {
                    Vector2 currentPos = playerObj.transform.position;
                    _playerSpeed = (currentPos - _playerPos).magnitude / Mathf.Max(0.001f, Time.deltaTime);
                    _isPlayerMoving = _playerSpeed > 0.05f;
                    _playerPos = currentPos;
                }
            }
        }

        public bool TryGetAnimatedTile(int gridX, int gridY, WorldTileData baseTile, out WorldTileData result)
        {
            result = baseTile;
            float time = Time.time;

            // 1. Moving Grass: ONLY animate specific patches of wild grass!
            if (baseTile.TileType == GameConstants.TILE_TYPE_GROUND ||
                baseTile.Glyph == '|' || baseTile.Glyph == '/' || baseTile.Glyph == '\\')
            {
                // Sample 2D procedural noise to create localized natural grass patches
                float patchNoise = Mathf.PerlinNoise((gridX * _grassPatchNoiseScale) + 127.4f, (gridY * _grassPatchNoiseScale) + 319.8f);

                // If this tile is NOT in an animated patch, keep it completely steady and static!
                if (patchNoise < _grassPatchThreshold)
                {
                    if (baseTile.Glyph != '|')
                    {
                        result = new WorldTileData('|', baseTile.IsWalkable, baseTile.Color, baseTile.BackgroundColor, GameConstants.TILE_TYPE_GROUND);
                        return true;
                    }
                    return false;
                }

                // In an animated patch: calculate player proximity for footstep agitation
                float cellSize = _worldMap != null ? _worldMap.CellSize : 0.25f;
                Vector2 tilePos = new Vector2((gridX + 0.5f) * cellSize, (gridY + 0.5f) * cellSize);

                float distToPlayer = Vector2.Distance(tilePos, _playerPos);
                float wake = Mathf.Clamp01(1.0f - (distToPlayer / _playerWakeRadius));

                float speedMultiplier = 1.0f + (wake * (_isPlayerMoving ? _playerAgitationMultiplier : 0.8f));
                float effectiveSpeed = _grassLoopSpeed * speedMultiplier;

                // Local phase offset per patch for natural variety
                float patchPhase = patchNoise * 8.0f;
                float animTime = (time * effectiveSpeed) + patchPhase;

                if (wake > 0.15f && _isPlayerMoving)
                {
                    animTime += Mathf.Sin(time * 14.0f + distToPlayer * 4.0f) * (wake * 1.5f);
                }

                // 4-step loop requested by user: | -> \ -> | -> /
                int step = Mathf.FloorToInt(animTime) % 4;
                if (step < 0) step += 4;

                char grassGlyph;
                switch (step)
                {
                    case 0:
                        grassGlyph = 'K';  // center
                        break;
                    case 1:
                        grassGlyph = '\\'; // lean left
                        break;
                    case 2:
                        grassGlyph = 'J';  // center
                        break;
                    case 3:
                    default:
                        grassGlyph = '/';  // lean right
                        break;
                }

                Color32 grassColor = baseTile.Color;

                // Subtle emerald highlight around player footsteps
                if (wake > 0.25f)
                {
                    byte addG = (byte)(wake * 28);
                    grassColor = new Color32(
                        (byte)Mathf.Min(255, grassColor.r + addG / 3),
                        (byte)Mathf.Min(255, grassColor.g + addG),
                        (byte)Mathf.Min(255, grassColor.b + addG / 4),
                        grassColor.a);
                }

                result = new WorldTileData(grassGlyph, baseTile.IsWalkable, grassColor, baseTile.BackgroundColor, GameConstants.TILE_TYPE_GROUND);
                return true;
            }

            if (baseTile.TileType == GameConstants.TILE_TYPE_DATASTREAM ||
                baseTile.Glyph == GameConstants.GLYPH_DATA_ZERO || baseTile.Glyph == GameConstants.GLYPH_DATA_ONE)
            {
                float flowOffset = (gridY + (time * _streamSpeed));
                int charIndex = (Mathf.FloorToInt(flowOffset) + gridX * 7) % MATRIX_GLYPHS.Length;
                if (charIndex < 0) charIndex += MATRIX_GLYPHS.Length;

                char animatedGlyph = MATRIX_GLYPHS[charIndex];
                float pulse = Mathf.Sin(flowOffset * 0.5f);
                Color32 streamColor = (pulse > 0.8f) ? _streamLeadColor : _streamTrailColor;

                result = new WorldTileData(animatedGlyph, baseTile.IsWalkable, streamColor, baseTile.BackgroundColor, baseTile.TileType);
                return true;
            }

            if (baseTile.TileType == GameConstants.TILE_TYPE_VEGETATION ||
                baseTile.TileType == GameConstants.TILE_TYPE_CANOPY ||
                baseTile.Glyph == '*' || baseTile.Glyph == '+' || baseTile.Glyph == '~')
            {
                float vegPhase = Mathf.Sin((time * _vegetationPulseSpeed) + (gridX * 0.4f) + (gridY * 0.3f));
                float lumFactor = Mathf.Lerp(_vegetationMinLuminance, 1.12f, (vegPhase + 1f) * 0.5f);

                byte r = (byte)Mathf.Clamp(baseTile.Color.r * lumFactor, 0, 255);
                byte g = (byte)Mathf.Clamp(baseTile.Color.g * lumFactor, 0, 255);
                byte b = (byte)Mathf.Clamp(baseTile.Color.b * lumFactor, 0, 255);

                char vegGlyph = baseTile.Glyph;
                if (baseTile.Glyph == '+' || baseTile.Glyph == '~')
                {
                    vegGlyph = (vegPhase > 0.5f) ? '~' : '+';
                }

                result = new WorldTileData(vegGlyph, baseTile.IsWalkable, new Color32(r, g, b, baseTile.Color.a), baseTile.BackgroundColor, baseTile.TileType);
                return true;
            }

            if (baseTile.TileType == GameConstants.TILE_TYPE_WATER || baseTile.Glyph == GameConstants.GLYPH_WATER_WAVE)
            {
                float wavePhase = (time * 3.5f) + (gridX * 0.75f) + (gridY * 0.35f);
                float rippleNoise = Mathf.Sin(wavePhase);

                char waveGlyph = GameConstants.GLYPH_WATER_WAVE;
                Color32 waterColor = baseTile.Color;

                if (rippleNoise > 0.65f)
                {
                    waveGlyph = GameConstants.GLYPH_WATER_RIPPLE;
                    waterColor = new Color32(80, 180, 255, 255);
                }
                else if (rippleNoise < -0.6f)
                {
                    waveGlyph = GameConstants.GLYPH_WATER_SURFACE;
                    waterColor = new Color32(25, 80, 160, 255);
                }

                result = new WorldTileData(waveGlyph, baseTile.IsWalkable, waterColor, baseTile.BackgroundColor, GameConstants.TILE_TYPE_WATER);
                return true;
            }

            return false;
        }
    }
}
