using UnityEngine;
using AsciiActionGame.Weapons;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace AsciiActionGame.Player
{
    [RequireComponent(typeof(PlayerController))]
    public class PlayerCombat : MonoBehaviour
    {
        [Header("Equipped Weapon")]
        [SerializeField] private WeaponBase _activeWeapon;

        private PlayerController _controller;
        private PlayerAsciiView _asciiView;
        private bool _isFireInputHeld = false;

        public WeaponBase ActiveWeapon => _activeWeapon;
        public string ActiveWeaponName => _activeWeapon != null ? _activeWeapon.WeaponName : "None";

        private void Awake()
        {
            _controller = GetComponent<PlayerController>();
            _asciiView = GetComponent<PlayerAsciiView>();

            EnsureWeaponExists();
        }

        private void EnsureWeaponExists()
        {
            if (_activeWeapon == null)
            {
                _activeWeapon = GetComponentInChildren<WeaponBase>(true);
            }
            if (_activeWeapon == null)
            {
                _activeWeapon = FindFirstObjectByType<WeaponBase>();
            }
            if (_activeWeapon == null)
            {
                var weaponObj = new GameObject("EraserBeamWeapon");
                weaponObj.transform.SetParent(transform, false);
                _activeWeapon = weaponObj.AddComponent<EraserBeamWeapon>();
            }
        }

        private void Update()
        {
            if (_controller != null && _controller.IsDead) return;

            EnsureWeaponExists();
            PollFireInput();

            if (_isFireInputHeld && _activeWeapon != null)
            {
                Vector2 origin = (_asciiView != null) ? _asciiView.GunMuzzleWorldPosition : _controller.Position;
                Vector2 aimDir = _controller.AimDirection;
                _activeWeapon.Fire(origin, aimDir);
                _asciiView?.TriggerFire();
            }
            else if (!_isFireInputHeld && _activeWeapon != null && _activeWeapon.IsFiring)
            {
                _activeWeapon.StopFiring();
            }
        }

        private void PollFireInput()
        {
            bool firePressed = false;

#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null && Mouse.current.leftButton.isPressed)
            {
                firePressed = true;
            }
            if (Keyboard.current != null && Keyboard.current.spaceKey.isPressed)
            {
                firePressed = true;
            }
#else
            if (Input.GetMouseButton(0) || Input.GetKey(KeyCode.Space))
            {
                firePressed = true;
            }
#endif

            _isFireInputHeld = firePressed;
        }

        public void EquipWeapon(WeaponBase weapon)
        {
            _activeWeapon = weapon;
        }
    }
}
