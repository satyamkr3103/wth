using UnityEngine;
using AsciiActionGame.Combat;
using AsciiActionGame.Core;
using AsciiActionGame.Rendering;

namespace AsciiActionGame.Projectiles
{
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public class FragmentProjectile : MonoBehaviour
    {
        [Header("Projectile Properties")]
        [SerializeField] private float _damage = 18f;
        [SerializeField] private float _lifetime = 2.5f;
        [SerializeField] private char _glyph = GameConstants.GLYPH_FRAGMENT;
        [SerializeField] private Color32 _color = new Color32(255, 100, 40, 255);

        [Header("Collision & Physics")]
        [SerializeField] private LayerMask _targetLayers = ~0;
        [SerializeField] private bool _enableTumblingGlyphs = true;

        private Rigidbody2D _rb;
        private CircleCollider2D _collider;
        private GameObject _instigator;
        private float _remainingLifetime;
        private float _animTimer;
        private bool _isActive = false;

        private static readonly char[] TUMBLING_GLYPHS = { '*', '+', 'x', '%' };

        public bool IsActive => _isActive;
        public Vector2 Position => _rb != null ? _rb.position : (Vector2)transform.position;
        public char Glyph => _glyph;
        public Color32 Color => _color;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.gravityScale = 0f;
            _rb.freezeRotation = true;
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            _collider = GetComponent<CircleCollider2D>();
            _collider.isTrigger = true;
            _collider.radius = 0.22f;
        }

        private void Update()
        {
            if (!_isActive) return;

            _remainingLifetime -= Time.deltaTime;
            if (_remainingLifetime <= 0f)
            {
                Deactivate();
                return;
            }

            if (_enableTumblingGlyphs)
            {
                _animTimer += Time.deltaTime;
                int frame = Mathf.FloorToInt(_animTimer * 14f) % TUMBLING_GLYPHS.Length;
                _glyph = TUMBLING_GLYPHS[frame];
            }
        }

        public void Launch(Vector2 origin, Vector2 velocity, float damage, float lifetime,
            GameObject instigator, Color32 color, char initialGlyph = GameConstants.GLYPH_FRAGMENT)
        {
            transform.position = origin;
            _rb.position = origin;
            _rb.linearVelocity = velocity;

            _damage = damage;
            _lifetime = lifetime;
            _remainingLifetime = lifetime;
            _instigator = instigator;
            _color = color;
            _glyph = initialGlyph;
            _animTimer = Random.Range(0f, 1f);

            _isActive = true;
            gameObject.SetActive(true);
            _collider.enabled = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!_isActive) return;

            if (_instigator != null && (other.gameObject == _instigator || other.transform.IsChildOf(_instigator.transform)))
            {
                return;
            }

            if (other.GetComponent<FragmentProjectile>() != null)
            {
                return;
            }

            IDamageable damageable = other.GetComponent<IDamageable>();
            if (damageable == null)
            {
                damageable = other.GetComponentInParent<IDamageable>();
            }

            if (damageable != null && !damageable.IsDead)
            {
                Vector2 hitPoint = other.ClosestPoint(transform.position);
                Vector2 hitNormal = ((Vector2)transform.position - hitPoint).normalized;

                DamageInfo damageInfo = new DamageInfo(
                    _damage,
                    DamageType.Explosive,
                    hitPoint,
                    hitNormal,
                    _instigator
                );

                damageable.TakeDamage(damageInfo);
                Deactivate();
                return;
            }

            if (!other.isTrigger)
            {
                Deactivate();
            }
        }

        public void Deactivate()
        {
            if (!_isActive) return;

            _isActive = false;
            _rb.linearVelocity = Vector2.zero;
            _collider.enabled = false;
            gameObject.SetActive(false);

            if (FragmentPool.Instance != null)
            {
                FragmentPool.Instance.Despawn(this);
            }
        }
    }
}
