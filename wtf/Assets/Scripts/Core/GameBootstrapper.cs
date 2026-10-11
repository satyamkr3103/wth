using UnityEngine;
using AsciiActionGame.Rendering;
using AsciiActionGame.World;
using AsciiActionGame.Player;
using AsciiActionGame.CameraControl;
using AsciiActionGame.Weapons;
using AsciiActionGame.Projectiles;
using AsciiActionGame.Combat;

namespace AsciiActionGame.Core
{
    public class GameBootstrapper : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private AsciiWorldMap _worldMap;
        [SerializeField] private AsciiGridRenderer _renderer;
        [SerializeField] private PlayerController _player;
        [SerializeField] private SmoothFollowCamera _cameraFollow;
        [SerializeField] private AsciiColorPalette _palette;

        private void Awake()
        {
            ValidateOrFindReferences();
        }

        private void ValidateOrFindReferences()
        {
            if (_worldMap == null) _worldMap = FindFirstObjectByType<AsciiWorldMap>();
            if (_renderer == null) _renderer = FindFirstObjectByType<AsciiGridRenderer>();
            if (_player == null) _player = FindFirstObjectByType<PlayerController>();
            if (_cameraFollow == null) _cameraFollow = FindFirstObjectByType<SmoothFollowCamera>();

            if (_player != null)
            {
                var dmg = _player.GetComponent<Damageable>();
                if (dmg == null)
                {
                    dmg = _player.gameObject.AddComponent<Damageable>();
                    dmg.Initialize(100f, 0f);
                }
            }

            if (_cameraFollow != null && _player != null && _cameraFollow.Target == null)
            {
                _cameraFollow.Target = _player.transform;
                _cameraFollow.SnapToTarget();
            }

            if (FindFirstObjectByType<EraserBeamVfx>() == null)
            {
                var vfxObj = new GameObject("EraserBeamVfx");
                vfxObj.AddComponent<EraserBeamVfx>();
            }

            if (FindFirstObjectByType<FragmentPool>() == null)
            {
                var poolObj = new GameObject("FragmentPool");
                poolObj.AddComponent<FragmentPool>();
            }

            if (FindFirstObjectByType<DisintegrationEffect>() == null)
            {
                var disObj = new GameObject("DisintegrationEffect");
                disObj.AddComponent<DisintegrationEffect>();
            }

            if (FindFirstObjectByType<AnimatedTerrainTile>() == null)
            {
                var animObj = new GameObject("AnimatedTerrainTile");
                animObj.AddComponent<AnimatedTerrainTile>();
            }

            if (FindFirstObjectByType<AsciiPostProcessing>() == null)
            {
                var postObj = new GameObject("AsciiPostProcessing");
                postObj.AddComponent<AsciiPostProcessing>();
            }

            if (FindFirstObjectByType<AsciiCombatHud>() == null)
            {
                var hudObj = new GameObject("AsciiCombatHud");
                hudObj.AddComponent<AsciiCombatHud>();
            }
        }
    }
}
