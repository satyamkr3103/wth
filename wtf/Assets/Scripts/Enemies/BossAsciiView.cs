using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using TMPro;
using AsciiActionGame.Core;
using AsciiActionGame.Rendering;
using AsciiActionGame.Combat;

namespace AsciiActionGame.Enemies
{
    [RequireComponent(typeof(BossEntity))]
    public class BossAsciiView : MonoBehaviour
    {
        [Header("Procedural Mesh Sizing")]
        [SerializeField] private float _glyphWidth = 0.155f;
        [SerializeField] private float _glyphHeight = 0.18f;
        [SerializeField] private Vector2 _meshOffset = new Vector2(0f, 0f);

        [Header("Colors")]
        [SerializeField] private Color32 _colorArmor = new Color32(200, 70, 40, 255);
        [SerializeField] private Color32 _colorArmorDark = new Color32(140, 40, 25, 255);
        [SerializeField] private Color32 _colorMetal = new Color32(180, 190, 205, 255);
        [SerializeField] private Color32 _colorCore = new Color32(255, 40, 80, 255);
        [SerializeField] private Color32 _colorCoreHot = new Color32(255, 220, 60, 255);
        [SerializeField] private Color32 _colorOptics = new Color32(255, 240, 50, 255);
        [SerializeField] private Color32 _colorGatling = new Color32(255, 140, 30, 255);

        private const int MAX_BOSS_QUADS = 750;

        private BossEntity _boss;
        private Damageable _damageable;
        private IAsciiRenderer _renderer;
        private TMP_FontAsset _fontAsset;

        private GameObject _meshObj;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Mesh _mesh;
        private Vector3[] _vertices;
        private Vector2[] _uvs;
        private Color32[] _colors;
        private int[] _triangles;
        private readonly Rect[] _asciiUvRects = new Rect[256];
        private bool _isUvCacheInitialized = false;

        private struct BossGlyphDef
        {
            public short Col;
            public short Row;
            public char Glyph;
            public byte Type;
        }

        private BossGlyphDef[] _glyphDefs;
        private int _totalQuads = 0;

        private static readonly string[] BOSS_ASCII_TEMPLATE = new string[]
        {
            "   /==============================\\   ",
            "  <  [:::]   /==============\\   [:::]  > ",
            "  <  [:::]  |  {XXXXXXXXXX}  |  [:::]  > ",
            " ==[======]{================}[======]== ",
            " ==| |##| |  {============}  | |##| |== ",
            " ==| |##| |  |  [<O>] [<O>] |  | |##| |== ",
            " ==[======]  |  [<O>] [<O>] |  [======]== ",
            "   \\  ||  /   \\  \\========/  /   \\  ||  / ",
            "  {========}  {============}  {========} ",
            "  {========}| { (@@@@@@@@) } |{========} ",
            "  {========}| {( *@@@@@@* )} |{========} ",
            "  {========}| { (@@@@@@@@) } |{========} ",
            "  {======}   {============}   {======} ",
            "   \\====/     |  ::::::::  |     \\====/ ",
            "   /====\\    /==============\\    /====\\ ",
            "  | |##| |  | |##||####||##| |  | |##| | ",
            "  | |##| |  | |##||####||##| |  | |##| | ",
            "  [======]  [==============]  [======] ",
            "  [======]   [============]   [======] ",
            "  \\======/    \\==========/    \\======/ "
        };

        private void Awake()
        {
            _boss = GetComponent<BossEntity>();
            _damageable = GetComponent<Damageable>();
            _renderer = FindFirstObjectByType<AsciiGridRenderer>();

            ParseTemplate();
            SetupMesh();
        }

        private void ParseTemplate()
        {
            var list = new List<BossGlyphDef>(MAX_BOSS_QUADS);
            for (short r = 0; r < BOSS_ASCII_TEMPLATE.Length; r++)
            {
                string rowStr = BOSS_ASCII_TEMPLATE[r];
                for (short c = 0; c < rowStr.Length; c++)
                {
                    char ch = rowStr[c];
                    if (ch == ' ' || ch == '\0') continue;

                    byte type = 0;
                    if (ch == '@' || ch == '*') type = 1;
                    else if (ch == 'O' || ch == '<' || ch == '>') type = 2;
                    else if (ch == '=' || ch == '#') type = 3;
                    else if (ch == ':' || ch == 'X') type = 4;
                    else if (ch == '[' || ch == ']' || ch == '{' || ch == '}') type = 5;

                    list.Add(new BossGlyphDef
                    {
                        Col = c,
                        Row = r,
                        Glyph = ch,
                        Type = type
                    });
                }
            }

            _glyphDefs = list.ToArray();
            _totalQuads = Mathf.Min(_glyphDefs.Length, MAX_BOSS_QUADS);
        }

        private void SetupMesh()
        {
            _meshObj = new GameObject("Boss_AsciiMesh");
            _meshObj.transform.SetParent(transform, false);
            _meshObj.transform.localPosition = new Vector3(_meshOffset.x, _meshOffset.y, -0.1f);

            _meshFilter = _meshObj.AddComponent<MeshFilter>();
            _meshRenderer = _meshObj.AddComponent<MeshRenderer>();
            _meshRenderer.sortingOrder = 55;

            var gridRenderer = FindFirstObjectByType<AsciiGridRenderer>();
            if (gridRenderer != null && gridRenderer.FontAsset != null)
            {
                _fontAsset = gridRenderer.FontAsset;
            }

            if (_fontAsset == null)
            {
                _fontAsset = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
                if (_fontAsset == null)
                {
                    _fontAsset = TMP_Settings.defaultFontAsset;
                }
            }

            if (_fontAsset != null && _fontAsset.material != null)
            {
                _meshRenderer.sharedMaterial = _fontAsset.material;
                InitializeUvCache();
            }

            BuildMeshBuffers();
        }

        private void InitializeUvCache()
        {
            if (_fontAsset == null || _fontAsset.atlasTexture == null) return;

            Texture2D atlasTex = _fontAsset.atlasTexture;
            float atlasW = atlasTex.width;
            float atlasH = atlasTex.height;

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

        private void BuildMeshBuffers()
        {
            int vCount = MAX_BOSS_QUADS * 4;
            int tCount = MAX_BOSS_QUADS * 6;

            _vertices = new Vector3[vCount];
            _uvs = new Vector2[vCount];
            _colors = new Color32[vCount];
            _triangles = new int[tCount];

            for (int i = 0; i < MAX_BOSS_QUADS; i++)
            {
                int vBase = i * 4;
                int tBase = i * 6;

                _triangles[tBase + 0] = vBase + 0;
                _triangles[tBase + 1] = vBase + 1;
                _triangles[tBase + 2] = vBase + 2;
                _triangles[tBase + 3] = vBase + 0;
                _triangles[tBase + 4] = vBase + 2;
                _triangles[tBase + 5] = vBase + 3;
            }

            _mesh = new Mesh { name = "Boss_ProceduralSpriteMesh" };
            _mesh.indexFormat = IndexFormat.UInt16;
            _mesh.MarkDynamic();
            _mesh.vertices = _vertices;
            _mesh.uv = _uvs;
            _mesh.colors32 = _colors;
            _mesh.triangles = _triangles;
            _mesh.RecalculateBounds();

            _meshFilter.sharedMesh = _mesh;
        }

        private void LateUpdate()
        {
            if (!_isUvCacheInitialized || _mesh == null || _glyphDefs == null) return;
            RenderBossMesh();
        }

        private void RenderBossMesh()
        {
            bool isFlashing = _damageable != null && _damageable.IsFlashing;
            bool isEnraged = _boss != null && _boss.IsEnraged;

            float time = Time.time;
            float heave = Mathf.Sin(time * 2.2f) * 0.05f;

            _meshObj.transform.localPosition = new Vector3(_meshOffset.x, _meshOffset.y + heave, -0.1f);

            float centerCol = 19.5f;
            float centerRow = 10.0f;

            int corePhase = (int)(time * 7f) % 4;
            char dynamicCoreGlyph = (corePhase == 0) ? '@' : ((corePhase == 1) ? '*' : ((corePhase == 2) ? 'O' : '%'));
            Color32 coreCol = (corePhase % 2 == 0) ? _colorCore : _colorCoreHot;

            for (int i = 0; i < _totalQuads; i++)
            {
                int vBase = i * 4;
                var def = _glyphDefs[i];

                float localX = (def.Col - centerCol) * _glyphWidth;
                float localY = (centerRow - def.Row) * _glyphHeight;

                char glyph = def.Glyph;
                Color32 col;

                if (def.Type == 1)
                {
                    glyph = dynamicCoreGlyph;
                    col = isEnraged ? new Color32(255, 20, 20, 255) : coreCol;
                }
                else if (def.Type == 2)
                {
                    col = _colorOptics;
                }
                else if (def.Type == 3)
                {
                    col = isEnraged ? new Color32(255, 80, 40, 255) : _colorArmor;
                }
                else if (def.Type == 4)
                {
                    col = _colorArmorDark;
                }
                else if (def.Type == 5)
                {
                    col = _colorMetal;
                }
                else
                {
                    col = _colorArmor;
                }

                if (isFlashing) col = Color.white;

                float x0 = localX - (_glyphWidth * 0.5f);
                float x1 = localX + (_glyphWidth * 0.5f);
                float y0 = localY - (_glyphHeight * 0.5f);
                float y1 = localY + (_glyphHeight * 0.5f);

                _vertices[vBase + 0] = new Vector3(x0, y1, 0f);
                _vertices[vBase + 1] = new Vector3(x1, y1, 0f);
                _vertices[vBase + 2] = new Vector3(x1, y0, 0f);
                _vertices[vBase + 3] = new Vector3(x0, y0, 0f);

                int ascii = (int)glyph;
                if (ascii < 0 || ascii >= 256) ascii = 63;
                Rect uv = _asciiUvRects[ascii];

                _uvs[vBase + 0] = new Vector2(uv.xMin, uv.yMax);
                _uvs[vBase + 1] = new Vector2(uv.xMax, uv.yMax);
                _uvs[vBase + 2] = new Vector2(uv.xMax, uv.yMin);
                _uvs[vBase + 3] = new Vector2(uv.xMin, uv.yMin);

                _colors[vBase + 0] = col;
                _colors[vBase + 1] = col;
                _colors[vBase + 2] = col;
                _colors[vBase + 3] = col;
            }

            for (int i = _totalQuads; i < MAX_BOSS_QUADS; i++)
            {
                int vBase = i * 4;
                _vertices[vBase + 0] = Vector3.zero;
                _vertices[vBase + 1] = Vector3.zero;
                _vertices[vBase + 2] = Vector3.zero;
                _vertices[vBase + 3] = Vector3.zero;

                _colors[vBase + 0] = Color.clear;
                _colors[vBase + 1] = Color.clear;
                _colors[vBase + 2] = Color.clear;
                _colors[vBase + 3] = Color.clear;
            }

            _mesh.SetVertices(_vertices);
            _mesh.SetUVs(0, _uvs);
            _mesh.SetColors(_colors);
        }
    }
}
