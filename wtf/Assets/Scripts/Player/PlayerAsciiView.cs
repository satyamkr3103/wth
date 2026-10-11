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
        [SerializeField] private float _baseCharHeight = 0.125f;
        [SerializeField] private float _charAspectRatio = 0.68f;
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

        private const int MAX_SPRITE_QUADS = 96;

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

        private struct TrailPoint
        {
            public Vector2 Position;
            public float RemainingTime;
            public float MaxTime;
            public char Glyph;
        }

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

            SetupSmoothSpriteMesh();
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

        private void BuildSpriteMesh()
        {
            int vCount = MAX_SPRITE_QUADS * 4;
            int tCount = MAX_SPRITE_QUADS * 6;

            _vertices = new Vector3[vCount];
            _uvs = new Vector2[vCount];
            _colors = new Color32[vCount];
            _triangles = new int[tCount];

            for (int i = 0; i < MAX_SPRITE_QUADS; i++)
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
            _lastTrailPos = _controller != null ? _controller.Position : (Vector2)transform.position;
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

            if (!_isUvCacheInitialized || _mesh == null) return;

            Vector2 aim = _controller != null ? _controller.AimDirection.normalized : Vector2.right;
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

            float baseH = (_baseCharHeight < 0.08f) ? 0.125f : _baseCharHeight;
            float aspect = (_charAspectRatio < 0.6f) ? 0.68f : _charAspectRatio;

            float stepY = baseH * _characterScale;
            float stepX = stepY * aspect;
            float quadH = stepY * _glyphSizeFactor;
            float quadW = quadH * (_renderer != null ? _renderer.GlyphAspect : 0.86f);

            bool isMoving = _controller != null && _controller.IsMoving;
            float walkPhase = Time.time * 13.0f;
            float legStride = isMoving ? Mathf.Sin(walkPhase) * (0.45f * stepY) : 0f;
            float legLift = isMoving ? Mathf.Max(0f, Mathf.Cos(walkPhase)) * (0.28f * stepY) : 0f;
            float bodyBob = isMoving ? Mathf.Abs(Mathf.Sin(walkPhase)) * (0.22f * stepY) : (Mathf.Sin(Time.time * 3.2f) * (0.09f * stepY));
            float breathe = Mathf.Sin(Time.time * 3.2f) * (0.08f * stepY);

            Vector2 torsoRecoil = Vector2.zero;
            Vector2 gunRecoil = Vector2.zero;
            if (_fireTimer > 0f)
            {
                float recoilRatio = _fireTimer / FIRE_DURATION;
                torsoRecoil = -aim * (0.05f * _characterScale * recoilRatio);
                gunRecoil = -aim * (0.16f * _characterScale * recoilRatio);
            }

            _spriteObj.transform.localPosition = new Vector3(
                _characterOffset.x + torsoRecoil.x,
                _characterOffset.y + torsoRecoil.y + bodyBob,
                -0.1f
            );

            int qIdx = 0;

            void AddQuad(Vector2 localPos, char glyph, Color32 color, float scale = 1.0f)
            {
                if (qIdx >= MAX_SPRITE_QUADS) return;
                int vBase = qIdx * 4;

                float qW = quadW * scale;
                float qH = quadH * scale;

                float x0 = localPos.x - (qW * 0.5f);
                float x1 = localPos.x + (qW * 0.5f);
                float y0 = localPos.y - (qH * 0.5f);
                float y1 = localPos.y + (qH * 0.5f);

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

                qIdx++;
            }

            int corePhase = (int)(Time.time * 6.0f) % 4;
            char coreGlyph = '@';
            Color32 coreColor = new Color32(0, 240, 255, 255);
            if (corePhase == 1)
            {
                coreGlyph = '*';
                coreColor = new Color32(180, 255, 255, 255);
            }
            else if (corePhase == 2)
            {
                coreGlyph = 'O';
                coreColor = new Color32(40, 200, 255, 255);
            }
            else if (corePhase == 3)
            {
                coreGlyph = '%';
                coreColor = new Color32(100, 255, 220, 255);
            }

            int thrusterPhase = (int)(Time.time * 8.0f) % 3;
            Color32 thrusterColor = (thrusterPhase == 0) ? new Color32(255, 140, 30, 255) : (thrusterPhase == 1 ? new Color32(255, 210, 60, 255) : new Color32(255, 80, 20, 255));

            int visorPhase = (int)(Time.time * 4.0f) % 2;
            char visorCenterGlyph = (visorPhase == 0) ? '*' : 'o';
            Color32 visorCenterColor = (visorPhase == 0) ? new Color32(255, 255, 255, 255) : new Color32(80, 240, 255, 255);

            Color32 colArmor = _colorHash;
            Color32 colArmorLight = _colorPlus;
            Color32 colArmorDark = _colorMinus;
            Color32 colConduit = new Color32(60, 180, 220, 255);
            Color32 colBelt = new Color32(140, 160, 180, 255);
            Color32 colVisor = new Color32(0, 255, 255, 255);

            if (_currentFacingDir == 0)
            {
                AddQuad(new Vector2(-1f * stepX, 4f * stepY + breathe), '/', colArmorLight);
                AddQuad(new Vector2( 0f * stepX, 4f * stepY + breathe), '^', Color.white);
                AddQuad(new Vector2( 1f * stepX, 4f * stepY + breathe), '\\', colArmorLight);

                AddQuad(new Vector2(-2f * stepX, 3f * stepY + breathe), '[', colArmorDark);
                AddQuad(new Vector2(-1f * stepX, 3f * stepY + breathe), '=', colVisor);
                AddQuad(new Vector2( 0f * stepX, 3f * stepY + breathe), visorCenterGlyph, visorCenterColor, 1.15f);
                AddQuad(new Vector2( 1f * stepX, 3f * stepY + breathe), '=', colVisor);
                AddQuad(new Vector2( 2f * stepX, 3f * stepY + breathe), ']', colArmorDark);

                AddQuad(new Vector2(-2f * stepX, 2f * stepY + breathe), '(', colArmor);
                AddQuad(new Vector2(-1f * stepX, 2f * stepY + breathe), '#', colArmorDark);
                AddQuad(new Vector2( 0f * stepX, 2f * stepY + breathe), 'v', colArmorLight);
                AddQuad(new Vector2( 1f * stepX, 2f * stepY + breathe), '#', colArmorDark);
                AddQuad(new Vector2( 2f * stepX, 2f * stepY + breathe), ')', colArmor);

                AddQuad(new Vector2(-3f * stepX, 1f * stepY + (breathe * 0.6f)), '{', colArmorLight);
                AddQuad(new Vector2(-2f * stepX, 1f * stepY + (breathe * 0.6f)), '#', colArmor);
                AddQuad(new Vector2(-1f * stepX, 1f * stepY + (breathe * 0.6f)), '|', colConduit);
                AddQuad(new Vector2( 0f * stepX, 1f * stepY + (breathe * 0.6f)), '=', colArmorDark);
                AddQuad(new Vector2( 1f * stepX, 1f * stepY + (breathe * 0.6f)), '|', colConduit);
                AddQuad(new Vector2( 2f * stepX, 1f * stepY + (breathe * 0.6f)), '#', colArmor);
                AddQuad(new Vector2( 3f * stepX, 1f * stepY + (breathe * 0.6f)), '}', colArmorLight);

                AddQuad(new Vector2(-3f * stepX, 0f * stepY), '|', colArmorDark);
                AddQuad(new Vector2(-2f * stepX, 0f * stepY), '[', colArmor);
                AddQuad(new Vector2(-1f * stepX, 0f * stepY), ':', colConduit);
                AddQuad(new Vector2( 0f * stepX, 0f * stepY), coreGlyph, coreColor, 1.25f);
                AddQuad(new Vector2( 1f * stepX, 0f * stepY), ':', colConduit);
                AddQuad(new Vector2( 2f * stepX, 0f * stepY), ']', colArmor);
                AddQuad(new Vector2( 3f * stepX, 0f * stepY), '|', colArmorDark);

                AddQuad(new Vector2(-2f * stepX, -1f * stepY), '/', colArmorLight);
                AddQuad(new Vector2(-1f * stepX, -1f * stepY), '#', colArmor);
                AddQuad(new Vector2( 0f * stepX, -1f * stepY), '|', colConduit);
                AddQuad(new Vector2( 1f * stepX, -1f * stepY), '#', colArmor);
                AddQuad(new Vector2( 2f * stepX, -1f * stepY), '\\', colArmorLight);

                AddQuad(new Vector2(-3f * stepX, -2f * stepY), '[', colBelt);
                AddQuad(new Vector2(-2f * stepX, -2f * stepY), '=', colBelt);
                AddQuad(new Vector2(-1f * stepX, -2f * stepY), '#', colArmor);
                AddQuad(new Vector2( 0f * stepX, -2f * stepY), '=', colArmorLight);
                AddQuad(new Vector2( 1f * stepX, -2f * stepY), '#', colArmor);
                AddQuad(new Vector2( 2f * stepX, -2f * stepY), '=', colBelt);
                AddQuad(new Vector2( 3f * stepX, -2f * stepY), ']', colBelt);

                float legL_Y = legStride;
                float legR_Y = -legStride;

                AddQuad(new Vector2(-2f * stepX, -3f * stepY + legL_Y), '|', colArmorDark);
                AddQuad(new Vector2(-1f * stepX, -3f * stepY + legL_Y), '#', colArmor);
                AddQuad(new Vector2( 1f * stepX, -3f * stepY + legR_Y), '#', colArmor);
                AddQuad(new Vector2( 2f * stepX, -3f * stepY + legR_Y), '|', colArmorDark);

                AddQuad(new Vector2(-2f * stepX, -4f * stepY + legL_Y), '[', colArmorDark);
                AddQuad(new Vector2(-1f * stepX, -4f * stepY + legL_Y), 'o', colArmorLight);
                AddQuad(new Vector2( 1f * stepX, -4f * stepY + legR_Y), 'o', colArmorLight);
                AddQuad(new Vector2( 2f * stepX, -4f * stepY + legR_Y), ']', colArmorDark);

                AddQuad(new Vector2(-2f * stepX, -5f * stepY + legL_Y), '[', colArmor);
                AddQuad(new Vector2(-1f * stepX, -5f * stepY + legL_Y), '=', colArmorLight);
                AddQuad(new Vector2( 1f * stepX, -5f * stepY + legR_Y), '=', colArmorLight);
                AddQuad(new Vector2( 2f * stepX, -5f * stepY + legR_Y), ']', colArmor);
            }
            else if (_currentFacingDir == 3)
            {
                AddQuad(new Vector2(-1f * stepX, 4f * stepY + breathe), '/', colArmorLight);
                AddQuad(new Vector2( 0f * stepX, 4f * stepY + breathe), '|', Color.white);
                AddQuad(new Vector2( 1f * stepX, 4f * stepY + breathe), '\\', colArmorLight);

                AddQuad(new Vector2(-2f * stepX, 3f * stepY + breathe), '(', colArmorDark);
                AddQuad(new Vector2(-1f * stepX, 3f * stepY + breathe), '#', colArmor);
                AddQuad(new Vector2( 0f * stepX, 3f * stepY + breathe), '=', colArmorDark);
                AddQuad(new Vector2( 1f * stepX, 3f * stepY + breathe), '#', colArmor);
                AddQuad(new Vector2( 2f * stepX, 3f * stepY + breathe), ')', colArmorDark);

                AddQuad(new Vector2(-2f * stepX, 2f * stepY + breathe), '[', colArmorDark);
                AddQuad(new Vector2(-1f * stepX, 2f * stepY + breathe), ':', colConduit);
                AddQuad(new Vector2( 0f * stepX, 2f * stepY + breathe), '|', colConduit);
                AddQuad(new Vector2( 1f * stepX, 2f * stepY + breathe), ':', colConduit);
                AddQuad(new Vector2( 2f * stepX, 2f * stepY + breathe), ']', colArmorDark);

                AddQuad(new Vector2(-3f * stepX, 1f * stepY + (breathe * 0.6f)), '{', colArmorLight);
                AddQuad(new Vector2(-2f * stepX, 1f * stepY + (breathe * 0.6f)), 'O', thrusterColor, 1.2f);
                AddQuad(new Vector2(-1f * stepX, 1f * stepY + (breathe * 0.6f)), '#', colArmor);
                AddQuad(new Vector2( 0f * stepX, 1f * stepY + (breathe * 0.6f)), '=', colArmorDark);
                AddQuad(new Vector2( 1f * stepX, 1f * stepY + (breathe * 0.6f)), '#', colArmor);
                AddQuad(new Vector2( 2f * stepX, 1f * stepY + (breathe * 0.6f)), 'O', thrusterColor, 1.2f);
                AddQuad(new Vector2( 3f * stepX, 1f * stepY + (breathe * 0.6f)), '}', colArmorLight);

                AddQuad(new Vector2(-3f * stepX, 0f * stepY), '|', colArmorDark);
                AddQuad(new Vector2(-2f * stepX, 0f * stepY), '[', colArmorLight);
                AddQuad(new Vector2(-1f * stepX, 0f * stepY), '=', colConduit);
                AddQuad(new Vector2( 0f * stepX, 0f * stepY), '#', colArmor);
                AddQuad(new Vector2( 1f * stepX, 0f * stepY), '=', colConduit);
                AddQuad(new Vector2( 2f * stepX, 0f * stepY), ']', colArmorLight);
                AddQuad(new Vector2( 3f * stepX, 0f * stepY), '|', colArmorDark);

                AddQuad(new Vector2(-2f * stepX, -1f * stepY), '/', colArmorLight);
                AddQuad(new Vector2(-1f * stepX, -1f * stepY), '|', colConduit);
                AddQuad(new Vector2( 0f * stepX, -1f * stepY), ':', colConduit);
                AddQuad(new Vector2( 1f * stepX, -1f * stepY), '|', colConduit);
                AddQuad(new Vector2( 2f * stepX, -1f * stepY), '\\', colArmorLight);

                AddQuad(new Vector2(-3f * stepX, -2f * stepY), '[', colBelt);
                AddQuad(new Vector2(-2f * stepX, -2f * stepY), '=', colBelt);
                AddQuad(new Vector2(-1f * stepX, -2f * stepY), '#', colArmor);
                AddQuad(new Vector2( 0f * stepX, -2f * stepY), '=', colArmorLight);
                AddQuad(new Vector2( 1f * stepX, -2f * stepY), '#', colArmor);
                AddQuad(new Vector2( 2f * stepX, -2f * stepY), '=', colBelt);
                AddQuad(new Vector2( 3f * stepX, -2f * stepY), ']', colBelt);

                float legL_Y = -legStride;
                float legR_Y = legStride;

                AddQuad(new Vector2(-2f * stepX, -3f * stepY + legL_Y), '|', colArmorDark);
                AddQuad(new Vector2(-1f * stepX, -3f * stepY + legL_Y), '#', colArmor);
                AddQuad(new Vector2( 1f * stepX, -3f * stepY + legR_Y), '#', colArmor);
                AddQuad(new Vector2( 2f * stepX, -3f * stepY + legR_Y), '|', colArmorDark);

                AddQuad(new Vector2(-2f * stepX, -4f * stepY + legL_Y), '[', colArmorDark);
                AddQuad(new Vector2(-1f * stepX, -4f * stepY + legL_Y), 'o', colArmorLight);
                AddQuad(new Vector2( 1f * stepX, -4f * stepY + legR_Y), 'o', colArmorLight);
                AddQuad(new Vector2( 2f * stepX, -4f * stepY + legR_Y), ']', colArmorDark);

                AddQuad(new Vector2(-2f * stepX, -5f * stepY + legL_Y), '[', colArmor);
                AddQuad(new Vector2(-1f * stepX, -5f * stepY + legL_Y), '=', colArmorLight);
                AddQuad(new Vector2( 1f * stepX, -5f * stepY + legR_Y), '=', colArmorLight);
                AddQuad(new Vector2( 2f * stepX, -5f * stepY + legR_Y), ']', colArmor);
            }
            else
            {
                bool flipX = (_currentFacingDir == 2);

                void AddSideQuad(float col, float row, char glyph, Color32 color, float scale = 1.0f)
                {
                    float posX = col * stepX;
                    char g = glyph;
                    if (flipX)
                    {
                        posX = -posX;
                        if (g == '>') g = '<';
                        else if (g == '<') g = '>';
                        else if (g == '[') g = ']';
                        else if (g == ']') g = '[';
                        else if (g == '{') g = '}';
                        else if (g == '}') g = '{';
                        else if (g == '(') g = ')';
                        else if (g == ')') g = '(';
                        else if (g == '/') g = '\\';
                        else if (g == '\\') g = '/';
                    }
                    AddQuad(new Vector2(posX, row * stepY), g, color, scale);
                }

                AddSideQuad(-1f, 4f + breathe, '^', Color.white);
                AddSideQuad( 0f, 4f + breathe, '=', colArmorLight);
                AddSideQuad( 1f, 4f + breathe, '\\', colArmorLight);

                AddSideQuad(-2f, 3f + breathe, '(', colArmorDark);
                AddSideQuad(-1f, 3f + breathe, '#', colArmor);
                AddSideQuad( 0f, 3f + breathe, '[', colArmorDark);
                AddSideQuad( 1f, 3f + breathe, '>', colVisor, 1.15f);
                AddSideQuad( 2f, 3f + breathe, '=', colVisor);

                AddSideQuad(-2f, 2f + breathe, '|', colArmorDark);
                AddSideQuad(-1f, 2f + breathe, '#', colArmor);
                AddSideQuad( 0f, 2f + breathe, '/', colArmorLight);
                AddSideQuad( 1f, 2f + breathe, '=', colArmorLight);

                AddSideQuad(-3f, 1f + (breathe * 0.6f), '{', colArmorLight);
                AddSideQuad(-2f, 1f + (breathe * 0.6f), 'O', thrusterColor, 1.2f);
                AddSideQuad(-1f, 1f + (breathe * 0.6f), '#', colArmor);
                AddSideQuad( 0f, 1f + (breathe * 0.6f), '{', colArmorLight);
                AddSideQuad( 1f, 1f + (breathe * 0.6f), '#', colArmor);
                AddSideQuad( 2f, 1f + (breathe * 0.6f), ']', colArmorDark);

                AddSideQuad(-3f, 0f, '[', colArmorLight);
                AddSideQuad(-2f, 0f, '#', colArmor);
                AddSideQuad(-1f, 0f, '|', colArmorDark);
                AddSideQuad( 0f, 0f, coreGlyph, coreColor, 1.2f);
                AddSideQuad( 1f, 0f, '#', colArmor);
                AddSideQuad( 2f, 0f, ']', colArmorDark);

                AddSideQuad(-2f, -1f, '|', colArmorDark);
                AddSideQuad(-1f, -1f, '#', colArmor);
                AddSideQuad( 0f, -1f, ':', colConduit);
                AddSideQuad( 1f, -1f, '#', colArmor);
                AddSideQuad( 2f, -1f, '/', colArmorLight);

                AddSideQuad(-2f, -2f, '[', colBelt);
                AddSideQuad(-1f, -2f, '#', colArmor);
                AddSideQuad( 0f, -2f, '=', colBelt);
                AddSideQuad( 1f, -2f, '#', colArmor);
                AddSideQuad( 2f, -2f, ']', colBelt);

                float legFrontX = (flipX ? -legStride : legStride) * 1.5f;
                float legBackX = -legFrontX;
                float legFrontY = legLift;
                float legBackY = 0f;

                AddSideQuad(-1f + (legBackX / stepX), -3f + (legBackY / stepY), '\\', colArmorDark);
                AddSideQuad( 0f + (legBackX / stepX), -3f + (legBackY / stepY), '#', colArmor);
                AddSideQuad( 1f + (legFrontX / stepX), -3f + (legFrontY / stepY), '#', colArmor);
                AddSideQuad( 2f + (legFrontX / stepX), -3f + (legFrontY / stepY), '/', colArmorDark);

                AddSideQuad(-1f + (legBackX / stepX), -4f + (legBackY / stepY), 'o', colArmorLight);
                AddSideQuad( 0f + (legBackX / stepX), -4f + (legBackY / stepY), ']', colArmorDark);
                AddSideQuad( 1f + (legFrontX / stepX), -4f + (legFrontY / stepY), '[', colArmorDark);
                AddSideQuad( 2f + (legFrontX / stepX), -4f + (legFrontY / stepY), 'o', colArmorLight);

                AddSideQuad(-2f + (legBackX / stepX), -5f + (legBackY / stepY), '[', colArmor);
                AddSideQuad(-1f + (legBackX / stepX), -5f + (legBackY / stepY), '=', colArmorLight);
                AddSideQuad( 0f + (legBackX / stepX), -5f + (legBackY / stepY), ']', colArmor);
                AddSideQuad( 1f + (legFrontX / stepX), -5f + (legFrontY / stepY), '[', colArmor);
                AddSideQuad( 2f + (legFrontX / stepX), -5f + (legFrontY / stepY), '=', colArmorLight);
                AddSideQuad( 3f + (legFrontX / stepX), -5f + (legFrontY / stepY), ']', colArmor);
            }

            float shoulderX;
            if (_currentFacingDir == 2)
            {
                shoulderX = -0.20f * _characterScale;
            }
            else if (_currentFacingDir == 1)
            {
                shoulderX = 0.20f * _characterScale;
            }
            else
            {
                shoulderX = (aim.x >= 0f) ? (0.22f * _characterScale) : (-0.22f * _characterScale);
            }

            float shoulderY = (1.25f * stepY) + (breathe * 0.6f);
            Vector2 shoulderPivot = new Vector2(shoulderX, shoulderY);

            Vector2 handPos = shoulderPivot + (aim * (0.20f * _characterScale));
            Vector2 gunPos = handPos + gunRecoil;

            char barrelGlyph = '=';
            char muzzleGlyph = (_currentFacingDir == 2) ? '<' : '>';

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

            if (_showGunInHands)
            {
                Vector2 armPos = Vector2.Lerp(shoulderPivot, gunPos, 0.45f);
                char armGlyph = (_currentFacingDir == 2 || _currentFacingDir == 1) ? '=' : '-';
                AddQuad(armPos, armGlyph, colArmorLight, 1.0f);

                char gripGlyph = (_currentFacingDir == 2) ? ']' : '[';
                AddQuad(gunPos, gripGlyph, _colorHash, 1.05f);

                Vector2 receiverPos = gunPos + aim * (0.14f * _characterScale);
                AddQuad(receiverPos, '#', _colorGunBody, 1.1f);

                Vector2 magPos = receiverPos - perp * (0.07f * _characterScale);
                AddQuad(magPos, '|', _colorHash, 0.95f);

                Vector2 sightPos = receiverPos + perp * (0.07f * _characterScale);
                AddQuad(sightPos, '.', _colorGunSight, 1.0f);

                Vector2 barrelPos1 = gunPos + aim * (0.26f * _characterScale);
                AddQuad(barrelPos1, barrelGlyph, _colorGunBarrel, 1.05f);

                Vector2 barrelPos2 = gunPos + aim * (0.38f * _characterScale);
                AddQuad(barrelPos2, barrelGlyph, _colorGunBarrel, 1.1f);

                Vector2 muzzlePos = gunPos + aim * (_gunDistance * _characterScale);
                AddQuad(muzzlePos, muzzleGlyph, new Color32(190, 245, 255, 255), 1.15f);

                _gunMuzzleWorldPos = (Vector2)transform.position + (Vector2)_spriteObj.transform.localPosition + muzzlePos;

                if (_fireTimer > 0f)
                {
                    Vector2 flashCorePos = muzzlePos + aim * (0.12f * _characterScale);
                    AddQuad(flashCorePos, '*', _colorMuzzleFlash, 1.4f);

                    Vector2 flashSparkPos = muzzlePos + aim * (0.24f * _characterScale);
                    AddQuad(flashSparkPos, '+', new Color32(200, 255, 255, 255), 1.15f);

                    Vector2 sparkSidePos1 = muzzlePos + aim * (0.16f * _characterScale) + perp * (0.10f * _characterScale);
                    AddQuad(sparkSidePos1, '.', new Color32(255, 220, 80, 255), 0.95f);

                    Vector2 sparkSidePos2 = muzzlePos + aim * (0.16f * _characterScale) - perp * (0.10f * _characterScale);
                    AddQuad(sparkSidePos2, '.', new Color32(255, 220, 80, 255), 0.95f);
                }
            }
            else
            {
                _gunMuzzleWorldPos = (Vector2)transform.position;
            }

            for (int i = qIdx; i < MAX_SPRITE_QUADS; i++)
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
            bool isMoving = _controller != null && _controller.IsMoving;
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
                    if (quadIdx >= MAX_SPRITE_QUADS) break;

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

            for (int i = quadIdx; i < MAX_SPRITE_QUADS; i++)
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
            Vector2 pos = _controller != null ? _controller.Position : (Vector2)transform.position;
            Vector2 aim = _controller != null ? _controller.AimDirection.normalized : Vector2.right;
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
            Vector2 aim = _controller != null ? _controller.AimDirection.normalized : Vector2.right;
            bool isMoving = _controller != null && _controller.IsMoving;
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

            Vector2 centerPos = _controller != null ? _controller.Position : (Vector2)transform.position;
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
                        GameConstants.LAYER_PLAYER + 1
                    );
                }
            }
        }

        private void UpdateTrail()
        {
            if (!_enableTrail)
            {
                _trailPoints.Clear();
                return;
            }

            float dt = Time.deltaTime;
            for (int i = _trailPoints.Count - 1; i >= 0; i--)
            {
                var tp = _trailPoints[i];
                tp.RemainingTime -= dt;
                if (tp.RemainingTime <= 0f)
                {
                    _trailPoints.RemoveAt(i);
                }
                else
                {
                    _trailPoints[i] = tp;
                }
            }

            Vector2 currentPos = _controller != null ? _controller.Position : (Vector2)transform.position;
            if (Vector2.Distance(currentPos, _lastTrailPos) >= _trailSpawnDistance)
            {
                if (_trailPoints.Count >= _maxTrailPoints)
                {
                    _trailPoints.RemoveAt(0);
                }

                _trailPoints.Add(new TrailPoint
                {
                    Position = currentPos,
                    RemainingTime = _trailLifetime,
                    MaxTime = _trailLifetime,
                    Glyph = '.'
                });

                _lastTrailPos = currentPos;
            }
        }

        private void RenderTrail(IAsciiRenderer renderer)
        {
            if (!_enableTrail || _trailPoints.Count == 0) return;

            Color32 baseColor = _palette != null ? _palette.PlayerTrail : new Color32(0, 150, 200, 100);

            for (int i = 0; i < _trailPoints.Count; i++)
            {
                var tp = _trailPoints[i];
                float t = tp.RemainingTime / tp.MaxTime;
                byte alpha = (byte)(baseColor.a * t);
                Color32 c = new Color32(baseColor.r, baseColor.g, baseColor.b, alpha);

                renderer.DrawWorldGlyph(
                    tp.Position,
                    tp.Glyph,
                    c,
                    Color.clear,
                    GameConstants.LAYER_PLAYER - 1
                );
            }
        }
    }
}
