using UnityEngine;
using AsciiActionGame.Weapons;
using AsciiActionGame.Enemies;
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

        [Header("Body Part Throw")]
        [SerializeField] private int _bodyPartAmmo = 3;
        [SerializeField] private float _throwCooldown = 0.55f;
        [SerializeField] private float _throwSpeed = 10.5f;

        private PlayerController _controller;
        private PlayerAsciiView _asciiView;
        private bool _isFireInputHeld = false;
        private bool _isThrowInputPressed = false;
        private float _lastThrowTime = -999f;

        public WeaponBase ActiveWeapon => _activeWeapon;
        public string ActiveWeaponName => _activeWeapon != null ? _activeWeapon.WeaponName : "None";
        public int BodyPartAmmo => _bodyPartAmmo;

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
            PollInputs();

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

            if (_isThrowInputPressed && Time.time >= _lastThrowTime + _throwCooldown)
            {
                ExecuteBodyPartThrow();
            }
        }

        private void PollInputs()
        {
            bool firePressed = false;
            bool throwPressed = false;

#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                if (Mouse.current.leftButton.isPressed) firePressed = true;
                if (Mouse.current.rightButton.wasPressedThisFrame) throwPressed = true;
            }
            if (Keyboard.current != null)
            {
                if (Keyboard.current.spaceKey.isPressed) firePressed = true;
                if (Keyboard.current.eKey.wasPressedThisFrame || Keyboard.current.qKey.wasPressedThisFrame) throwPressed = true;
            }
#else
            if (Input.GetMouseButton(0) || Input.GetKey(KeyCode.Space)) firePressed = true;
            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Q)) throwPressed = true;
#endif

            _isFireInputHeld = firePressed;
            _isThrowInputPressed = throwPressed;
        }

        public void AddCollectedBodyPart()
        {
            _bodyPartAmmo++;
        }

        private void ExecuteBodyPartThrow()
        {
            if (_bodyPartAmmo <= 0) return;

            _bodyPartAmmo--;
            _lastThrowTime = Time.time;

            Vector2 origin = (_asciiView != null) ? _asciiView.GunMuzzleWorldPosition : _controller.Position;
            Vector2 aimDir = _controller.AimDirection;

            GameObject obj = new GameObject("PlayerThrown_BodyPart");
            obj.AddComponent<Rigidbody2D>();
            obj.AddComponent<CircleCollider2D>();
            var launched = obj.AddComponent<LaunchedBodyPart>();

            launched.Initialize(
                origin,
                aimDir,
                _throwSpeed,
                6.5f,
                1.6f,
                35f,
                BodyPartType.ChestArmor,
                new Color32(0, 240, 255, 255),
                gameObject
            );

            _asciiView?.TriggerFire();
            AsciiActionGame.Rendering.AsciiPostProcessing.TriggerScreenShake(0.2f);
        }

        public void EquipWeapon(WeaponBase weapon)
        {
            _activeWeapon = weapon;
        }
    }
}
