#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using TMPro;
using AsciiActionGame.Core;
using AsciiActionGame.Rendering;
using AsciiActionGame.World;
using AsciiActionGame.Player;
using AsciiActionGame.CameraControl;
using AsciiActionGame.Combat;
using AsciiActionGame.Enemies;
using AsciiActionGame.Weapons;
using AsciiActionGame.Projectiles;
using AsciiActionGame.AI;

namespace AsciiActionGame.EditorTools
{
    public static class AsciiSceneBuilder
    {
        [MenuItem("ASCII Game/Setup Phase 1 Scene (Foundation)", false, 10)]
        public static void SetupPhase1Scene()
        {
            SetupBaseWorld(out var player, out var palette);
            Debug.Log("[AsciiSceneBuilder] Phase 1 Foundation setup completed!");
        }

        [MenuItem("ASCII Game/Setup Phase 2 Scene (Combat & Modular Enemies)", false, 11)]
        public static void SetupPhase2Scene()
        {
            SetupPhase2Core(out _);
            Debug.Log("[AsciiSceneBuilder] Phase 2 Combat & Modular Enemies setup completed! Press Play to battle.");
        }

        [MenuItem("ASCII Game/Setup Phase 3 Scene (Self-Fragmentation & Projectiles)", false, 12)]
        public static void SetupPhase3Scene()
        {
            SetupPhase2Core(out var player);

            FragmentPool pool = Object.FindFirstObjectByType<FragmentPool>();
            if (pool == null)
            {
                GameObject poolObj = new GameObject("FragmentPool");
                pool = poolObj.AddComponent<FragmentPool>();
                Undo.RegisterCreatedObjectUndo(poolObj, "Create FragmentPool");
            }

            DisintegrationEffect disVfx = Object.FindFirstObjectByType<DisintegrationEffect>();
            if (disVfx == null)
            {
                GameObject disObj = new GameObject("DisintegrationEffect");
                disVfx = disObj.AddComponent<DisintegrationEffect>();
                Undo.RegisterCreatedObjectUndo(disObj, "Create DisintegrationEffect");
            }

            var enemies = Object.FindObjectsByType<EnemyEntity>(FindObjectsSortMode.None);
            foreach (var enemy in enemies)
            {
                if (enemy.GetComponent<LimbSacrificeAction>() == null)
                {
                    enemy.gameObject.AddComponent<LimbSacrificeAction>();
                }
            }

            Debug.Log("[AsciiSceneBuilder] Phase 3 Self-Fragmentation & Projectile System setup completed! Press Play to test.");
        }

        [MenuItem("ASCII Game/Setup Phase 4 Scene (Advanced ASCII Visuals)", false, 13)]
        public static void SetupPhase4Scene()
        {
            SetupPhase2Core(out var player);

            AnimatedTerrainTile animTiles = Object.FindFirstObjectByType<AnimatedTerrainTile>();
            if (animTiles == null)
            {
                var worldMap = Object.FindFirstObjectByType<AsciiWorldMap>();
                GameObject animObj = worldMap != null ? worldMap.gameObject : new GameObject("AnimatedTerrainTile");
                animTiles = animObj.AddComponent<AnimatedTerrainTile>();
                Undo.RegisterCreatedObjectUndo(animObj, "Add AnimatedTerrainTile");
            }

            AsciiPostProcessing postProcess = Object.FindFirstObjectByType<AsciiPostProcessing>();
            if (postProcess == null)
            {
                var renderer = Object.FindFirstObjectByType<AsciiGridRenderer>();
                GameObject postObj = renderer != null ? renderer.gameObject : new GameObject("AsciiPostProcessing");
                postProcess = postObj.AddComponent<AsciiPostProcessing>();
                Undo.RegisterCreatedObjectUndo(postObj, "Add AsciiPostProcessing");
            }

            var map = Object.FindFirstObjectByType<AsciiWorldMap>();
            if (map != null)
            {
                map.InitializeMap();
            }

            Debug.Log("[AsciiSceneBuilder] Phase 4 Advanced ASCII Visuals setup completed! Press Play to view rich visuals.");
        }

        [MenuItem("ASCII Game/Setup Phase 5 Scene (Gemma AI & ML-Agents)", false, 14)]
        public static void SetupPhase5Scene()
        {
            SetupPhase4Scene();

            var enemies = Object.FindObjectsByType<EnemyEntity>(FindObjectsSortMode.None);
            foreach (var enemy in enemies)
            {
                if (enemy.GetComponent<EnemyDecisionDirector>() == null)
                {
                    enemy.gameObject.AddComponent<EnemyDecisionDirector>();
                }
            }

            Debug.Log("[AsciiSceneBuilder] Phase 5 Gemma AI + ML-Agents setup completed! Press Play to battle autonomous cyber-agents.");
        }

        private static void SetupPhase2Core(out PlayerController player)
        {
            SetupBaseWorld(out player, out var palette);

            if (Object.FindFirstObjectByType<AnimatedTerrainTile>() == null)
            {
                var worldMap = Object.FindFirstObjectByType<AsciiWorldMap>();
                GameObject animObj = worldMap != null ? worldMap.gameObject : new GameObject("AnimatedTerrainTile");
                animObj.AddComponent<AnimatedTerrainTile>();
            }

            if (Object.FindFirstObjectByType<AsciiPostProcessing>() == null)
            {
                var renderer = Object.FindFirstObjectByType<AsciiGridRenderer>();
                GameObject postObj = renderer != null ? renderer.gameObject : new GameObject("AsciiPostProcessing");
                postObj.AddComponent<AsciiPostProcessing>();
            }

            EraserBeamVfx vfx = Object.FindFirstObjectByType<EraserBeamVfx>();
            if (vfx == null)
            {
                GameObject vfxObj = new GameObject("EraserBeamVfx");
                vfx = vfxObj.AddComponent<EraserBeamVfx>();
            }

            if (Object.FindFirstObjectByType<FragmentPool>() == null)
            {
                GameObject poolObj = new GameObject("FragmentPool");
                poolObj.AddComponent<FragmentPool>();
            }

            if (Object.FindFirstObjectByType<DisintegrationEffect>() == null)
            {
                GameObject disObj = new GameObject("DisintegrationEffect");
                disObj.AddComponent<DisintegrationEffect>();
            }

            if (player != null)
            {
                var playerDmg = player.GetComponent<Damageable>();
                if (playerDmg == null)
                {
                    playerDmg = player.gameObject.AddComponent<Damageable>();
                    playerDmg.Initialize(100f, 5f);
                }

                var playerCombat = player.GetComponent<PlayerCombat>();
                if (playerCombat == null)
                {
                    playerCombat = player.gameObject.AddComponent<PlayerCombat>();
                }

                var eraserWeapon = player.GetComponentInChildren<EraserBeamWeapon>();
                if (eraserWeapon == null)
                {
                    GameObject weaponObj = new GameObject("EraserBeamWeapon");
                    weaponObj.transform.SetParent(player.transform, false);
                    eraserWeapon = weaponObj.AddComponent<EraserBeamWeapon>();
                }

                playerCombat.EquipWeapon(eraserWeapon);
            }

            SpawnModularEnemyIfNotExists("CyberDrone_Alpha", new Vector3(36f, 20f, 0f));
            SpawnModularEnemyIfNotExists("CyberDrone_Beta", new Vector3(25f, 25f, 0f));
            SpawnModularEnemyIfNotExists("CyberDrone_Gamma", new Vector3(35f, 27f, 0f));
        }

        private static void SetupBaseWorld(out PlayerController player, out AsciiColorPalette palette)
        {
            palette = AssetDatabase.LoadAssetAtPath<AsciiColorPalette>("Assets/Settings/AsciiColorPalette.asset");
            if (palette == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Settings"))
                {
                    AssetDatabase.CreateFolder("Assets", "Settings");
                }
                palette = ScriptableObject.CreateInstance<AsciiColorPalette>();
                AssetDatabase.CreateAsset(palette, "Assets/Settings/AsciiColorPalette.asset");
                AssetDatabase.SaveAssets();
                Debug.Log("[AsciiSceneBuilder] Created default AsciiColorPalette asset at Assets/Settings/AsciiColorPalette.asset");
            }

            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                GameObject camObj = new GameObject("Main Camera");
                mainCam = camObj.AddComponent<Camera>();
                camObj.tag = "MainCamera";
            }
            mainCam.orthographic = true;
            mainCam.orthographicSize = 12f;
            mainCam.backgroundColor = palette.CanvasBackground;
            mainCam.clearFlags = CameraClearFlags.SolidColor;

            SmoothFollowCamera followCam = mainCam.GetComponent<SmoothFollowCamera>();
            if (followCam == null)
            {
                followCam = mainCam.gameObject.AddComponent<SmoothFollowCamera>();
            }

            AsciiWorldMap worldMap = Object.FindFirstObjectByType<AsciiWorldMap>();
            if (worldMap == null)
            {
                GameObject worldObj = new GameObject("AsciiWorldMap");
                worldMap = worldObj.AddComponent<AsciiWorldMap>();
            }
            SerializedObject mapSo = new SerializedObject(worldMap);
            var mapWProp = mapSo.FindProperty("_width");
            if (mapWProp != null) mapWProp.intValue = 240;
            var mapHProp = mapSo.FindProperty("_height");
            if (mapHProp != null) mapHProp.intValue = 160;
            var mapCellProp = mapSo.FindProperty("_cellSize");
            if (mapCellProp != null) mapCellProp.floatValue = 0.25f;
            mapSo.ApplyModifiedProperties();

            AsciiGridRenderer gridRenderer = Object.FindFirstObjectByType<AsciiGridRenderer>();
            if (gridRenderer == null)
            {
                GameObject rendererObj = new GameObject("AsciiRenderer");
                gridRenderer = rendererObj.AddComponent<AsciiGridRenderer>();
            }
            SerializedObject renSo = new SerializedObject(gridRenderer);
            var colProp = renSo.FindProperty("_columns");
            if (colProp != null) colProp.intValue = 192;
            var rowProp = renSo.FindProperty("_rows");
            if (rowProp != null) rowProp.intValue = 108;
            var matchProp = renSo.FindProperty("_matchCameraViewport");
            if (matchProp != null) matchProp.boolValue = true;
            var fontProp = renSo.FindProperty("_fontAsset");
            if (fontProp != null && fontProp.objectReferenceValue == null)
            {
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
                if (font != null) fontProp.objectReferenceValue = font;
            }
            renSo.ApplyModifiedProperties();

            player = Object.FindFirstObjectByType<PlayerController>();
            if (player == null)
            {
                GameObject playerObj = new GameObject("Player");
                player = playerObj.AddComponent<PlayerController>();
                playerObj.AddComponent<PlayerAsciiView>();

                var rb = playerObj.GetComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.freezeRotation = true;
                rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

                var col = playerObj.GetComponent<CircleCollider2D>();
                col.radius = 0.35f;

                playerObj.transform.position = new Vector3(30f, 20f, 0f);
            }

            followCam.Target = player.transform;
            followCam.SnapToTarget();

            GameBootstrapper bootstrapper = Object.FindFirstObjectByType<GameBootstrapper>();
            if (bootstrapper == null)
            {
                GameObject bootObj = new GameObject("GameBootstrapper");
                bootstrapper = bootObj.AddComponent<GameBootstrapper>();
            }

            Undo.RegisterCreatedObjectUndo(worldMap.gameObject, "Setup ASCII Scene");
        }

        private static GameObject SpawnModularEnemyIfNotExists(string name, Vector3 position)
        {
            GameObject existing = GameObject.Find(name);
            if (existing != null) return existing;

            GameObject enemyRoot = new GameObject(name);
            enemyRoot.transform.position = position;

            var rb = enemyRoot.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var rootCol = enemyRoot.AddComponent<CircleCollider2D>();
            rootCol.radius = 0.4f;

            var enemy = enemyRoot.AddComponent<EnemyEntity>();
            enemyRoot.AddComponent<EnemyAsciiView>();
            enemyRoot.AddComponent<LimbSacrificeAction>();
            enemyRoot.AddComponent<EnemyDecisionDirector>();

            CreateBodyPart(enemyRoot, "Core", BodyPartType.Core, GameConstants.GLYPH_ENEMY_CORE,
                Vector2.zero, 60f, 0f, 0.25f);

            CreateBodyPart(enemyRoot, "ChestArmor", BodyPartType.ChestArmor, GameConstants.GLYPH_ENEMY_ARMOR,
                new Vector2(0f, 0.4f), 45f, 10f, 0.22f);

            CreateBodyPart(enemyRoot, "LeftArm", BodyPartType.LeftArm, GameConstants.GLYPH_ENEMY_ARM_LEFT,
                new Vector2(-0.5f, 0.1f), 25f, 2f, 0.2f);

            CreateBodyPart(enemyRoot, "RightArm", BodyPartType.RightArm, GameConstants.GLYPH_ENEMY_ARM_RIGHT,
                new Vector2(0.5f, 0.1f), 25f, 2f, 0.2f);

            CreateBodyPart(enemyRoot, "LeftLeg", BodyPartType.LeftLeg, GameConstants.GLYPH_ENEMY_LEG,
                new Vector2(-0.3f, -0.4f), 20f, 0f, 0.18f);

            CreateBodyPart(enemyRoot, "RightLeg", BodyPartType.RightLeg, GameConstants.GLYPH_ENEMY_LEG,
                new Vector2(0.3f, -0.4f), 20f, 0f, 0.18f);

            enemy.CollectBodyParts();

            Undo.RegisterCreatedObjectUndo(enemyRoot, "Spawn Modular Enemy");
            return enemyRoot;
        }

        private static void CreateBodyPart(GameObject parent, string partName, BodyPartType type, char glyph,
            Vector2 offset, float health, float armor, float colRadius)
        {
            GameObject partObj = new GameObject(partName);
            partObj.transform.SetParent(parent.transform, false);
            partObj.transform.localPosition = offset;

            var col = partObj.AddComponent<CircleCollider2D>();
            col.radius = colRadius;

            var dmg = partObj.AddComponent<Damageable>();
            var bodyPart = partObj.AddComponent<EnemyBodyPart>();
            bodyPart.Configure(type, glyph, offset, health, armor);
        }

        [MenuItem("ASCII Game/Tools/Generate Sample Custom Environment Texture", false, 30)]
        public static void GenerateSampleEnvironmentTexture()
        {
            int w = 240;
            int h = 160;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);

            Color brownPath = new Color(0.45f, 0.38f, 0.25f);
            Color blueWater = new Color(0.12f, 0.40f, 0.85f);
            Color greenGround = new Color(0.18f, 0.50f, 0.25f);
            Color greyWall = new Color(0.15f, 0.15f, 0.15f);
            Color rockWhite = new Color(0.75f, 0.78f, 0.82f);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (x < 2 || x >= w - 2 || y < 2 || y >= h - 2)
                    {
                        tex.SetPixel(x, y, greyWall);
                        continue;
                    }

                    float riverNoise = Mathf.Abs(Mathf.PerlinNoise(x * 0.02f, y * 0.02f + 10f) - 0.5f);
                    if (riverNoise < 0.045f)
                    {
                        tex.SetPixel(x, y, blueWater);
                        continue;
                    }

                    float roadNoise = Mathf.Abs(Mathf.PerlinNoise(x * 0.025f + 40f, y * 0.025f) - 0.5f);
                    if (roadNoise < 0.035f)
                    {
                        tex.SetPixel(x, y, brownPath);
                        continue;
                    }

                    float rockNoise = Mathf.PerlinNoise(x * 0.08f + 80f, y * 0.08f + 80f);
                    if (rockNoise > 0.82f)
                    {
                        tex.SetPixel(x, y, rockWhite);
                        continue;
                    }

                    tex.SetPixel(x, y, greenGround);
                }
            }

            tex.Apply();
            byte[] bytes = tex.EncodeToPNG();
            string path = "Assets/Settings/SampleEnvironmentMap.png";
            System.IO.File.WriteAllBytes(path, bytes);
            AssetDatabase.Refresh();

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.isReadable = true;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            Debug.Log($"[AsciiSceneBuilder] Created sample custom environment texture at {path}! Drag into AsciiWorldMap to use.");
        }

        [MenuItem("ASCII Game/Tools/Create Sample Ascii Animation Clips", false, 31)]
        public static void CreateSampleAsciiAnimationClips()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Settings/Animations"))
            {
                AssetDatabase.CreateFolder("Assets/Settings", "Animations");
            }

            var runClip = ScriptableObject.CreateInstance<Animation.AsciiAnimationClip>();
            runClip.name = "Player_Run_Side";
            SerializedObject so = new SerializedObject(runClip);
            var textFramesProp = so.FindProperty("_textFrames");
            textFramesProp.arraySize = 3;
            textFramesProp.GetArrayElementAtIndex(0).stringValue = " . \n<@>\n - ";
            textFramesProp.GetArrayElementAtIndex(1).stringValue = " - \n<@>\n / ";
            textFramesProp.GetArrayElementAtIndex(2).stringValue = " ' \n<@>\n \\ ";
            so.FindProperty("_frameRate").floatValue = 8f;
            so.FindProperty("_loop").boolValue = true;
            so.ApplyModifiedProperties();

            string assetPath = "Assets/Settings/Animations/Player_Run_Side.asset";
            AssetDatabase.CreateAsset(runClip, assetPath);
            AssetDatabase.SaveAssets();

            Debug.Log($"[AsciiSceneBuilder] Created sample ASCII animation clip at {assetPath}! Assign to PlayerAsciiView in Inspector.");
        }
    }
}
#endif
