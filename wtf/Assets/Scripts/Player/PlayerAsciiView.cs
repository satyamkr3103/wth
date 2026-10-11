using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using TMPro;
using AsciiActionGame.Core;
using AsciiActionGame.Rendering;
using AsciiActionGame.Combat;
using AsciiActionGame.Animation;

namespace AsciiActionGame.Player
{
    [RequireComponent(typeof(PlayerController))]
    public class PlayerAsciiView : MonoBehaviour
    {
        [Header("Smooth Sub-Pixel ASCII Sprite Rendering")]
        [SerializeField] private bool _useSubPixelSmoothSprite = true;

        [Header("High-Res ASCII Character Sizing")]
        [SerializeField] [Range(0.2f, 5.0f)] private float _characterScale = 0.85f;
        [SerializeField] private float _baseCharHeight = 0.052f;
        [SerializeField] private float _charAspectRatio = 0.55125f;
        [SerializeField] private float _glyphSizeFactor = 1.05f;
        [SerializeField] private Vector2 _characterOffset = new Vector2(0f, 0.10f);

        [Header("High-Res ASCII Palette")]
        [SerializeField] private Color32 _colorHash = new Color32(225, 240, 255, 255);
        [SerializeField] private Color32 _colorPlus = new Color32(40, 220, 255, 255);
        [SerializeField] private Color32 _colorMinus = new Color32(90, 140, 175, 255);
        [SerializeField] private Color32 _colorDot = new Color32(255, 255, 255, 255);
        [SerializeField] private Color32 _colorMuzzleFlash = new Color32(255, 240, 100, 255);

        [Header("Gun In Hands Settings")]
        [SerializeField] private bool _showGunInHands = true;
        [SerializeField] private float _gunDistance = 0.52f;
        [SerializeField] private Color32 _colorGunBody = new Color32(40, 200, 240, 255);
        [SerializeField] private Color32 _colorGunBarrel = new Color32(0, 240, 255, 255);
        [SerializeField] private Color32 _colorGunSight = new Color32(255, 60, 60, 255);

        [Header("Custom Animation Clips (Aseprite / Sprites / Text)")]
        [SerializeField] private AsciiAnimationClip _clipIdleDown;
        [SerializeField] private AsciiAnimationClip _clipIdleUp;
        [SerializeField] private AsciiAnimationClip _clipIdleSide;
        [SerializeField] private AsciiAnimationClip _clipRunDown;
        [SerializeField] private AsciiAnimationClip _clipRunUp;
        [SerializeField] private AsciiAnimationClip _clipRunSide;
        [SerializeField] private AsciiAnimationClip _clipFire;
        [SerializeField] private AsciiAnimationClip _clipDeath;

        [Header("Trail Settings")]
        [SerializeField] private bool _enableTrail = true;
        [SerializeField] private int _maxTrailPoints = 8;
        [SerializeField] private float _trailSpawnDistance = 0.25f;
        [SerializeField] private float _trailLifetime = 0.28f;

        [Header("Palette Reference")]
        [SerializeField] private AsciiColorPalette _palette;

        [Header("Font Asset")]
        [SerializeField] private TMP_FontAsset _fontAsset;

        private PlayerController _controller;
        private Damageable _damageable;
        private IAsciiRenderer _renderer;
        private readonly List<TrailPoint> _trailPoints = new List<TrailPoint>();
        private Vector2 _lastTrailPos;

        private float _animTime = 0f;
        private float _fireTimer = 0f;
        private const float FIRE_DURATION = 0.12f;

        private int _currentFacingDir = 0;
        private bool _wasFlashing = false;

        private GameObject _spriteObj;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Mesh _mesh;
        private Vector3[] _vertices;
        private Vector2[] _uvs;
        private Color32[] _colors;
        private int[] _triangles;
        private readonly Rect[] _asciiUvRects = new Rect[256];
        private bool _isUvCacheInitialized = false;

        private Vector2 _gunMuzzleWorldPos;
        public Vector2 GunMuzzleWorldPosition => (_gunMuzzleWorldPos != Vector2.zero) ? _gunMuzzleWorldPos : (Vector2)transform.position;

        private struct AsciiGlyphDef
        {
            public short Col;
            public short Row;
            public char Glyph;
            public byte Type;
        }

        private AsciiGlyphDef[] _glyphDefs;
        private int _totalQuads = 0;

        private struct TrailPoint
        {
            public Vector2 Position;
            public float RemainingTime;
            public float MaxTime;
            public char Glyph;
        }

        private static readonly string[] ASCII_CHARACTER_TEMPLATE = new string[]
        {
            "                                         -##",
            "                                        ####",
            "                           ###        ######    #####",
            "                           ######+ ###++###########    ######",
            "                             ########+++##########+  #######",
            "                 ####### ###########+++#####################",
            "                 ##################++++##########++++++######  #####+",
            "         +#+      ####++++++###++##++++###+++----+++#######++++++###",
            "         +###     ######++--++##########++++++++######+++---++++###",
            "          ###############+++++++########++###########++++++++########",
            "           #####++++----++++++##########################+++++##+#++#####    ###",
            "  #+       #######+++++++++####+++#########+##########+##+####++########   ####",
            "  ###############+######+++--+++++######+++++++#############+++################",
            "   ############+++###++----+--+#++######++-+++--+####+##+++#+++++++++#########",
            "   ###########++##+++---+++#++++++####++#++-+#+-++###+++#++-+###############",
            "     ############++--++#++#+###++#####++++++++#++++###++++++--+#############",
            "       #########++++#+############-  +##+###++###+######+++++++++#############   +#",
            "##   +##########+++#############     +######++#####   ###+++###++##################",
            "###########################+###        #####+##+##+   .##########++###############",
            "  ########################  -#          ####### -.      + ######################",
            "   #####################-     #####      ####-     #####  ####################",
            "    ##################-    .########.    ###     ########   ################",
            "      ################-   ###           ##             -##   #################",
            "      ################-                                      ####################",
            "    #################                                         #################+",
            "   ##################-        ###+                  ####      ################",
            "        #####+   +####-       ###+                  ####      #####   +####+",
            "        #####   ..+#          ###-                  ###+        ++..   +####",
            "       ### ##     -+                                            -+     ++",
            "           ###     +                                            -.    ##+",
            "             ###  .+                                            +.  -##-",
            "              ######-                  #      #                -#####+",
            "                    #                   #####.                 #+",
            "                     ##-                                    -+#",
            "                        #+..                            ..-#",
            "                        +########                  ######++####",
            "                     ######+++########         .#######++-+#####+",
            "                  ###    .#---++##+--++#------#+---+#++---+#    +##",
            "                 ###     .#----+##------#+--#+------#+-+---#      ##+",
            "                 #       .#-----##+-----#+--##-----+#+-----#       #+",
            "                #       #+-------#+--++#-...-+++---#+------+#.      -#",
            "              ##        #+-------#####         .####+-------#.        #-",
            "              ##        #+--------+#       #      #---------#-        #-",
            "             #.        #----------++              #----------+#        ++",
            "             #         #----------++              #----------+#        ++",
            "           ##        ##+----------++       .      #+---------+#          #",
            "           ##        ##----------#+        #       #+---------+#         #",
            "          +##       .##----------#-                #+----------#         #++",
            "          #. ....   #++----------#.                #+----------+#-  ....  #+",
            "          #.      .+#+----------##         .       ##+---------+##-       #+",
            "          #+        #+----------#                   -#---------+#-        ##",
            "           #####    #+--------++#                   -#--------++#-    ####",
            "            #-  #####-+-------##+                   -#---------+######.  #",
            "            #-     +#+++++----##-      .......     --#+----++-++##       #",
            "            #-      ###--+-+-+##+######+      +#######+-++-+--+##.       #",
            "            ###     ####+-++++#++#+++#. ##++#- #+++#++##+----+###      +++",
            "             +##.  .#++####++##++#+++#. -++++- #+++#++##++#####+#+.   ##+",
            "                    #+++++++++#########+      ###########+++-+++##",
            "                    ##++----------------######---------------++###",
            "                    +##++------------------------------------++#+",
            "                     ###++-------++---++++++++++++++--------+###",
            "                      ##++--------+++###########+++---------+##",
            "                       ##++---------++##     ##+++---------++##",
            "                        ##+---------+###     ####+-------+++#+",
            "                        ####+++++++++##        ##++------+++#+",
            "                        ##+++##########        .###+++#######+",
            "                      ######++++++++##          ####+++++######",
            "                     ##+..----+#-++###          ##++-++##--..-##+",
            "                     ##+-------++#####          ####++--------+#+",
            "                     ###############            ################",
        };

        private void Awake()
        {
            _controller = GetComponent<PlayerController>();
            _damageable = GetComponent<Damageable>();
            _renderer = FindFirstObjectByType<AsciiGridRenderer>();

            if (_palette == null)
            {
                var map = FindFirstObjectByType<World.AsciiWorldMap>();
                if (map != null && map.Palette != null)
                {
                    _palette = map.Palette;
                }
                else
                {
                    _palette = AsciiColorPalette.CreateDefault();
                }
            }

            ParseAsciiTemplate();
            SetupSmoothSpriteMesh();
        }

        private void ParseAsciiTemplate()
        {
            var list = new List<AsciiGlyphDef>(3000);
            for (short r = 0; r < ASCII_CHARACTER_TEMPLATE.Length; r++)
            {
                string rowStr = ASCII_CHARACTER_TEMPLATE[r];
                for (short c = 0; c < rowStr.Length; c++)
                {
                    char ch = rowStr[c];
                    if (ch == ' ' || ch == '\0') continue;

                    byte t = 0;
                    if (ch == '#') t = 0;
                    else if (ch == '+') t = 1;
                    else if (ch == '-') t = 2;
                    else if (ch == '.') t = 3;

                    list.Add(new AsciiGlyphDef
                    {
                        Col = c,
                        Row = r,
                        Glyph = ch,
                        Type = t
                    });
                }
            }

            _glyphDefs = list.ToArray();
            _totalQuads = _glyphDefs.Length + 28;
        }

        private void SetupSmoothSpriteMesh()
        {
            _spriteObj = new GameObject("Player_AsciiMesh");
            _spriteObj.transform.SetParent(transform, false);
            _spriteObj.transform.localPosition = new Vector3(_characterOffset.x, _characterOffset.y, -0.1f);

            _meshFilter = _spriteObj.AddComponent<MeshFilter>();
            _meshRenderer = _spriteObj.AddComponent<MeshRenderer>();
            _meshRenderer.sortingOrder = 50;

            if (_fontAsset == null)
            {
                var gridRenderer = FindFirstObjectByType<AsciiGridRenderer>();
                if (gridRenderer != null && gridRenderer.FontAsset != null)
                {
                    _fontAsset = gridRenderer.FontAsset;
                }
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

            BuildSpriteMesh();
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

        private Color32 GetGlyphColor(byte type)
        {
            switch (type)
            {
                case 0: return _colorHash;
                case 1: return _colorPlus;
                case 2: return _colorMinus;
                case 3: return _colorDot;
                default: return _colorHash;
            }
        }

        private void BuildSpriteMesh()
        {
            if (_totalQuads <= 0) return;

            int vCount = _totalQuads * 4;
            int tCount = _totalQuads * 6;

            _vertices = new Vector3[vCount];
            _uvs = new Vector2[vCount];
            _colors = new Color32[vCount];
            _triangles = new int[tCount];

            for (int i = 0; i < _totalQuads; i++)
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

            if (_glyphDefs != null && _isUvCacheInitialized)
            {
                for (int i = 0; i < _glyphDefs.Length; i++)
                {
                    int vBase = i * 4;
                    char glyph = _glyphDefs[i].Glyph;
                    int ascii = (int)glyph;
                    if (ascii < 0 || ascii >= 256) ascii = 63;
                    Rect uv = _asciiUvRects[ascii];

                    _uvs[vBase + 0] = new Vector2(uv.xMin, uv.yMax);
                    _uvs[vBase + 1] = new Vector2(uv.xMax, uv.yMax);
                    _uvs[vBase + 2] = new Vector2(uv.xMax, uv.yMin);
                    _uvs[vBase + 3] = new Vector2(uv.xMin, uv.yMin);

                    Color32 col = GetGlyphColor(_glyphDefs[i].Type);
                    _colors[vBase + 0] = col;
                    _colors[vBase + 1] = col;
                    _colors[vBase + 2] = col;
                    _colors[vBase + 3] = col;
                }
            }

            _mesh = new Mesh { name = "Player_ProceduralSpriteMesh" };
            _mesh.indexFormat = IndexFormat.UInt16;
            _mesh.MarkDynamic();
            _mesh.vertices = _vertices;
            _mesh.uv = _uvs;
            _mesh.colors32 = _colors;
            _mesh.triangles = _triangles;
            _mesh.RecalculateBounds();

            _meshFilter.sharedMesh = _mesh;
        }

        private void OnEnable()
        {
            AsciiGridRenderer.OnRenderDynamicEntities += OnRenderDynamic;
        }

        private void OnDisable()
        {
            AsciiGridRenderer.OnRenderDynamicEntities -= OnRenderDynamic;
        }

        private void Start()
        {
            _lastTrailPos = _controller.Position;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _animTime += dt;

            if (_fireTimer > 0f)
            {
                _fireTimer -= dt;
            }

            UpdateTrail();
        }

        private void LateUpdate()
        {
            if (_useSubPixelSmoothSprite && _spriteObj != null)
            {
                _spriteObj.SetActive(true);
                RenderSmoothMeshSprite();
            }
            else if (_spriteObj != null)
            {
                _spriteObj.SetActive(false);
            }
        }

        public void TriggerFire()
        {
            _fireTimer = FIRE_DURATION;
        }

        private void OnRenderDynamic(IAsciiRenderer renderer)
        {
            RenderTrail(renderer);

            if (!_useSubPixelSmoothSprite)
            {
                if (HasCustomClips())
                {
                    RenderCustomAnimationClip(renderer);
                }
                else
                {
                    RenderLegacyQuantizedSprite(renderer);
                }
            }
        }

        private bool HasCustomClips()
        {
            return _clipIdleDown != null || _clipIdleUp != null || _clipIdleSide != null ||
                   _clipRunDown != null || _clipRunUp != null || _clipRunSide != null;
        }

        private void UpdateFacingDirection(Vector2 aim)
        {
            float absX = Mathf.Abs(aim.x);
            float absY = Mathf.Abs(aim.y);
            bool isHorizontal = (_currentFacingDir == 1 || _currentFacingDir == 2);

            if (isHorizontal)
            {
                if (absY > absX + 0.22f)
                {
                    _currentFacingDir = (aim.y > 0f) ? 3 : 0;
                }
                else
                {
                    _currentFacingDir = (aim.x >= 0f) ? 1 : 2;
                }
            }
            else
            {
                if (absX > absY + 0.22f)
                {
                    _currentFacingDir = (aim.x >= 0f) ? 1 : 2;
                }
                else
                {
                    _currentFacingDir = (aim.y > 0f) ? 3 : 0;
                }
            }
        }

        private void RenderSmoothMeshSprite()
        {
            if (!_isUvCacheInitialized)
            {
                if (_fontAsset == null)
                {
                    var gridRenderer = FindFirstObjectByType<AsciiGridRenderer>();
                    if (gridRenderer != null && gridRenderer.FontAsset != null)
                    {
                        _fontAsset = gridRenderer.FontAsset;
                    }
                }
                if (_fontAsset != null && _fontAsset.material != null)
                {
                    _meshRenderer.sharedMaterial = _fontAsset.material;
                    InitializeUvCache();
                    BuildSpriteMesh();
                }
            }

            if (!_isUvCacheInitialized || _mesh == null || _glyphDefs == null) return;

            Vector2 aim = _controller.AimDirection.normalized;
            if (aim.sqrMagnitude < 0.001f) aim = Vector2.right;

            UpdateFacingDirection(aim);

            if (HasCustomClips())
            {
                float cellW = (_renderer != null && _renderer.CellWidth > 0f) ? _renderer.CellWidth : 0.25f;
                float cellH = (_renderer != null && _renderer.CellHeight > 0f) ? _renderer.CellHeight : 0.25f;
                RenderSmoothCustomAnimationClip(cellW, cellH, aim);
                return;
            }

            bool isFlashing = _damageable != null && _damageable.IsFlashing;
            bool colorsNeedUpdate = (isFlashing != _wasFlashing);
            _wasFlashing = isFlashing;

            float charH = _baseCharHeight * _characterScale;
            float charW = charH * _charAspectRatio;
            float quadH = charH * _glyphSizeFactor;
            float quadW = quadH * (_renderer != null ? _renderer.GlyphAspect : 0.86f);

            bool isMoving = _controller.IsMoving;
            float walkCycle = isMoving ? Mathf.Sin(Time.time * 12.0f) : 0f;
            float bodyBob = isMoving ? Mathf.Abs(Mathf.Sin(Time.time * 12.0f)) * (0.025f * _characterScale) : (Mathf.Sin(_animTime * 2.5f) * (0.012f * _characterScale));
            float legStride = isMoving ? Mathf.Sin(Time.time * 12.0f) * (0.045f * _characterScale) : 0f;

            Vector2 bodyRecoil = Vector2.zero;
            if (_fireTimer > 0f)
            {
                float recoilFactor = _fireTimer / FIRE_DURATION;
                bodyRecoil = -aim * (0.08f * _characterScale * recoilFactor);
            }

            _spriteObj.transform.localPosition = new Vector3(_characterOffset.x + bodyRecoil.x, _characterOffset.y + bodyRecoil.y + bodyBob, -0.1f);

            bool isFacingLeft = (aim.x < -0.1f) || (_currentFacingDir == 2);
            float centerCol = 41.5f;
            float centerRow = 35.0f;

            for (int i = 0; i < _glyphDefs.Length; i++)
            {
                int vBase = i * 4;
                var def = _glyphDefs[i];

                float colDist = def.Col - centerCol;
                if (isFacingLeft) colDist = -colDist;

                float localX = colDist * charW;
                float localY = (centerRow - def.Row) * charH;

                if (def.Row >= 58)
                {
                    float legOffset = (def.Col < 41.5f) ? (isFacingLeft ? -legStride : legStride) : (isFacingLeft ? legStride : -legStride);
                    localY += legOffset;
                }

                float x0 = localX - (quadW * 0.5f);
                float x1 = localX + (quadW * 0.5f);
                float y0 = localY - (quadH * 0.5f);
                float y1 = localY + (quadH * 0.5f);

                _vertices[vBase + 0] = new Vector3(x0, y1, 0f);
                _vertices[vBase + 1] = new Vector3(x1, y1, 0f);
                _vertices[vBase + 2] = new Vector3(x1, y0, 0f);
                _vertices[vBase + 3] = new Vector3(x0, y0, 0f);

                if (colorsNeedUpdate)
                {
                    Color32 col = isFlashing ? Color.white : GetGlyphColor(def.Type);
                    _colors[vBase + 0] = col;
                    _colors[vBase + 1] = col;
                    _colors[vBase + 2] = col;
                    _colors[vBase + 3] = col;
                }
            }

            int quadIdx = _glyphDefs.Length;

            Vector2 handPivot = new Vector2(isFacingLeft ? (-0.42f * _characterScale) : (0.42f * _characterScale), -0.20f * _characterScale);
            Vector2 handPos = handPivot + (aim * (0.22f * _characterScale));
            Vector2 gunRecoil = (_fireTimer > 0f) ? (-aim * (0.12f * _characterScale * (_fireTimer / FIRE_DURATION))) : Vector2.zero;
            Vector2 gunPos = handPos + gunRecoil;

            char barrelGlyph = '=';
            char muzzleGlyph = isFacingLeft ? '<' : '>';

            float absAimX = Mathf.Abs(aim.x);
            float absAimY = Mathf.Abs(aim.y);

            if (absAimY > absAimX + 0.35f)
            {
                barrelGlyph = '|';
                muzzleGlyph = (aim.y > 0f) ? '^' : 'v';
            }
            else if (absAimX > absAimY + 0.35f)
            {
                barrelGlyph = '=';
                muzzleGlyph = (aim.x > 0f) ? '>' : '<';
            }
            else
            {
                if ((aim.x >= 0f && aim.y >= 0f) || (aim.x < 0f && aim.y < 0f))
                {
                    barrelGlyph = '/';
                }
                else
                {
                    barrelGlyph = '\\';
                }
                muzzleGlyph = (aim.y > 0f) ? '^' : 'v';
            }

            Vector2 perp = new Vector2(-aim.y, aim.x);

            void SetCustomQuad(int index, Vector2 pos, char glyph, Color32 color, float scaleMul = 1.0f)
            {
                if (index >= _totalQuads) return;
                int vBase = index * 4;

                float qW = quadW * scaleMul;
                float qH = quadH * scaleMul;

                float x0 = pos.x - (qW * 0.5f);
                float x1 = pos.x + (qW * 0.5f);
                float y0 = pos.y - (qH * 0.5f);
                float y1 = pos.y + (qH * 0.5f);

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

                Color32 c = isFlashing ? Color.white : color;
                _colors[vBase + 0] = c;
                _colors[vBase + 1] = c;
                _colors[vBase + 2] = c;
                _colors[vBase + 3] = c;
            }

            if (_showGunInHands)
            {
                char gripGlyph = isFacingLeft ? ']' : '[';
                SetCustomQuad(quadIdx++, gunPos, gripGlyph, _colorHash, 1.05f);

                Vector2 receiverPos = gunPos + aim * (0.16f * _characterScale);
                SetCustomQuad(quadIdx++, receiverPos, '#', _colorGunBody, 1.1f);

                Vector2 sightPos = receiverPos + perp * (0.08f * _characterScale);
                SetCustomQuad(quadIdx++, sightPos, '.', _colorGunSight, 1.0f);

                Vector2 barrelPos = gunPos + aim * (0.34f * _characterScale);
                SetCustomQuad(quadIdx++, barrelPos, barrelGlyph, _colorGunBarrel, 1.1f);

                Vector2 muzzlePos = gunPos + aim * (_gunDistance * _characterScale);
                SetCustomQuad(quadIdx++, muzzlePos, muzzleGlyph, new Color32(190, 245, 255, 255), 1.15f);

                _gunMuzzleWorldPos = (Vector2)transform.position + (Vector2)_spriteObj.transform.localPosition + muzzlePos;

                if (_fireTimer > 0f)
                {
                    Vector2 flashCorePos = muzzlePos + aim * (0.12f * _characterScale);
                    SetCustomQuad(quadIdx++, flashCorePos, '*', _colorMuzzleFlash, 1.4f);

                    Vector2 flashSparkPos = muzzlePos + aim * (0.24f * _characterScale);
                    SetCustomQuad(quadIdx++, flashSparkPos, '+', new Color32(200, 255, 255, 255), 1.1f);

                    Vector2 sparkSidePos = muzzlePos + aim * (0.16f * _characterScale) + perp * (0.10f * _characterScale);
                    SetCustomQuad(quadIdx++, sparkSidePos, '.', new Color32(255, 220, 80, 255), 0.9f);
                }
            }
            else
            {
                _gunMuzzleWorldPos = (Vector2)transform.position;
            }

            for (int i = quadIdx; i < _totalQuads; i++)
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

        private void RenderSmoothCustomAnimationClip(float cellW, float cellH, Vector2 aim)
        {
            bool isMoving = _controller.IsMoving;
            bool isFacingLeft = aim.x < -0.1f;

            AsciiAnimationClip activeClip = null;

            if (_damageable != null && _damageable.IsDead && _clipDeath != null)
            {
                activeClip = _clipDeath;
            }
            else if (_fireTimer > 0f && _clipFire != null)
            {
                activeClip = _clipFire;
            }
            else if (isMoving)
            {
                if (Mathf.Abs(aim.x) >= Mathf.Abs(aim.y))
                {
                    activeClip = _clipRunSide ?? _clipRunDown;
                }
                else
                {
                    activeClip = aim.y > 0 ? (_clipRunUp ?? _clipRunDown) : _clipRunDown;
                }
            }
            else
            {
                if (Mathf.Abs(aim.x) >= Mathf.Abs(aim.y))
                {
                    activeClip = _clipIdleSide ?? _clipIdleDown;
                }
                else
                {
                    activeClip = aim.y > 0 ? (_clipIdleUp ?? _clipIdleDown) : _clipIdleDown;
                }
            }

            if (activeClip == null) return;

            var frame = activeClip.Evaluate(_animTime);
            if (frame == null) return;

            float quadH = cellH * 0.92f;
            float quadW = quadH * (_renderer != null ? _renderer.GlyphAspect : 0.86f);

            int halfW = frame.Width / 2;
            int halfH = frame.Height / 2;

            bool isFlashing = _damageable != null && _damageable.IsFlashing;
            Color32 flashCol = Color.white;

            int quadIdx = 0;

            for (int y = 0; y < frame.Height; y++)
            {
                for (int x = 0; x < frame.Width; x++)
                {
                    if (quadIdx >= _totalQuads) break;

                    int sampleX = isFacingLeft ? (frame.Width - 1 - x) : x;
                    char g = frame.GetGlyph(sampleX, y);
                    if (g == ' ' || g == '\0') continue;

                    Color32 col = frame.GetColor(sampleX, y);
                    if (col.a == 0) continue;

                    if (isFlashing) col = flashCol;

                    float dx = x - halfW;
                    float dy = halfH - y;

                    int vBase = quadIdx * 4;

                    float xCenter = dx * cellW;
                    float yCenter = dy * cellH;

                    float x0 = xCenter - (quadW * 0.5f);
                    float x1 = xCenter + (quadW * 0.5f);
                    float y0 = yCenter - (quadH * 0.5f);
                    float y1 = yCenter + (quadH * 0.5f);

                    _vertices[vBase + 0] = new Vector3(x0, y1, 0f);
                    _vertices[vBase + 1] = new Vector3(x1, y1, 0f);
                    _vertices[vBase + 2] = new Vector3(x1, y0, 0f);
                    _vertices[vBase + 3] = new Vector3(x0, y0, 0f);

                    int ascii = (int)g;
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

                    quadIdx++;
                }
            }

            for (int i = quadIdx; i < _totalQuads; i++)
            {
                int vBase = i * 4;
                _vertices[vBase + 0] = Vector3.zero;
                _vertices[vBase + 1] = Vector3.zero;
                _vertices[vBase + 2] = Vector3.zero;
                _vertices[vBase + 3] = Vector3.zero;

                _uvs[vBase + 0] = Vector2.zero;
                _uvs[vBase + 1] = Vector2.zero;
                _uvs[vBase + 2] = Vector2.zero;
                _uvs[vBase + 3] = Vector2.zero;

                _colors[vBase + 0] = Color.clear;
                _colors[vBase + 1] = Color.clear;
                _colors[vBase + 2] = Color.clear;
                _colors[vBase + 3] = Color.clear;
            }

            _mesh.SetVertices(_vertices);
            _mesh.SetUVs(0, _uvs);
            _mesh.SetColors(_colors);
        }

        private void RenderLegacyQuantizedSprite(IAsciiRenderer renderer)
        {
            Vector2 pos = _controller.Position;
            Vector2 aim = _controller.AimDirection.normalized;
            UpdateFacingDirection(aim);

            bool isFlashing = _damageable != null && _damageable.IsFlashing;
            Color32 primaryCol = isFlashing ? Color.white : _colorHash;
            Color32 armorCol = isFlashing ? Color.white : _colorPlus;

            float cellW = renderer.CellWidth > 0f ? renderer.CellWidth : 0.25f;
            float cellH = renderer.CellHeight > 0f ? renderer.CellHeight : 0.25f;

            void Put(float dx, float dy, char glyph, Color32 color, int layerOffset = 0)
            {
                renderer.DrawWorldGlyph(
                    pos + new Vector2(dx * cellW, dy * cellH),
                    glyph,
                    color,
                    Color.clear,
                    GameConstants.LAYER_PLAYER + layerOffset
                );
            }

            Put(-1f, 2f, '/', armorCol, 1);
            Put( 0f, 2f, '^', primaryCol, 2);
            Put( 1f, 2f, '\\', armorCol, 1);
            Put( 0f, 0f, '@', primaryCol, 3);

            if (_showGunInHands)
            {
                renderer.DrawWorldGlyph(pos + aim * cellW, '>', _colorGunBarrel, Color.clear, GameConstants.LAYER_PLAYER + 4);
                if (_fireTimer > 0f)
                {
                    renderer.DrawWorldGlyph(pos + aim * (cellW * 1.8f), '*', _colorMuzzleFlash, Color.clear, GameConstants.LAYER_VFX_PARTICLES);
                }
            }
        }

        private void RenderCustomAnimationClip(IAsciiRenderer renderer)
        {
            Vector2 aim = _controller.AimDirection.normalized;
            bool isMoving = _controller.IsMoving;
            bool isFacingLeft = aim.x < -0.1f;

            AsciiAnimationClip activeClip = null;

            if (_damageable != null && _damageable.IsDead && _clipDeath != null)
            {
                activeClip = _clipDeath;
            }
            else if (_fireTimer > 0f && _clipFire != null)
            {
                activeClip = _clipFire;
            }
            else if (isMoving)
            {
                if (Mathf.Abs(aim.x) >= Mathf.Abs(aim.y))
                {
                    activeClip = _clipRunSide ?? _clipRunDown;
                }
                else
                {
                    activeClip = aim.y > 0 ? (_clipRunUp ?? _clipRunDown) : _clipRunDown;
                }
            }
            else
            {
                if (Mathf.Abs(aim.x) >= Mathf.Abs(aim.y))
                {
                    activeClip = _clipIdleSide ?? _clipIdleDown;
                }
                else
                {
                    activeClip = aim.y > 0 ? (_clipIdleUp ?? _clipIdleDown) : _clipIdleDown;
                }
            }

            if (activeClip == null) return;

            var frame = activeClip.Evaluate(_animTime);
            if (frame == null) return;

            Vector2 centerPos = _controller.Position;
            float cellW = renderer.CellWidth > 0f ? renderer.CellWidth : 0.25f;
            float cellH = renderer.CellHeight > 0f ? renderer.CellHeight : 0.25f;

            int halfW = frame.Width / 2;
            int halfH = frame.Height / 2;

            bool isFlashing = _damageable != null && _damageable.IsFlashing;
            Color32 flashCol = Color.white;

            for (int y = 0; y < frame.Height; y++)
            {
                for (int x = 0; x < frame.Width; x++)
                {
                    int sampleX = isFacingLeft ? (frame.Width - 1 - x) : x;
                    char g = frame.GetGlyph(sampleX, y);
                    if (g == ' ' || g == '\0') continue;

                    Color32 col = frame.GetColor(sampleX, y);
                    if (col.a == 0) continue;

                    if (isFlashing) col = flashCol;

                    float offsetX = (x - halfW) * cellW;
                    float offsetY = (halfH - y) * cellH;

                    renderer.DrawWorldGlyph(
                        centerPos + new Vector2(offsetX, offsetY),
                        g,
                        col,
                        Color.clear,
                        GameConstants.LAYER_PLAYER
                    );
                }
            }
        }

        private void UpdateTrail()
        {
            if (!_enableTrail) return;

            float dt = Time.deltaTime;

            for (int i = _trailPoints.Count - 1; i >= 0; i--)
            {
                var pt = _trailPoints[i];
                pt.RemainingTime -= dt;
                if (pt.RemainingTime <= 0f)
                {
                    _trailPoints.RemoveAt(i);
                }
                else
                {
                    _trailPoints[i] = pt;
                }
            }

            Vector2 currentPos = _controller.Position;
            if (Vector2.Distance(currentPos, _lastTrailPos) >= _trailSpawnDistance && _controller.IsMoving)
            {
                char trailGlyph = (_trailPoints.Count % 2 == 0) ? '.' : '+';
                _trailPoints.Add(new TrailPoint
                {
                    Position = _lastTrailPos,
                    RemainingTime = _trailLifetime,
                    MaxTime = _trailLifetime,
                    Glyph = trailGlyph
                });

                if (_trailPoints.Count > _maxTrailPoints)
                {
                    _trailPoints.RemoveAt(0);
                }

                _lastTrailPos = currentPos;
            }
        }

        private void RenderTrail(IAsciiRenderer renderer)
        {
            Color32 trailBase = _palette.PlayerTrail;

            foreach (var pt in _trailPoints)
            {
                float alphaRatio = Mathf.Clamp01(pt.RemainingTime / pt.MaxTime);
                Color32 fadedColor = new Color32(trailBase.r, trailBase.g, trailBase.b, (byte)(trailBase.a * alphaRatio));
                renderer.DrawWorldGlyph(pt.Position, pt.Glyph, fadedColor, Color.clear, GameConstants.LAYER_PLAYER_TRAIL);
            }
        }
    }
}
