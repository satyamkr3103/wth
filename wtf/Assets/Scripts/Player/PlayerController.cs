using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace AsciiActionGame.Player
{
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float _moveSpeed = 8.0f;
        [SerializeField] private float _acceleration = 50.0f;
        [SerializeField] private float _deceleration = 40.0f;

        [Header("Aim Settings")]
        [SerializeField] private Camera _aimCamera;

        private Rigidbody2D _rb;
        private Vector2 _moveInput;
        private Vector2 _aimDirection = Vector2.up;
        private Vector2 _currentVelocity;

        public Vector2 Position => _rb.position;
        public Vector2 Velocity => _rb.linearVelocity;
        public Vector2 AimDirection => _aimDirection;
        public bool IsMoving => _moveInput.sqrMagnitude > 0.01f;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.gravityScale = 0f;
            _rb.freezeRotation = true;
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            if (_aimCamera == null)
            {
                _aimCamera = Camera.main;
            }
        }

        private void Update()
        {
            ReadInput();
            UpdateAimDirection();
        }

        private void FixedUpdate()
        {
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

            _moveInput = input.normalized;
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
            Vector2 dir = ((Vector2)mouseWorldPos - _rb.position).normalized;

            if (dir.sqrMagnitude > 0.01f)
            {
                _aimDirection = dir;
            }
            else if (IsMoving)
            {
                _aimDirection = _moveInput;
            }
        }

        private void ApplyMovement()
        {
            Vector2 targetVelocity = _moveInput * _moveSpeed;
            float rate = _moveInput.sqrMagnitude > 0.01f ? _acceleration : _deceleration;

            _rb.linearVelocity = Vector2.MoveTowards(_rb.linearVelocity, targetVelocity, rate * Time.fixedDeltaTime);
        }

        public void OnMove(InputValue value)
        {
            _moveInput = value.Get<Vector2>();
        }
    }
}
