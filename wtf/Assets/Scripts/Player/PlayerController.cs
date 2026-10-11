using UnityEngine;
using AsciiActionGame.Combat;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace AsciiActionGame.Player
{
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(Damageable))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float _moveSpeed = 8.5f;
        [SerializeField] private float _acceleration = 24.0f;
        [SerializeField] private float _deceleration = 20.0f;
        [SerializeField] private bool _useSmoothExponentialMovement = true;

        [Header("Aim Settings")]
        [SerializeField] private Camera _aimCamera;
        [SerializeField] private float _aimSmoothSpeed = 28.0f;
        [SerializeField] private float _mouseDeadzoneRadius = 0.35f;

        [Header("Health & Damage")]
        [SerializeField] private float _maxHealth = 100f;
        [SerializeField] private float _armor = 0f;

        private Rigidbody2D _rb;
        private Damageable _damageable;
        private Vector2 _moveInput;
        private Vector2 _aimDirection = Vector2.down;
        private Vector2 _targetAimDirection = Vector2.down;

        public Vector2 Position => transform.position;
        public Vector2 Velocity => _rb != null ? _rb.linearVelocity : Vector2.zero;
        public Vector2 AimDirection => _aimDirection;
        public bool IsMoving => _moveInput.sqrMagnitude > 0.01f || (_rb != null && _rb.linearVelocity.sqrMagnitude > 0.04f);

        public Damageable Damageable => _damageable;
        public float CurrentHealth => _damageable != null ? _damageable.CurrentHealth : _maxHealth;
        public float MaxHealth => _maxHealth;
        public bool IsDead => _damageable != null && _damageable.IsDead;

        public float MoveSpeed { get => _moveSpeed; set => _moveSpeed = value; }
        public float Acceleration { get => _acceleration; set => _acceleration = value; }
        public float Deceleration { get => _deceleration; set => _deceleration = value; }
        public bool UseSmoothExponentialMovement { get => _useSmoothExponentialMovement; set => _useSmoothExponentialMovement = value; }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.gravityScale = 0f;
            _rb.freezeRotation = true;
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _rb.interpolation = RigidbodyInterpolation2D.Interpolate;

            _damageable = GetComponent<Damageable>();
            if (_damageable == null)
            {
                _damageable = gameObject.AddComponent<Damageable>();
            }
            _damageable.Initialize(_maxHealth, _armor);

            if (_aimCamera == null)
            {
                _aimCamera = Camera.main;
            }
        }

        private void OnEnable()
        {
            if (_damageable != null)
            {
                _damageable.OnDamaged += HandleDamageTaken;
                _damageable.OnDied += HandlePlayerDied;
            }
        }

        private void OnDisable()
        {
            if (_damageable != null)
            {
                _damageable.OnDamaged -= HandleDamageTaken;
                _damageable.OnDied -= HandlePlayerDied;
            }
        }

        private void Update()
        {
            if (IsDead) return;

            ReadInput();
            UpdateAimDirection();
        }

        private void FixedUpdate()
        {
            if (IsDead)
            {
                if (_rb != null) _rb.linearVelocity = Vector2.zero;
                return;
            }

            ApplyMovement();
        }

        private void ReadInput()
        {
            Vector2 input = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) input.y += 1f;
                if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) input.y -= 1f;
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) input.x -= 1f;
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) input.x += 1f;
            }
#else
            input.x = Input.GetAxisRaw("Horizontal");
            input.y = Input.GetAxisRaw("Vertical");
#endif

            _moveInput = Vector2.ClampMagnitude(input, 1.0f);
        }

        private void UpdateAimDirection()
        {
            if (_aimCamera == null)
            {
                _aimCamera = Camera.main;
                if (_aimCamera == null) return;
            }

            Vector3 mouseScreenPos = Vector3.zero;

#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                mouseScreenPos = Mouse.current.position.ReadValue();
            }
#else
            mouseScreenPos = Input.mousePosition;
#endif

            Vector3 mouseWorldPos = _aimCamera.ScreenToWorldPoint(mouseScreenPos);
            Vector2 toMouse = (Vector2)mouseWorldPos - (Vector2)transform.position;

            if (toMouse.sqrMagnitude >= (_mouseDeadzoneRadius * _mouseDeadzoneRadius))
            {
                _targetAimDirection = toMouse.normalized;
            }
            else if (_moveInput.sqrMagnitude > 0.01f)
            {
                _targetAimDirection = _moveInput.normalized;
            }

            float aimBlend = 1.0f - Mathf.Exp(-_aimSmoothSpeed * Time.deltaTime);
            _aimDirection = Vector2.Lerp(_aimDirection, _targetAimDirection, aimBlend).normalized;
        }

        private void ApplyMovement()
        {
            if (_rb == null) return;

            Vector2 targetVelocity = _moveInput * _moveSpeed;
            bool hasInput = _moveInput.sqrMagnitude > 0.01f;

            if (_useSmoothExponentialMovement)
            {
                float rate = hasInput ? _acceleration : _deceleration;
                float blend = 1.0f - Mathf.Exp(-rate * Time.fixedDeltaTime);
                _rb.linearVelocity = Vector2.Lerp(_rb.linearVelocity, targetVelocity, blend);

                if (!hasInput && _rb.linearVelocity.sqrMagnitude < 0.002f)
                {
                    _rb.linearVelocity = Vector2.zero;
                }
            }
            else
            {
                float rate = hasInput ? _acceleration : _deceleration;
                _rb.linearVelocity = Vector2.MoveTowards(_rb.linearVelocity, targetVelocity, rate * Time.fixedDeltaTime);
            }
        }

        private void HandleDamageTaken(DamageInfo info)
        {
            Vector2 pushDir = info.HitNormal.sqrMagnitude > 0.01f ? info.HitNormal : -_aimDirection;
            if (_rb != null)
            {
                _rb.AddForce(pushDir * 5.5f, ForceMode2D.Impulse);
            }

            AsciiActionGame.Rendering.AsciiPostProcessing.TriggerScreenShake(0.35f);
            AsciiActionGame.Rendering.AsciiPostProcessing.TriggerGlitch(0.14f, 0.85f);
        }

        private void HandlePlayerDied(DamageInfo info)
        {
            if (_rb != null) _rb.linearVelocity = Vector2.zero;

            AsciiActionGame.Rendering.DisintegrationEffect.TriggerBurst(transform.position, 28, new Color32(0, 240, 255, 255));
            AsciiActionGame.Rendering.AsciiPostProcessing.TriggerScreenShake(0.6f);
            AsciiActionGame.Rendering.AsciiPostProcessing.TriggerGlitch(0.35f, 1.0f);

            Invoke(nameof(Respawn), 2.2f);
        }

        private void Respawn()
        {
            if (_damageable != null)
            {
                _damageable.Initialize(_maxHealth, _armor);
            }
            AsciiActionGame.Rendering.DisintegrationEffect.TriggerBurst(transform.position, 12, new Color32(0, 255, 120, 255));
        }

        public void OnMove(InputValue value)
        {
            _moveInput = Vector2.ClampMagnitude(value.Get<Vector2>(), 1.0f);
        }
    }
}
