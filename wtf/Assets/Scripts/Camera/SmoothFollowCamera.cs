using UnityEngine;
using AsciiActionGame.World;

namespace AsciiActionGame.CameraControl
{
    [RequireComponent(typeof(Camera))]
    public class SmoothFollowCamera : MonoBehaviour
    {
        [Header("Target Tracking")]
        [SerializeField] private Transform _target;
        [SerializeField] private bool _enableSmoothing = true;
        [SerializeField] private float _smoothTime = 0.12f;
        [SerializeField] private Vector3 _offset = new Vector3(0f, 0f, -10f);

        [Header("Look Ahead")]
        [SerializeField] private bool _enableLookAhead = true;
        [SerializeField] private float _lookAheadDistance = 1.2f;
        [SerializeField] private float _lookAheadSmoothTime = 0.20f;
        [SerializeField] private float _minVelocityForLookAhead = 0.25f;

        [Header("Boundary Clamping")]
        [SerializeField] private bool _clampToWorldBounds = true;
        [SerializeField] private AsciiWorldMap _worldMap;

        private Vector3 _currentVelocity;
        private Vector3 _currentLookAhead;
        private Vector3 _lookAheadVelocity;
        private Rigidbody2D _targetRb;
        private Camera _cam;

        public Transform Target
        {
            get => _target;
            set
            {
                _target = value;
                _targetRb = _target != null ? _target.GetComponent<Rigidbody2D>() : null;
            }
        }

        public bool EnableSmoothing { get => _enableSmoothing; set => _enableSmoothing = value; }
        public bool EnableLookAhead { get => _enableLookAhead; set => _enableLookAhead = value; }
        public float SmoothTime { get => _smoothTime; set => _smoothTime = value; }
        public float LookAheadDistance { get => _lookAheadDistance; set => _lookAheadDistance = value; }

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            if (_worldMap == null)
            {
                _worldMap = FindFirstObjectByType<AsciiWorldMap>();
            }

            if (_target != null)
            {
                _targetRb = _target.GetComponent<Rigidbody2D>();
            }
        }

        private void LateUpdate()
        {
            if (_target == null) return;

            Vector3 targetPos = _target.position;

            if (_enableLookAhead && _targetRb != null)
            {
                Vector2 vel = _targetRb.linearVelocity;
                float speed = vel.magnitude;

                if (speed > _minVelocityForLookAhead)
                {
                    float speedFactor = Mathf.Clamp01(speed / 6.0f);
                    Vector3 targetLookAhead = (Vector3)(vel.normalized * (_lookAheadDistance * speedFactor));
                    _currentLookAhead = Vector3.SmoothDamp(_currentLookAhead, targetLookAhead, ref _lookAheadVelocity, _lookAheadSmoothTime);
                }
                else
                {
                    _currentLookAhead = Vector3.SmoothDamp(_currentLookAhead, Vector3.zero, ref _lookAheadVelocity, _lookAheadSmoothTime * 0.75f);
                }
            }
            else
            {
                _currentLookAhead = Vector3.zero;
            }

            Vector3 desiredPosition = targetPos + _offset + _currentLookAhead;

            if (_clampToWorldBounds && _worldMap != null)
            {
                float camVertExtent = _cam.orthographicSize;
                float camHorzExtent = _cam.orthographicSize * _cam.aspect;

                Bounds worldBounds = _worldMap.GetWorldBounds();
                float minX = worldBounds.min.x + camHorzExtent;
                float maxX = worldBounds.max.x - camHorzExtent;
                float minY = worldBounds.min.y + camVertExtent;
                float maxY = worldBounds.max.y - camVertExtent;

                if (minX < maxX) desiredPosition.x = Mathf.Clamp(desiredPosition.x, minX, maxX);
                else desiredPosition.x = worldBounds.center.x;

                if (minY < maxY) desiredPosition.y = Mathf.Clamp(desiredPosition.y, minY, maxY);
                else desiredPosition.y = worldBounds.center.y;
            }

            if (_enableSmoothing)
            {
                transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref _currentVelocity, _smoothTime);
            }
            else
            {
                transform.position = desiredPosition;
            }
        }

        public void SnapToTarget()
        {
            if (_target != null)
            {
                transform.position = _target.position + _offset;
            }
        }
    }
}
