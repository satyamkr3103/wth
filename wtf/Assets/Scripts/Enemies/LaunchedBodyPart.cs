using UnityEngine;
using AsciiActionGame.Combat;
using AsciiActionGame.Core;
using AsciiActionGame.Rendering;

namespace AsciiActionGame.Enemies
{
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public class LaunchedBodyPart : MonoBehaviour
    {
        private Rigidbody2D _rb;
        private CircleCollider2D _collider;
        private GameObject _instigator;
        private BodyPartType _partType;
        private Color32 _color;
        private Vector2 _startPosition;
        private Vector2 _flyDirection;
        private float _speed = 8.8f;
        private float _maxTravelDistance = 5.4f;
        private float _blastRadius = 1.4f;
        private float _blastDamage = 26.0f;
        private float _lifetime = 3.5f;
        private float _elapsedTime = 0f;
        private bool _isDetonated = false;

        public Vector2 Position => _rb != null ? _rb.position : (Vector2)transform.position;
        public BodyPartType PartType => _partType;
        public bool IsDetonated => _isDetonated;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.gravityScale = 0f;
            _rb.freezeRotation = true;
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            _collider = GetComponent<CircleCollider2D>();
            _collider.isTrigger = true;
            _collider.radius = 0.3f;
        }

        private void OnEnable()
        {
            AsciiGridRenderer.OnRenderDynamicEntities += OnRenderAscii;
        }

        private void OnDisable()
        {
            AsciiGridRenderer.OnRenderDynamicEntities -= OnRenderAscii;
        }

        public void Initialize(
            Vector2 origin,
            Vector2 direction,
            float speed,
            float maxTravelDistance,
            float blastRadius,
            float blastDamage,
            BodyPartType partType,
            Color32 color,
            GameObject instigator)
        {
            transform.position = origin;
            _startPosition = origin;
            _flyDirection = direction.normalized;
            _speed = speed;
            _maxTravelDistance = maxTravelDistance;
            _blastRadius = blastRadius;
            _blastDamage = blastDamage;
            _partType = partType;
            _color = color;
            _instigator = instigator;
            _isDetonated = false;
            _elapsedTime = 0f;

            if (_rb != null)
            {
                _rb.position = origin;
                _rb.linearVelocity = _flyDirection * _speed;
            }
        }

        private void Update()
        {
            if (_isDetonated) return;

            _elapsedTime += Time.deltaTime;

            float traveled = Vector2.Distance(_startPosition, Position);
            if (traveled >= _maxTravelDistance || _elapsedTime >= _lifetime)
            {
                DetonateBlast();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_isDetonated) return;

            if (_instigator != null && (other.gameObject == _instigator || other.transform.IsChildOf(_instigator.transform)))
            {
                return;
            }

            if (other.GetComponent<LaunchedBodyPart>() != null || other.GetComponent<Projectiles.FragmentProjectile>() != null)
            {
                return;
            }

            if (other.GetComponent<Player.PlayerController>() != null || other.GetComponent<IDamageable>() != null || !other.isTrigger)
            {
                DetonateBlast();
            }
        }

        public void DetonateBlast()
        {
            if (_isDetonated) return;

            _isDetonated = true;
            if (_rb != null) _rb.linearVelocity = Vector2.zero;
            if (_collider != null) _collider.enabled = false;

            Vector2 blastCenter = Position;

            Collider2D[] colliders = Physics2D.OverlapCircleAll(blastCenter, _blastRadius);
            foreach (var col in colliders)
            {
                if (col == null) continue;
                if (_instigator != null && (col.gameObject == _instigator || col.transform.IsChildOf(_instigator.transform))) continue;

                IDamageable dmg = col.GetComponent<IDamageable>() ?? col.GetComponentInParent<IDamageable>();
                if (dmg != null && !dmg.IsDead)
                {
                    float dist = Vector2.Distance(blastCenter, col.ClosestPoint(blastCenter));
                    float falloff = Mathf.Clamp01(1f - (dist / _blastRadius));
                    float finalDmg = Mathf.Max(6f, _blastDamage * falloff);
                    Vector2 hitNormal = (col.transform.position - (Vector3)blastCenter).normalized;

                    DamageInfo info = new DamageInfo(finalDmg, DamageType.Explosive, blastCenter, hitNormal, _instigator);
                    dmg.TakeDamage(info);
                }
            }

            if (Projectiles.FragmentPool.Instance != null)
            {
                for (int i = 0; i < 8; i++)
                {
                    float angle = (i * 45f + Random.Range(-10f, 10f)) * Mathf.Deg2Rad;
                    Vector2 shrapnelVel = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(6.0f, 9.5f);
                    Projectiles.FragmentPool.Instance.Spawn(
                        blastCenter,
                        shrapnelVel,
                        12f,
                        1.4f,
                        _instigator,
                        new Color32(255, 120, 40, 255),
                        GameConstants.GLYPH_FRAGMENT
                    );
                }
            }

            DisintegrationEffect.TriggerBurst(blastCenter, 14);
            AsciiPostProcessing.TriggerScreenShake(0.35f);
            AsciiPostProcessing.TriggerGlitch(0.14f, 0.75f);

            Destroy(gameObject, 0.05f);
        }

        private void OnRenderAscii(IAsciiRenderer renderer)
        {
            if (_isDetonated) return;

            Vector2 pos = Position;
            Vector2 dir = _flyDirection;
            Vector2 perp = new Vector2(-dir.y, dir.x);
            float cellW = renderer.CellWidth > 0f ? renderer.CellWidth : 0.25f;

            switch (_partType)
            {
                case BodyPartType.ChestArmor:
                    renderer.DrawWorldGlyph(pos - perp * (cellW * 0.8f), '[', _color, Color.clear, GameConstants.LAYER_PROJECTILES);
                    renderer.DrawWorldGlyph(pos, '#', Color.white, Color.clear, GameConstants.LAYER_PROJECTILES + 1);
                    renderer.DrawWorldGlyph(pos + perp * (cellW * 0.8f), ']', _color, Color.clear, GameConstants.LAYER_PROJECTILES);
                    break;

                case BodyPartType.LeftArm:
                    renderer.DrawWorldGlyph(pos, '<', _color, Color.clear, GameConstants.LAYER_PROJECTILES + 1);
                    renderer.DrawWorldGlyph(pos - dir * (cellW * 0.8f), '=', Color.white, Color.clear, GameConstants.LAYER_PROJECTILES);
                    break;

                case BodyPartType.RightArm:
                    renderer.DrawWorldGlyph(pos, '>', _color, Color.clear, GameConstants.LAYER_PROJECTILES + 1);
                    renderer.DrawWorldGlyph(pos - dir * (cellW * 0.8f), '=', Color.white, Color.clear, GameConstants.LAYER_PROJECTILES);
                    break;

                case BodyPartType.LeftLeg:
                    renderer.DrawWorldGlyph(pos, '[', _color, Color.clear, GameConstants.LAYER_PROJECTILES);
                    renderer.DrawWorldGlyph(pos - dir * (cellW * 0.8f), '|', Color.white, Color.clear, GameConstants.LAYER_PROJECTILES);
                    break;

                case BodyPartType.RightLeg:
                    renderer.DrawWorldGlyph(pos, ']', _color, Color.clear, GameConstants.LAYER_PROJECTILES);
                    renderer.DrawWorldGlyph(pos - dir * (cellW * 0.8f), '|', Color.white, Color.clear, GameConstants.LAYER_PROJECTILES);
                    break;

                default:
                    renderer.DrawWorldGlyph(pos, 'X', _color, Color.clear, GameConstants.LAYER_PROJECTILES);
                    break;
            }

            renderer.DrawWorldGlyph(pos - dir * (cellW * 1.8f), '*', new Color32(255, 180, 50, 255), Color.clear, GameConstants.LAYER_PROJECTILES);
            renderer.DrawWorldGlyph(pos - dir * (cellW * 2.8f), '.', new Color32(220, 80, 30, 180), Color.clear, GameConstants.LAYER_PROJECTILES);
        }
    }
}
