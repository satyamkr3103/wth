using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using TMPro;
using AsciiActionGame.Core;
using AsciiActionGame.World;

namespace AsciiActionGame.Rendering
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class AsciiGridRenderer : MonoBehaviour, IAsciiRenderer
    {
        [Header("Diagnostic & Movement Modes")]
        [Tooltip("True: Anchors grid quads to world coordinates for sub-cell fractional camera scrolling without jumping.")]
        [SerializeField] private bool _useSubCellSmoothScrolling = true;

        [Tooltip("Diagnostic toggle to disable terrain wind/matrix animations to inspect camera motion purity.")]
        [SerializeField] private bool _enableTerrainAnimations = true;

        [Tooltip("Diagnostic toggle to disable screen shake, scanlines, and glitch effects.")]
        [SerializeField] private bool _enablePostProcessing = true;

        [Tooltip("Safety margin cells added to each boundary to prevent viewport clipping during sub-cell scrolling.")]
        [Range(1, 4)]
        [SerializeField] private int _marginCells = 2;

        [Header("Viewport Resolution")]
        [Tooltip("Number of horizontal character columns visible on screen.")]
        [SerializeField] private int _columns = 200;

        [Tooltip("Number of vertical character rows visible on screen.")]
        [SerializeField] private int _rows = 108;

        [Tooltip("In legacy mode: scale cell dimensions to fit viewport. In smooth mode: matches WorldMap.CellSize.")]
        [SerializeField] private bool _matchCameraViewport = true;

        [Tooltip("Manual monospaced character width in world units (overridden by WorldMap.CellSize in smooth mode).")]
        [SerializeField] private float _cellWidth = 0.25f;

        [Tooltip("Manual monospaced character height in world units (overridden by WorldMap.CellSize in smooth mode).")]
        [SerializeField] private float _cellHeight = 0.25f;

        [Tooltip("Aspect ratio adjustment for glyph quads within cells (width / height).")]
        [Range(0.4f, 1.0f)]
        [SerializeField] private float _glyphAspect = 0.86f;

        [Tooltip("Fill factor of glyph quad within cell boundaries.")]
        [Range(0.5f, 1.0f)]
        [SerializeField] private float _glyphScale = 0.92f;

        [Header("Font & Material")]
        [Tooltip("TextMeshPro font asset whose atlas texture contains ASCII glyphs.")]
        [SerializeField] private TMP_FontAsset _fontAsset;

        [Header("References")]
        [SerializeField] private AsciiWorldMap _worldMap;
        [SerializeField] private Camera _targetCamera;
        [SerializeField] private AsciiColorPalette _palette;

        [Header("Background Styling")]
        [SerializeField] private bool _renderBackgroundQuad = true;
        [SerializeField] private SpriteRenderer _backgroundQuadRenderer;

        private AsciiCell[,] _buffer;
        private Transform _cameraTransform;

        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Mesh _mesh;
        private Vector3[] _vertices;
        private Vector2[] _uvs;
        private Color32[] _colors;
        private int[] _triangles;

        private readonly Rect[] _asciiUvRects = new Rect[256];
        private bool _isUvCacheInitialized = false;

        private int _currentBaseGridX;
        private int _currentBaseGridY;
        private float _currentMeshOriginX;
        private float _currentMeshOriginY;

        public static AsciiGridRenderer Instance { get; private set; }

        public static event Action<IAsciiRenderer> OnRenderDynamicEntities;
        public static event Action<AsciiCell[,], int, int> OnPostProcessBuffer;

        public int Columns => _columns;
        public int Rows => _rows;
        public float CellWidth => _cellWidth;
        public float CellHeight => _cellHeight;
        public AsciiColorPalette Palette => _palette;
        public TMP_FontAsset FontAsset => _fontAsset;
        public float GlyphAspect
        {
            get => _glyphAspect;
            set
            {
                _glyphAspect = Mathf.Clamp(value, 0.4f, 1.0f);
                BuildProceduralMesh();
            }
        }
        public float GlyphScale
        {
            get => _glyphScale;
            set
            {
                _glyphScale = Mathf.Clamp(value, 0.5f, 1.0f);
                BuildProceduralMesh();
            }
        }

        public bool UseSubCellSmoothScrolling { get => _useSubCellSmoothScrolling; set => _useSubCellSmoothScrolling = value; }
        public bool EnableTerrainAnimations { get => _enableTerrainAnimations; set => _enableTerrainAnimations = value; }
        public bool EnablePostProcessing { get => _enablePostProcessing; set => _enablePostProcessing = value; }

        private void Awake()
        {
            Instance = this;

            _meshFilter = GetComponent<MeshFilter>();
            _meshRenderer = GetComponent<MeshRenderer>();

            TextMeshPro tmp = GetComponent<TextMeshPro>();
            if (tmp != null)
            {
                if (_fontAsset == null)
                {
                    _fontAsset = tmp.font;
                }
                tmp.enabled = false;
            }

            if (_targetCamera == null)
            {
                _targetCamera = Camera.main;
            }

            if (_targetCamera != null)
            {
                _cameraTransform = _targetCamera.transform;
            }

            if (_worldMap == null)
            {
                _worldMap = FindFirstObjectByType<AsciiWorldMap>();
            }

            if (_palette == null && _worldMap != null)
            {
                _palette = _worldMap.Palette;
            }

            if (_palette == null)
            {
                _palette = AsciiColorPalette.CreateDefault();
            }

            if (_glyphAspect < 0.80f)
            {
                _glyphAspect = 0.86f;
            }
            InitializeRenderer();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            if (_mesh != null)
            {
                Destroy(_mesh);
            }
        }

        public void InitializeRenderer()
        {
            UpdateCellMetrics();

            _buffer = new AsciiCell[_columns, _rows];

            InitializeFontCache();
            BuildProceduralMesh();
            SetupBackgroundQuad();
        }

        private void UpdateCellMetrics()
        {
            float targetCellSize = _worldMap != null ? _worldMap.CellSize : 0.25f;

            if (_useSubCellSmoothScrolling)
            {
                _cellWidth = targetCellSize;
                _cellHeight = targetCellSize;

                if (_targetCamera != null)
                {
                    float camHeight = _targetCamera.orthographicSize * 2.0f;
                    float camWidth = camHeight * _targetCamera.aspect;

                    int visibleCols = Mathf.CeilToInt(camWidth / _cellWidth);
                    int visibleRows = Mathf.CeilToInt(camHeight / _cellHeight);

                    int neededCols = visibleCols + (_marginCells * 2);
                    int neededRows = visibleRows + (_marginCells * 2);

                    if (neededCols != _columns || neededRows != _rows)
                    {
                        _columns = neededCols;
                        _rows = neededRows;
                        _buffer = new AsciiCell[_columns, _rows];
                        BuildProceduralMesh();
                    }
                }
            }
            else
            {
                if (_matchCameraViewport && _targetCamera != null)
                {
                    float camHeight = _targetCamera.orthographicSize * 2.0f;
                    float camWidth = camHeight * _targetCamera.aspect;

                    _cellWidth = camWidth / _columns;
                    _cellHeight = camHeight / _rows;
                }
            }
        }

        private void InitializeFontCache()
        {
            if (_fontAsset == null)
            {
                _fontAsset = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
                if (_fontAsset == null)
                {
                    _fontAsset = TMP_Settings.defaultFontAsset;
                }
            }

            if (_fontAsset == null)
            {
                Debug.LogWarning("[AsciiGridRenderer] No TMP_FontAsset found for UV caching!");
                return;
            }

            if (_meshRenderer != null && _fontAsset.material != null)
            {
                _meshRenderer.sharedMaterial = _fontAsset.material;
            }

            Texture2D atlasTex = _fontAsset.atlasTexture;
            float atlasW = atlasTex != null ? atlasTex.width : 512f;
            float atlasH = atlasTex != null ? atlasTex.height : 512f;

            for (int i = 0; i < 256; i++)
            {
                _asciiUvRects[i] = Rect.zero;
            }

            foreach (var ch in _fontAsset.characterTable)
            {
                uint unicode = ch.unicode;
                if (unicode < 256 && ch.glyph != null)
                {
                    var glyphRect = ch.glyph.glyphRect;
                    float uMin = glyphRect.x / atlasW;
                    float vMin = glyphRect.y / atlasH;
                    float uWidth = glyphRect.width / atlasW;
                    float vHeight = glyphRect.height / atlasH;

                    _asciiUvRects[unicode] = new Rect(uMin, vMin, uWidth, vHeight);
                }
            }

            _isUvCacheInitialized = true;
        }

        private void BuildProceduralMesh()
        {
            int totalCells = _columns * _rows;
            int vertexCount = totalCells * 4;
            int triangleCount = totalCells * 6;

            _vertices = new Vector3[vertexCount];
            _uvs = new Vector2[vertexCount];
            _colors = new Color32[vertexCount];
            _triangles = new int[triangleCount];

            float quadHeight = _cellHeight * _glyphScale;
            float quadWidth = quadHeight * _glyphAspect;
            if (quadWidth > _cellWidth * _glyphScale)
            {
                quadWidth = _cellWidth * _glyphScale;
            }

            float padX = (_cellWidth - quadWidth) * 0.5f;
            float padY = (_cellHeight - quadHeight) * 0.5f;

            Color32 initColor = Color.clear;

            for (int r = 0; r < _rows; r++)
            {
                for (int c = 0; c < _columns; c++)
                {
                    int cellIndex = (r * _columns) + c;
                    int vBase = cellIndex * 4;
                    int tBase = cellIndex * 6;

                    float x0 = (c * _cellWidth) + padX;
                    float x1 = x0 + quadWidth;
                    float y1 = -(r * _cellHeight) - padY;
                    float y0 = y1 - quadHeight;

                    _vertices[vBase + 0] = new Vector3(x0, y1, 0f);
                    _vertices[vBase + 1] = new Vector3(x1, y1, 0f);
                    _vertices[vBase + 2] = new Vector3(x1, y0, 0f);
                    _vertices[vBase + 3] = new Vector3(x0, y0, 0f);

                    _uvs[vBase + 0] = Vector2.zero;
                    _uvs[vBase + 1] = Vector2.zero;
                    _uvs[vBase + 2] = Vector2.zero;
                    _uvs[vBase + 3] = Vector2.zero;

                    _colors[vBase + 0] = initColor;
                    _colors[vBase + 1] = initColor;
                    _colors[vBase + 2] = initColor;
                    _colors[vBase + 3] = initColor;

                    _triangles[tBase + 0] = vBase + 0;
                    _triangles[tBase + 1] = vBase + 1;
                    _triangles[tBase + 2] = vBase + 2;
                    _triangles[tBase + 3] = vBase + 0;
                    _triangles[tBase + 4] = vBase + 2;
                    _triangles[tBase + 5] = vBase + 3;
                }
            }

            if (_mesh == null)
            {
                _mesh = new Mesh { name = "AsciiGrid_ProceduralMesh" };
            }
            else
            {
                _mesh.Clear();
            }

            _mesh.indexFormat = IndexFormat.UInt32;
            _mesh.vertices = _vertices;
            _mesh.uv = _uvs;
            _mesh.colors32 = _colors;
            _mesh.triangles = _triangles;
            _mesh.RecalculateBounds();

            _meshFilter.sharedMesh = _mesh;
        }

        private void SetupBackgroundQuad()
        {
            if (!_renderBackgroundQuad) return;

            GameObject bgObj = transform.Find("BackgroundQuad")?.gameObject;
            if (bgObj == null)
            {
                bgObj = new GameObject("BackgroundQuad");
                bgObj.transform.SetParent(transform, false);
                _backgroundQuadRenderer = bgObj.AddComponent<SpriteRenderer>();

                Texture2D tex = new Texture2D(1, 1);
                tex.SetPixel(0, 0, Color.white);
                tex.Apply();
                _backgroundQuadRenderer.sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
                _backgroundQuadRenderer.sortingOrder = -100;
            }

            float totalWidth = _columns * _cellWidth;
            float totalHeight = _rows * _cellHeight;
            bgObj.transform.localScale = new Vector3(totalWidth * 1.5f, totalHeight * 1.5f, 1f);
            bgObj.transform.localPosition = new Vector3(totalWidth * 0.5f, -totalHeight * 0.5f, 1f);
            _backgroundQuadRenderer.color = _palette.CanvasBackground;
        }

        private void LateUpdate()
        {
            if (_cameraTransform == null) return;

            UpdateCellMetrics();

            Vector3 camPos = _cameraTransform.position;
            Vector2 shake = (_enablePostProcessing && AsciiPostProcessing.Instance != null) ? AsciiPostProcessing.Instance.CurrentShakeOffset : Vector2.zero;

            if (_useSubCellSmoothScrolling && _targetCamera != null)
            {
                float halfCamW = _targetCamera.orthographicSize * _targetCamera.aspect;
                float halfCamH = _targetCamera.orthographicSize;

                float camLeft = camPos.x - halfCamW;
                float camTop = camPos.y + halfCamH;

                _currentBaseGridX = Mathf.FloorToInt(camLeft / _cellWidth) - _marginCells;
                _currentBaseGridY = Mathf.CeilToInt(camTop / _cellHeight) + _marginCells;

                _currentMeshOriginX = _currentBaseGridX * _cellWidth;
                _currentMeshOriginY = _currentBaseGridY * _cellHeight;

                transform.position = new Vector3(_currentMeshOriginX + shake.x, _currentMeshOriginY + shake.y, 0f);
            }
            else
            {
                float halfWidth = (_columns * _cellWidth) * 0.5f;
                float halfHeight = (_rows * _cellHeight) * 0.5f;

                transform.position = new Vector3(camPos.x - halfWidth + shake.x, camPos.y + halfHeight + shake.y, 0f);
            }

            ClearBuffer();

            RasterizeWorldMap();

            OnRenderDynamicEntities?.Invoke(this);

            if (_enablePostProcessing)
            {
                OnPostProcessBuffer?.Invoke(_buffer, _columns, _rows);
            }

            Present();
        }

        public void ClearBuffer()
        {
            Color32 defaultBg = _palette != null ? _palette.CanvasBackground : new Color32(0, 0, 0, 255);
            for (int y = 0; y < _rows; y++)
            {
                for (int x = 0; x < _columns; x++)
                {
                    _buffer[x, y].Set(' ', Color.clear, defaultBg, -1);
                }
            }
        }

        private void RasterizeWorldMap()
        {
            if (_worldMap == null) return;

            if (_useSubCellSmoothScrolling)
            {
                for (int r = 0; r < _rows; r++)
                {
                    int gy = _currentBaseGridY - r;

                    for (int c = 0; c < _columns; c++)
                    {
                        int gx = _currentBaseGridX + c;

                        WorldTileData tile = _worldMap.GetTile(gx, gy);

                        if (_enableTerrainAnimations && AnimatedTerrainTile.Instance != null)
                        {
                            AnimatedTerrainTile.Instance.TryGetAnimatedTile(gx, gy, tile, out tile);
                        }

                        if (tile.Glyph == ' ' && tile.TileType == GameConstants.TILE_TYPE_GROUND)
                        {
                            tile.Glyph = '|';
                        }

                        if (tile.Glyph != ' ' || tile.BackgroundColor.a > 0)
                        {
                            float ao = _worldMap.GetAO(gx, gy);
                            Color32 shadedColor = AsciiShadingRamp.ApplyShading(tile.Color, ao, 1.0f);
                            char shadedGlyph = AsciiShadingRamp.ShadedTerrainGlyph(tile.Glyph, ao);

                            _buffer[c, r].Set(shadedGlyph, shadedColor, tile.BackgroundColor, GameConstants.LAYER_BACKGROUND_TERRAIN);
                        }
                    }
                }
            }
            else
            {
                Vector2 camPos = _cameraTransform.position;
                float startWorldX = camPos.x - (_columns * _cellWidth) * 0.5f;
                float startWorldY = camPos.y + (_rows * _cellHeight) * 0.5f;

                for (int y = 0; y < _rows; y++)
                {
                    float worldY = startWorldY - (y + 0.5f) * _cellHeight;

                    for (int x = 0; x < _columns; x++)
                    {
                        float worldX = startWorldX + (x + 0.5f) * _cellWidth;
                        Vector2Int gridCoord = _worldMap.WorldToGrid(new Vector2(worldX, worldY));

                        WorldTileData tile = _worldMap.GetTile(gridCoord.x, gridCoord.y);

                        if (_enableTerrainAnimations && AnimatedTerrainTile.Instance != null)
                        {
                            AnimatedTerrainTile.Instance.TryGetAnimatedTile(gridCoord.x, gridCoord.y, tile, out tile);
                        }

                        if (tile.Glyph == ' ' && tile.TileType == GameConstants.TILE_TYPE_GROUND)
                        {
                            tile.Glyph = '|';
                        }

                        if (tile.Glyph != ' ' || tile.BackgroundColor.a > 0)
                        {
                            float ao = _worldMap.GetAO(gridCoord.x, gridCoord.y);
                            Color32 shadedColor = AsciiShadingRamp.ApplyShading(tile.Color, ao, 1.0f);
                            char shadedGlyph = AsciiShadingRamp.ShadedTerrainGlyph(tile.Glyph, ao);

                            _buffer[x, y].Set(shadedGlyph, shadedColor, tile.BackgroundColor, GameConstants.LAYER_BACKGROUND_TERRAIN);
                        }
                    }
                }
            }
        }

        public void DrawGlyph(int viewX, int viewY, char glyph, Color32 foreground, Color32 background, int layer)
        {
            if (viewX < 0 || viewX >= _columns || viewY < 0 || viewY >= _rows) return;

            if (layer >= _buffer[viewX, viewY].Layer)
            {
                _buffer[viewX, viewY].Set(glyph, foreground, background, layer);
            }
        }

        public void DrawWorldGlyph(Vector2 worldPos, char glyph, Color32 foreground, Color32 background, int layer)
        {
            Vector2Int viewCoord = WorldToViewport(worldPos);
            DrawGlyph(viewCoord.x, viewCoord.y, glyph, foreground, background, layer);
        }

        public Vector2Int WorldToViewport(Vector2 worldPos)
        {
            if (_useSubCellSmoothScrolling)
            {
                int c = Mathf.FloorToInt((worldPos.x - _currentMeshOriginX) / _cellWidth);
                int r = Mathf.FloorToInt((_currentMeshOriginY - worldPos.y) / _cellHeight);
                return new Vector2Int(c, r);
            }
            else
            {
                if (_cameraTransform == null) return new Vector2Int(-1, -1);

                Vector2 camPos = _cameraTransform.position;
                float startWorldX = camPos.x - (_columns * _cellWidth) * 0.5f;
                float startWorldY = camPos.y + (_rows * _cellHeight) * 0.5f;

                int x = Mathf.FloorToInt((worldPos.x - startWorldX) / _cellWidth);
                int y = Mathf.FloorToInt((startWorldY - worldPos.y) / _cellHeight);

                return new Vector2Int(x, y);
            }
        }

        public Vector2 ViewportToWorld(int viewX, int viewY)
        {
            if (_useSubCellSmoothScrolling)
            {
                float worldX = _currentMeshOriginX + (viewX + 0.5f) * _cellWidth;
                float worldY = _currentMeshOriginY - (viewY + 0.5f) * _cellHeight;
                return new Vector2(worldX, worldY);
            }
            else
            {
                if (_cameraTransform == null) return Vector2.zero;

                Vector2 camPos = _cameraTransform.position;
                float startWorldX = camPos.x - (_columns * _cellWidth) * 0.5f;
                float startWorldY = camPos.y + (_rows * _cellHeight) * 0.5f;

                float worldX = startWorldX + (viewX + 0.5f) * _cellWidth;
                float worldY = startWorldY - (viewY + 0.5f) * _cellHeight;

                return new Vector2(worldX, worldY);
            }
        }

        private void OnValidate()
        {
            if (Application.isPlaying && _mesh != null)
            {
                BuildProceduralMesh();
            }
        }

        public void Present()
        {
            if (!_isUvCacheInitialized || _mesh == null) return;

            for (int r = 0; r < _rows; r++)
            {
                for (int c = 0; c < _columns; c++)
                {
                    int cellIndex = (r * _columns) + c;
                    int vBase = cellIndex * 4;

                    char glyph = _buffer[c, r].Glyph;
                    Color32 color = _buffer[c, r].ForegroundColor;

                    if (glyph == ' ' || glyph == ' ' || color.a == 0)
                    {
                        _uvs[vBase + 0] = Vector2.zero;
                        _uvs[vBase + 1] = Vector2.zero;
                        _uvs[vBase + 2] = Vector2.zero;
                        _uvs[vBase + 3] = Vector2.zero;

                        _colors[vBase + 0] = Color.clear;
                        _colors[vBase + 1] = Color.clear;
                        _colors[vBase + 2] = Color.clear;
                        _colors[vBase + 3] = Color.clear;
                    }
                    else
                    {
                        int asciiCode = (int)glyph;
                        if (asciiCode < 0 || asciiCode >= 256) asciiCode = 63;

                        Rect uvRect = _asciiUvRects[asciiCode];

                        float uMin = uvRect.xMin;
                        float vMin = uvRect.yMin;
                        float uMax = uvRect.xMax;
                        float vMax = uvRect.yMax;

                        _uvs[vBase + 0] = new Vector2(uMin, vMax);
                        _uvs[vBase + 1] = new Vector2(uMax, vMax);
                        _uvs[vBase + 2] = new Vector2(uMax, vMin);
                        _uvs[vBase + 3] = new Vector2(uMin, vMin);

                        _colors[vBase + 0] = color;
                        _colors[vBase + 1] = color;
                        _colors[vBase + 2] = color;
                        _colors[vBase + 3] = color;
                    }
                }
            }

            _mesh.SetUVs(0, _uvs);
            _mesh.SetColors(_colors);
        }
    }
}
