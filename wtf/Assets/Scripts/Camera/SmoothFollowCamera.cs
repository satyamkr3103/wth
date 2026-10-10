using UnityEngine;
using AsciiActionGame.World;

namespace AsciiActionGame.CameraControl
{
    [RequireComponent(typeof(Camera))]
    public class SmoothFollowCamera : MonoBehaviour
    {
        [Header("Target Tracking")]
        [SerializeField] private Transform _target;
        [SerializeField] private float _smoothTime = 0.15f;
        [SerializeField] private Vector3 _offset = new Vector3(0f, 0f, -10f);

        [Header("Look Ahead")]
        [SerializeField] private bool _enableLookAhead = true;
        [SerializeField] private float _lookAheadDistance = 2.0f;
        [SerializeField] private float _lookAheadSmoothTime = 0.25f;

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
                Vector3 moveDir = new Vector3(_targetRb.linearVelocity.x, _targetRb.linearVelocity.y, 0f).normalized;
                Vector3 targetLookAhead = moveDir * _lookAheadDistance;
                _currentLookAhead = Vector3.SmoothDamp(_currentLookAhead, targetLookAhead, ref _lookAheadVelocity, _lookAheadSmoothTime);
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

            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref _currentVelocity, _smoothTime);
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
