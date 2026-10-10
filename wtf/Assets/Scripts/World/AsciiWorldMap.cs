using System;
using System.Collections.Generic;
using UnityEngine;
using AsciiActionGame.Core;
using AsciiActionGame.Rendering;

namespace AsciiActionGame.World
{

    public class AsciiWorldMap : MonoBehaviour
    {
        [Header("Map Dimensions")]
        [Tooltip("Width of the world grid in ASCII cells.")]
        [SerializeField] private int _width = 240;

        [Tooltip("Height of the world grid in ASCII cells.")]
        [SerializeField] private int _height = 160;

        [Tooltip("World unit size of a single logical cell.")]
        [SerializeField] private float _cellSize = 0.25f;

        [Header("Custom Environment Map (Aseprite / PNG / Tilemap)")]
        [Tooltip("Optional image defining the level. Blue=Water, Brown=Road/Path, Green=Meadow, Dark=Walls, White=Rocks.")]
        [SerializeField] private Texture2D _customEnvironmentMap;

        [Tooltip("If using a custom map, should procedural trees & bushes spawn on tiles detected as wild ground?")]
        [SerializeField] private bool _spawnFoliageOnCustomGround = true;

        [Tooltip("Can the player walk through water?")]
        [SerializeField] private bool _isWaterWalkable = false;

        [Header("Generation Settings")]
        [SerializeField] private int _seed = 42;
        [SerializeField] private bool _randomizeSeedOnStart = false;

        [Header("Color Palette")]
        [SerializeField] private AsciiColorPalette _palette;

        [Header("Collision Generation")]
        [SerializeField] private bool _generatePhysicsColliders = true;
        [SerializeField] private LayerMask _obstacleLayer;

        private WorldTileData[,] _tiles;
        private float[,] _ambientOcclusion;
        private GameObject _colliderRoot;

        public int Width => _width;
        public int Height => _height;
        public float CellSize => _cellSize;
        public AsciiColorPalette Palette => _palette;
        public Texture2D CustomEnvironmentMap { get => _customEnvironmentMap; set => _customEnvironmentMap = value; }

        public event Action<int, int, WorldTileData> OnTileChanged;

        private void Awake()
        {
            if (_palette == null)
            {
                _palette = AsciiColorPalette.CreateDefault();
            }

            if (FindFirstObjectByType<AnimatedTerrainTile>() == null)
            {
                gameObject.AddComponent<AnimatedTerrainTile>();
            }

            InitializeMap();
        }

        public void InitializeMap()
        {
            if (_palette == null)
            {
                _palette = AsciiColorPalette.CreateDefault();
            }

            if (_randomizeSeedOnStart && Application.isPlaying)
            {
                _seed = UnityEngine.Random.Range(1000, 999999);
            }

            _tiles = new WorldTileData[_width, _height];

            if (_customEnvironmentMap != null)
            {
                GenerateFromCustomEnvironmentMap();
            }
            else
            {
                GenerateDesignedEnvironment();
            }

            BakeAmbientOcclusion();

            if (_generatePhysicsColliders)
            {
                RebuildPhysicsColliders();
            }
        }

        public void BakeAmbientOcclusion()
        {
            if (_tiles == null)
            {
                InitializeMap();
                return;
            }

            _ambientOcclusion = new float[_width, _height];
            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    _ambientOcclusion[x, y] = AsciiShadingRamp.CalculateAmbientOcclusion(this, x, y);
                }
            }
        }

        public float GetAO(int x, int y)
        {
            if (_ambientOcclusion == null || x < 0 || x >= _width || y < 0 || y >= _height) return 1.0f;
            return _ambientOcclusion[x, y];
        }

        public void GenerateFromCustomEnvironmentMap()
        {
            if (_customEnvironmentMap == null) return;

            float seedX = (_seed % 1000) * 0.1f;
            float seedY = ((_seed / 1000) % 1000) * 0.1f;

            Color32 wallColor = _palette.WallSolid;
            Color32 rockColor = _palette.RockObstacle;
            Color32 groundColor = _palette.GroundPrimary;
            Color32 groundDetail = _palette.GroundSecondary;
            Color32 pathColor = _palette.PathDirt;
            Color32 waterColor = _palette.WaterPrimary;
            Color32 leafColor = _palette.CanopyLeaves;
            Color32 denseCanopyColor = _palette.CanopyDense;
            Color32 bushColor = _palette.Vegetation;

            for (int y = 0; y < _height; y++)
            {
                float normY = (float)y / _height;
                for (int x = 0; x < _width; x++)
                {
                    float normX = (float)x / _width;
                    Color pixel = _customEnvironmentMap.GetPixelBilinear(normX, normY);

                    if (x < 2 || x >= _width - 2 || y < 2 || y >= _height - 2)
                    {
                        char wallGlyph = (x == 0 || x == _width - 1 || y == 0 || y == _height - 1) ? '#' : '%';
                        _tiles[x, y] = new WorldTileData(wallGlyph, false, wallColor, new Color32(0, 0, 0, 0), GameConstants.TILE_TYPE_WALL);
                        continue;
                    }

                    if (pixel.b > 0.40f && pixel.b > (pixel.r + 0.15f) && pixel.b > (pixel.g + 0.08f))
                    {
                        _tiles[x, y] = new WorldTileData(GameConstants.GLYPH_WATER_WAVE, _isWaterWalkable, waterColor, new Color32(0, 0, 0, 0), GameConstants.TILE_TYPE_WATER);
                    }
                    else if (pixel.r > 0.38f && pixel.g > 0.32f && pixel.b < 0.35f && Mathf.Abs(pixel.r - pixel.g) < 0.25f)
                    {
                        float pDetail = Mathf.PerlinNoise(x * 0.4f, y * 0.4f);
                        char pChar = pDetail < 0.5f ? '.' : ',';
                        _tiles[x, y] = new WorldTileData(pChar, true, pathColor, new Color32(0, 0, 0, 0), GameConstants.TILE_TYPE_PATH);
                    }
                    else if (pixel.maxColorComponent < 0.28f)
                    {
                        char wallGlyph = ((x + y) % 3 == 0) ? '#' : '%';
                        _tiles[x, y] = new WorldTileData(wallGlyph, false, wallColor, new Color32(0, 0, 0, 0), GameConstants.TILE_TYPE_WALL);
                    }
                    else if (pixel.r > 0.65f && pixel.g > 0.65f && pixel.b > 0.65f)
                    {
                        char rockGlyph = ((x + y) % 2 == 0) ? 'o' : 'O';
                        _tiles[x, y] = new WorldTileData(rockGlyph, false, rockColor, new Color32(0, 0, 0, 0), GameConstants.TILE_TYPE_OBSTACLE);
                    }
                    else
                    {
                        float gNoise = Mathf.PerlinNoise((x * 0.12f) + seedX, (y * 0.12f) + seedY);
                        Color32 gCol = gNoise < 0.55f ? groundColor : groundDetail;
                        _tiles[x, y] = new WorldTileData('|', true, gCol, new Color32(0, 0, 0, 0), GameConstants.TILE_TYPE_GROUND);
                    }
                }
            }

            if (_spawnFoliageOnCustomGround)
            {
                GenerateTreeCanopies(seedX, seedY, leafColor, denseCanopyColor);
                GenerateBushClusters(seedX, seedY, bushColor);
            }
        }

        public void GenerateDesignedEnvironment()
        {
            float seedX = (_seed % 1000) * 0.1f;
            float seedY = ((_seed / 1000) % 1000) * 0.1f;

            Color32 wallColor = _palette.WallSolid;
            Color32 wallDense = _palette.WallDense;
            Color32 rockColor = _palette.RockObstacle;
            Color32 groundColor = _palette.GroundPrimary;
            Color32 groundDetail = _palette.GroundSecondary;
            Color32 pathColor = _palette.PathDirt;
            Color32 waterColor = _palette.WaterPrimary;
            Color32 dataColor = _palette.DataStream;
            Color32 leafColor = _palette.CanopyLeaves;
            Color32 denseCanopyColor = _palette.CanopyDense;
            Color32 bushColor = _palette.Vegetation;

            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    if (x < 2 || x >= _width - 2 || y < 2 || y >= _height - 2)
                    {
                        char wallGlyph = (x == 0 || x == _width - 1 || y == 0 || y == _height - 1) ? '#' : '%';
                        _tiles[x, y] = new WorldTileData(wallGlyph, false, wallColor, new Color32(0, 0, 0, 0), GameConstants.TILE_TYPE_WALL);
                        continue;
                    }

                    float pathNoiseA = Mathf.PerlinNoise((x * 0.02f) + seedX, (y * 0.02f) + seedY);
                    float pathDistA = Mathf.Abs(pathNoiseA - 0.5f);

                    float pathNoiseB = Mathf.PerlinNoise((x * 0.035f) + seedX + 50f, (y * 0.035f) + seedY + 50f);
                    float pathDistB = Mathf.Abs(pathNoiseB - 0.5f);

                    bool isPath = (pathDistA < 0.045f) || (pathDistB < 0.035f);

                    float clearingNoise = Mathf.PerlinNoise((x * 0.015f) + seedX + 120f, (y * 0.015f) + seedY + 120f);
                    bool isClearing = clearingNoise > 0.72f;

                    if (isPath || isClearing)
                    {
                        float pDetail = Mathf.PerlinNoise(x * 0.4f, y * 0.4f);
                        char pChar = pDetail < 0.5f ? '.' : ',';
                        _tiles[x, y] = new WorldTileData(pChar, true, pathColor, new Color32(0, 0, 0, 0), GameConstants.TILE_TYPE_PATH);
                        continue;
                    }

                    float waterNoise = Mathf.PerlinNoise((x * 0.035f) + seedX + 70f, (y * 0.035f) + seedY + 70f);
                    if (waterNoise > 0.75f)
                    {
                        _tiles[x, y] = new WorldTileData(GameConstants.GLYPH_WATER_WAVE, _isWaterWalkable, waterColor, new Color32(0, 0, 0, 0), GameConstants.TILE_TYPE_WATER);
                        continue;
                    }

                    float gNoise = Mathf.PerlinNoise((x * 0.12f) + seedX, (y * 0.12f) + seedY);
                    Color32 gCol = gNoise < 0.55f ? groundColor : groundDetail;

                    _tiles[x, y] = new WorldTileData('|', true, gCol, new Color32(0, 0, 0, 0), GameConstants.TILE_TYPE_GROUND);
                }
            }

            GenerateRuinsAndColumns(wallColor, wallDense);

            GenerateRockFormations(seedX, seedY, rockColor);

            GenerateTreeCanopies(seedX, seedY, leafColor, denseCanopyColor);

            GenerateBushClusters(seedX, seedY, bushColor);

            GenerateDigitalConduits(seedX, seedY, dataColor);
        }

        private void GenerateRuinsAndColumns(Color32 wallColor, Color32 wallDense)
        {
            int[,] roomCenters = {
                { _width / 4, _height / 4 },
                { 3 * _width / 4, _height / 4 },
                { _width / 4, 3 * _height / 4 },
                { 3 * _width / 4, 3 * _height / 4 }
            };

            for (int r = 0; r < 4; r++)
            {
                int cx = roomCenters[r, 0];
                int cy = roomCenters[r, 1];
                int halfW = 8;
                int halfH = 6;

                for (int y = cy - halfH; y <= cy + halfH; y++)
                {
                    for (int x = cx - halfW; x <= cx + halfW; x++)
                    {
                        if (x <= 2 || x >= _width - 3 || y <= 2 || y >= _height - 3) continue;

                        bool isWall = (x == cx - halfW || x == cx + halfW || y == cy - halfH || y == cy + halfH);
                        bool isDoor = (x == cx && (y == cy - halfH || y == cy + halfH)) ||
                                      (y == cy && (x == cx - halfW || x == cx + halfW));

                        if (isWall && !isDoor)
                        {
                            char glyph = ((x + y) % 3 == 0) ? '#' : '%';
                            _tiles[x, y] = new WorldTileData(glyph, false, wallColor, new Color32(0, 0, 0, 0), GameConstants.TILE_TYPE_WALL);
                        }
                    }
                }

                if (cx > 5 && cx < _width - 5 && cy > 5 && cy < _height - 5)
                {
                    _tiles[cx, cy] = new WorldTileData('%', false, wallDense, new Color32(0, 0, 0, 0), GameConstants.TILE_TYPE_WALL);
                }
            }
        }

        private void GenerateRockFormations(float seedX, float seedY, Color32 rockColor)
        {
            for (int y = 5; y < _height - 5; y++)
            {
                for (int x = 5; x < _width - 5; x++)
                {
                    if (_tiles[x, y].TileType == GameConstants.TILE_TYPE_WALL ||
                        _tiles[x, y].TileType == GameConstants.TILE_TYPE_PATH ||
                        _tiles[x, y].TileType == GameConstants.TILE_TYPE_WATER)
                        continue;

                    float rockNoise = Mathf.PerlinNoise((x * 0.08f) + seedX + 80f, (y * 0.08f) + seedY + 80f);
                    if (rockNoise > 0.88f)
                    {
                        char rockGlyph = rockNoise > 0.93f ? 'O' : 'o';
                        _tiles[x, y] = new WorldTileData(rockGlyph, false, rockColor, new Color32(0, 0, 0, 0), GameConstants.TILE_TYPE_OBSTACLE);
                    }
                }
            }
        }

        private void GenerateTreeCanopies(float seedX, float seedY, Color32 leafColor, Color32 denseCanopyColor)
        {
            int step = 9;
            for (int gy = 6; gy < _height - 6; gy += step)
            {
                for (int gx = 6; gx < _width - 6; gx += step)
                {
                    float jx = (Mathf.PerlinNoise(gx * 0.1f + seedX, gy * 0.1f + seedY) - 0.5f) * (step * 0.6f);
                    float jy = (Mathf.PerlinNoise(gx * 0.1f + seedX + 30f, gy * 0.1f + seedY + 30f) - 0.5f) * (step * 0.6f);
                    int cx = Mathf.Clamp(Mathf.RoundToInt(gx + jx), 4, _width - 5);
                    int cy = Mathf.Clamp(Mathf.RoundToInt(gy + jy), 4, _height - 5);

                    if (_tiles[cx, cy].TileType == GameConstants.TILE_TYPE_PATH ||
                        _tiles[cx, cy].TileType == GameConstants.TILE_TYPE_WATER ||
                        _tiles[cx, cy].TileType == GameConstants.TILE_TYPE_WALL ||
                        _tiles[cx, cy].TileType == GameConstants.TILE_TYPE_OBSTACLE)
                        continue;

                    float treeChance = Mathf.PerlinNoise(cx * 0.04f + seedX + 150f, cy * 0.04f + seedY + 150f);
                    if (treeChance < 0.40f) continue;

                    float radius = Mathf.Lerp(2.2f, 3.8f, treeChance);
                    float radiusSqr = radius * radius;

                    int minX = Mathf.Max(2, Mathf.FloorToInt(cx - radius));
                    int maxX = Mathf.Min(_width - 3, Mathf.CeilToInt(cx + radius));
                    int minY = Mathf.Max(2, Mathf.FloorToInt(cy - radius));
                    int maxY = Mathf.Min(_height - 3, Mathf.CeilToInt(cy + radius));

                    for (int ty = minY; ty <= maxY; ty++)
                    {
                        for (int tx = minX; tx <= maxX; tx++)
                        {
                            if (_tiles[tx, ty].TileType == GameConstants.TILE_TYPE_PATH ||
                                _tiles[tx, ty].TileType == GameConstants.TILE_TYPE_WATER ||
                                _tiles[tx, ty].TileType == GameConstants.TILE_TYPE_WALL ||
                                _tiles[tx, ty].TileType == GameConstants.TILE_TYPE_OBSTACLE)
                                continue;

                            float dx = tx - cx;
                            float dy = ty - cy;
                            float distSqr = (dx * dx) + (dy * dy);

                            if (distSqr <= radiusSqr)
                            {
                                float normDist = Mathf.Sqrt(distSqr) / radius;

                                if (tx == cx && ty == cy)
                                {
                                    _tiles[tx, ty] = new WorldTileData('#', false, denseCanopyColor, new Color32(0, 0, 0, 0), GameConstants.TILE_TYPE_CANOPY);
                                }
                                else if (normDist < 0.45f)
                                {
                                    char coreGlyph = ((tx + ty) % 2 == 0) ? '%' : '&';
                                    _tiles[tx, ty] = new WorldTileData(coreGlyph, true, denseCanopyColor, new Color32(0, 0, 0, 0), GameConstants.TILE_TYPE_CANOPY);
                                }
                                else if (normDist < 0.80f)
                                {
                                    char midGlyph = ((tx + ty) % 2 == 0) ? '*' : 'o';
                                    _tiles[tx, ty] = new WorldTileData(midGlyph, true, leafColor, new Color32(0, 0, 0, 0), GameConstants.TILE_TYPE_CANOPY);
                                }
                                else
                                {
                                    char edgeGlyph = ((tx + ty) % 2 == 0) ? '+' : '~';
                                    _tiles[tx, ty] = new WorldTileData(edgeGlyph, true, leafColor, new Color32(0, 0, 0, 0), GameConstants.TILE_TYPE_CANOPY);
                                }
                            }
                        }
                    }
                }
            }
        }

        private void GenerateBushClusters(float seedX, float seedY, Color32 bushColor)
        {
            for (int y = 4; y < _height - 4; y++)
            {
                for (int x = 4; x < _width - 4; x++)
                {
                    if (_tiles[x, y].TileType != GameConstants.TILE_TYPE_GROUND) continue;

                    float bNoise = Mathf.PerlinNoise((x * 0.18f) + seedX + 200f, (y * 0.18f) + seedY + 200f);
                    if (bNoise > 0.76f)
                    {
                        char bushGlyph = bNoise > 0.84f ? '*' : 'o';
                        _tiles[x, y] = new WorldTileData(bushGlyph, true, bushColor, new Color32(0, 0, 0, 0), GameConstants.TILE_TYPE_VEGETATION);
                    }
                }
            }
        }

        private void GenerateDigitalConduits(float seedX, float seedY, Color32 dataColor)
        {
            for (int y = 4; y < _height - 4; y++)
            {
                for (int x = 4; x < _width - 4; x++)
                {
                    if (_tiles[x, y].TileType == GameConstants.TILE_TYPE_WALL ||
                        _tiles[x, y].TileType == GameConstants.TILE_TYPE_CANOPY ||
                        _tiles[x, y].TileType == GameConstants.TILE_TYPE_WATER)
                        continue;

                    float conduitNoise = Mathf.PerlinNoise((x * 0.025f) + seedX + 310f, (y * 0.025f) + seedY + 310f);
                    float conduitDist = Mathf.Abs(conduitNoise - 0.48f);

                    if (conduitDist < 0.012f)
                    {
                        char cGlyph = ((x + y) % 2 == 0) ? GameConstants.GLYPH_DATA_ZERO : GameConstants.GLYPH_DATA_ONE;
                        _tiles[x, y] = new WorldTileData(cGlyph, true, dataColor, new Color32(0, 0, 0, 0), GameConstants.TILE_TYPE_DATASTREAM);
                    }
                }
            }
        }

        public void RebuildPhysicsColliders()
        {
            if (_colliderRoot != null)
            {
                DestroyImmediate(_colliderRoot);
            }

            _colliderRoot = new GameObject("World_PhysicsColliders");
            _colliderRoot.transform.SetParent(transform, false);

            for (int y = 0; y < _height; y++)
            {
                int startX = -1;

                for (int x = 0; x < _width; x++)
                {
                    bool isSolid = !_tiles[x, y].IsWalkable;

                    if (isSolid && startX == -1)
                    {
                        startX = x;
                    }
                    else if (!isSolid && startX != -1)
                    {
                        CreateMergedCollider(startX, x - 1, y);
                        startX = -1;
                    }
                }

                if (startX != -1)
                {
                    CreateMergedCollider(startX, _width - 1, y);
                }
            }
        }

        private void CreateMergedCollider(int startX, int endX, int y)
        {
            int count = (endX - startX) + 1;
            float worldStartX = startX * _cellSize;
            float totalWidth = count * _cellSize;
            float centerX = worldStartX + (totalWidth * 0.5f);
            float centerY = (y + 0.5f) * _cellSize;

            var colObj = new GameObject($"WallRow_{y}_{startX}_{endX}");
            colObj.transform.SetParent(_colliderRoot.transform, false);
            colObj.transform.position = new Vector3(centerX, centerY, 0f);

            var box = colObj.AddComponent<BoxCollider2D>();
            box.size = new Vector2(totalWidth, _cellSize);
        }

        public WorldTileData GetTile(int x, int y)
        {
            if (_tiles == null || x < 0 || x >= _width || y < 0 || y >= _height)
            {
                return WorldTileData.DefaultWall(_palette != null ? _palette.WallSolid : new Color32(100, 100, 100, 255));
            }
            return _tiles[x, y];
        }

        public void SetTile(int x, int y, WorldTileData tile)
        {
            if (_tiles == null || x < 0 || x >= _width || y < 0 || y >= _height) return;

            _tiles[x, y] = tile;
            OnTileChanged?.Invoke(x, y, tile);
        }

        public bool IsWalkableWorld(Vector2 worldPos)
        {
            Vector2Int gridPos = WorldToGrid(worldPos);
            return IsWalkableGrid(gridPos.x, gridPos.y);
        }

        public bool IsWalkableGrid(int x, int y)
        {
            if (_tiles == null || x < 0 || x >= _width || y < 0 || y >= _height) return false;
            return _tiles[x, y].IsWalkable;
        }

        public Vector2Int WorldToGrid(Vector2 worldPos)
        {
            int x = Mathf.FloorToInt(worldPos.x / _cellSize);
            int y = Mathf.FloorToInt(worldPos.y / _cellSize);
            return new Vector2Int(x, y);
        }

        public Vector2 GridToWorld(Vector2Int gridPos)
        {
            return new Vector2((gridPos.x + 0.5f) * _cellSize, (gridPos.y + 0.5f) * _cellSize);
        }

        public Bounds GetWorldBounds()
        {
            Vector3 center = new Vector3((_width * _cellSize) * 0.5f, (_height * _cellSize) * 0.5f, 0f);
            Vector3 size = new Vector3(_width * _cellSize, _height * _cellSize, 1f);
            return new Bounds(center, size);
        }
    }
}
