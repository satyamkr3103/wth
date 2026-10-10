using System.Collections.Generic;
using UnityEngine;
using AsciiActionGame.Core;
using AsciiActionGame.Rendering;

namespace AsciiActionGame.Projectiles
{
    public class FragmentPool : MonoBehaviour
    {
        public static FragmentPool Instance { get; private set; }

        [Header("Pool Configuration")]
        [SerializeField] private int _initialPoolSize = 40;
        [SerializeField] private GameObject _projectilePrefab;

        [Header("Palette")]
        [SerializeField] private AsciiColorPalette _palette;

        private readonly Queue<FragmentProjectile> _availablePool = new Queue<FragmentProjectile>();
        private readonly List<FragmentProjectile> _activeProjectiles = new List<FragmentProjectile>();

        public int ActiveCount => _activeProjectiles.Count;
        public int AvailableCount => _availablePool.Count;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }

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

            PrewarmPool();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void OnEnable()
        {
            AsciiGridRenderer.OnRenderDynamicEntities += OnRenderAscii;
        }

        private void OnDisable()
        {
            AsciiGridRenderer.OnRenderDynamicEntities -= OnRenderAscii;
        }

        private void PrewarmPool()
        {
            for (int i = 0; i < _initialPoolSize; i++)
            {
                var projectile = CreateNewProjectileInstance();
                projectile.gameObject.SetActive(false);
                _availablePool.Enqueue(projectile);
            }
        }

        private FragmentProjectile CreateNewProjectileInstance()
        {
            GameObject obj;
            if (_projectilePrefab != null)
            {
                obj = Instantiate(_projectilePrefab, transform);
            }
            else
            {
                obj = new GameObject("FragmentProjectile_Instance");
                obj.transform.SetParent(transform);
                obj.AddComponent<Rigidbody2D>();
                obj.AddComponent<CircleCollider2D>();
                obj.AddComponent<FragmentProjectile>();
            }

            var proj = obj.GetComponent<FragmentProjectile>();
            obj.SetActive(false);
            return proj;
        }

        public FragmentProjectile Spawn(Vector2 origin, Vector2 velocity, float damage, float lifetime,
            GameObject instigator, Color32? color = null, char glyph = GameConstants.GLYPH_FRAGMENT)
        {
            FragmentProjectile projectile;

            if (_availablePool.Count > 0)
            {
                projectile = _availablePool.Dequeue();
            }
            else
            {
                projectile = CreateNewProjectileInstance();
            }

            Color32 projColor = color ?? (_palette != null ? _palette.EnemyFragment : new Color32(255, 90, 40, 255));
            projectile.Launch(origin, velocity, damage, lifetime, instigator, projColor, glyph);

            _activeProjectiles.Add(projectile);
            return projectile;
        }

        public void Despawn(FragmentProjectile projectile)
        {
            if (projectile == null) return;

            _activeProjectiles.Remove(projectile);
            if (!_availablePool.Contains(projectile))
            {
                _availablePool.Enqueue(projectile);
            }
        }

        private void OnRenderAscii(IAsciiRenderer renderer)
        {
            for (int i = 0; i < _activeProjectiles.Count; i++)
            {
                var proj = _activeProjectiles[i];
                if (proj != null && proj.IsActive)
                {
                    renderer.DrawWorldGlyph(
                        proj.Position,
                        proj.Glyph,
                        proj.Color,
                        Color.clear,
                        GameConstants.LAYER_PROJECTILES
                    );
                }
            }
        }
    }
}
