using UnityEngine;
using UnityEngine.UI;
using TMPro;
using AsciiActionGame.Core;
using AsciiActionGame.Player;
using AsciiActionGame.Rendering;
using AsciiActionGame.Enemies;

namespace AsciiActionGame.Rendering
{
    public class AsciiCombatHud : MonoBehaviour
    {
        [Header("Game Over & Game Won Panels (Optional)")]
        [SerializeField] private GameObject _gameOverPanel;
        [SerializeField] private GameObject _gameWonPanel;

        [Header("Canvas Text Display")]
        [SerializeField] private TMP_Text _playerHudText;
        [SerializeField] private TMP_Text _bossHudText;
        [SerializeField] private bool _useCanvasText = true;

        private GameObject _playerPanelObj;
        private GameObject _bossPanelObj;

        private PlayerController _player;
        private PlayerCombat _combat;
        private BossEntity _boss;
        private bool _isGameOver = false;
        private bool _isGameWon = false;

        private float _displayedPlayerHp = -1f;
        private float _displayedBossHp = -1f;

        private void Awake()
        {
            FindPlayer();
            EnsureCanvasText();
        }

        private void OnEnable()
        {
            BossEntity.OnBossDied += HandleBossDefeated;
        }

        private void OnDisable()
        {
            BossEntity.OnBossDied -= HandleBossDefeated;
        }

        private void Start()
        {
            if (_gameOverPanel != null) _gameOverPanel.SetActive(false);
            if (_gameWonPanel != null) _gameWonPanel.SetActive(false);
        }

        private void FindPlayer()
        {
            if (_player == null) _player = FindFirstObjectByType<PlayerController>();
            if (_combat == null && _player != null) _combat = _player.GetComponent<PlayerCombat>();
            if (_boss == null) _boss = FindFirstObjectByType<BossEntity>();

            if (_player != null && _displayedPlayerHp < 0f)
            {
                _displayedPlayerHp = _player.CurrentHealth;
            }
            if (_boss != null && _displayedBossHp < 0f)
            {
                _displayedBossHp = _boss.CurrentHealth;
            }
        }

        private void EnsureCanvasText()
        {
            if (!_useCanvasText) return;

            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                canvas = FindFirstObjectByType<Canvas>();
            }

            if (canvas == null)
            {
                var canvasObj = new GameObject("CombatCanvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
                canvasObj.AddComponent<GraphicRaycaster>();
            }
            else
            {
                if (!canvas.gameObject.activeSelf)
                {
                    canvas.gameObject.SetActive(true);
                }

                var scaler = canvas.GetComponent<CanvasScaler>();
                if (scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ConstantPixelSize)
                {
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    scaler.referenceResolution = new Vector2(1920f, 1080f);
                    scaler.matchWidthOrHeight = 0.5f;
                }
            }

            Transform uiRoot = canvas.transform;

            if (_playerHudText == null)
            {
                var existingPanel = uiRoot.Find("PlayerHudPanel");
                if (existingPanel != null)
                {
                    _playerPanelObj = existingPanel.gameObject;
                    _playerHudText = existingPanel.GetComponentInChildren<TMP_Text>();
                }
                else
                {
                    _playerPanelObj = new GameObject("PlayerHudPanel");
                    _playerPanelObj.transform.SetParent(uiRoot, false);

                    var panelRect = _playerPanelObj.AddComponent<RectTransform>();
                    panelRect.anchorMin = new Vector2(0f, 1f);
                    panelRect.anchorMax = new Vector2(0f, 1f);
                    panelRect.pivot = new Vector2(0f, 1f);
                    panelRect.anchoredPosition = new Vector2(30f, -30f);
                    panelRect.sizeDelta = new Vector2(580f, 92f);

                    var img = _playerPanelObj.AddComponent<Image>();
                    img.color = new Color(0.03f, 0.05f, 0.08f, 0.88f);

                    var textObj = new GameObject("PlayerHudText");
                    textObj.transform.SetParent(_playerPanelObj.transform, false);

                    var textRect = textObj.AddComponent<RectTransform>();
                    textRect.anchorMin = Vector2.zero;
                    textRect.anchorMax = Vector2.one;
                    textRect.offsetMin = new Vector2(16f, 8f);
                    textRect.offsetMax = new Vector2(-16f, -8f);

                    _playerHudText = textObj.AddComponent<TextMeshProUGUI>();
                    _playerHudText.fontSize = 20f;
                    _playerHudText.color = Color.white;
                    _playerHudText.alignment = TextAlignmentOptions.MidlineLeft;
                    _playerHudText.textWrappingMode = TextWrappingModes.NoWrap;
                }
            }

            if (_bossHudText == null)
            {
                var existingBossPanel = uiRoot.Find("BossHudPanel");
                if (existingBossPanel != null)
                {
                    _bossPanelObj = existingBossPanel.gameObject;
                    _bossHudText = existingBossPanel.GetComponentInChildren<TMP_Text>();
                }
                else
                {
                    _bossPanelObj = new GameObject("BossHudPanel");
                    _bossPanelObj.transform.SetParent(uiRoot, false);

                    var bossPanelRect = _bossPanelObj.AddComponent<RectTransform>();
                    bossPanelRect.anchorMin = new Vector2(1f, 1f);
                    bossPanelRect.anchorMax = new Vector2(1f, 1f);
                    bossPanelRect.pivot = new Vector2(1f, 1f);
                    bossPanelRect.anchoredPosition = new Vector2(-30f, -30f);
                    bossPanelRect.sizeDelta = new Vector2(620f, 92f);

                    var bossImg = _bossPanelObj.AddComponent<Image>();
                    bossImg.color = new Color(0.08f, 0.02f, 0.03f, 0.88f);

                    var bossTextObj = new GameObject("BossHudText");
                    bossTextObj.transform.SetParent(_bossPanelObj.transform, false);

                    var bossTextRect = bossTextObj.AddComponent<RectTransform>();
                    bossTextRect.anchorMin = Vector2.zero;
                    bossTextRect.anchorMax = Vector2.one;
                    bossTextRect.offsetMin = new Vector2(16f, 8f);
                    bossTextRect.offsetMax = new Vector2(-16f, -8f);

                    _bossHudText = bossTextObj.AddComponent<TextMeshProUGUI>();
                    _bossHudText.fontSize = 20f;
                    _bossHudText.color = new Color32(255, 40, 50, 255);
                    _bossHudText.alignment = TextAlignmentOptions.MidlineRight;
                    _bossHudText.textWrappingMode = TextWrappingModes.NoWrap;
                }
            }

            var gridRenderer = FindFirstObjectByType<AsciiGridRenderer>();
            if (gridRenderer != null && gridRenderer.FontAsset != null)
            {
                if (_playerHudText != null) _playerHudText.font = gridRenderer.FontAsset;
                if (_bossHudText != null) _bossHudText.font = gridRenderer.FontAsset;
            }
        }

        private void Update()
        {
            if (_player == null || _boss == null)
            {
                FindPlayer();
            }

            if (!_isGameOver && _player != null && _player.IsDead)
            {
                TriggerGameOver();
            }

            if (_player != null)
            {
                float targetPlayerHp = Mathf.Max(0f, _player.CurrentHealth);
                if (_displayedPlayerHp < 0f)
                {
                    _displayedPlayerHp = targetPlayerHp;
                }
                _displayedPlayerHp = Mathf.MoveTowards(_displayedPlayerHp, targetPlayerHp, Time.unscaledDeltaTime * 140f);
            }

            if (_boss != null)
            {
                float targetBossHp = Mathf.Max(0f, _boss.CurrentHealth);
                if (_displayedBossHp < 0f)
                {
                    _displayedBossHp = targetBossHp;
                }
                _displayedBossHp = Mathf.MoveTowards(_displayedBossHp, targetBossHp, Time.unscaledDeltaTime * 650f);
            }

            if (_useCanvasText)
            {
                UpdatePlayerHudText();
                UpdateBossHudText();
            }
        }

        private void TriggerGameOver()
        {
            _isGameOver = true;
            Time.timeScale = 0f;

            if (_gameOverPanel != null)
            {
                _gameOverPanel.SetActive(true);
            }
        }

        private void HandleBossDefeated()
        {
            if (_isGameWon) return;
            _isGameWon = true;
            Time.timeScale = 0f;

            if (_gameWonPanel != null)
            {
                _gameWonPanel.SetActive(true);
            }
        }

        private void UpdatePlayerHudText()
        {
            if (_playerHudText == null || _player == null) return;

            float hp = Mathf.Max(0f, _displayedPlayerHp);
            float maxHp = Mathf.Max(1f, _player.MaxHealth);
            float ratio = Mathf.Clamp01(hp / maxHp);

            int barWidth = 20;
            float totalChars = ratio * barWidth;
            int filled = Mathf.FloorToInt(totalChars);
            float frac = totalChars - filled;
            char transitionChar = frac >= 0.66f ? '#' : (frac >= 0.25f ? '=' : '-');

            string barStr = new string('#', filled);
            if (filled < barWidth)
            {
                barStr += transitionChar;
                barStr += new string('-', barWidth - filled - 1);
            }

            string colorHex = (ratio > 0.5f) ? "#00FFAA" : ((ratio > 0.25f) ? "#FFCC00" : "#FF3344");
            int ammo = _combat != null ? _combat.BodyPartAmmo : 0;

            string content = $"<color={colorHex}><b>HP [{barStr}] {Mathf.CeilToInt(hp)}/{Mathf.CeilToInt(maxHp)}</b></color>\n";

            if (_isGameOver && _gameOverPanel == null)
            {
                content += "\n<color=#FF2222><size=24><b>=== GAME OVER ===</b></size></color>";
            }
            else if (_isGameWon && _gameWonPanel == null)
            {
                content += "\n<color=#22FF88><size=24><b>=== VICTORY - BOSS DEFEATED ===</b></size></color>";
            }

            _playerHudText.text = content;
        }

        private void UpdateBossHudText()
        {
            if (_bossHudText == null) return;

            if (_boss == null)
            {
                if (_bossPanelObj != null && _bossPanelObj.activeSelf)
                {
                    _bossPanelObj.SetActive(false);
                }
                _bossHudText.text = string.Empty;
                return;
            }

            if (_bossPanelObj != null && !_bossPanelObj.activeSelf)
            {
                _bossPanelObj.SetActive(true);
            }

            if (_boss.IsExploding)
            {
                _bossHudText.text ="<color=#FF2200><b>BOSS [ DETONATING... ] 0/4000 HP</b></color>";
                return;
            }

            if (_boss.IsDead)
            {
                _bossHudText.text = "<color=#888888><b>BOSS[DESTROYED]</b></color>";
                return;
            }

            float bossHp = Mathf.Max(0f, _displayedBossHp);
            float bossMaxHp = Mathf.Max(1f, _boss.MaxHealth);
            float bossRatio = Mathf.Clamp01(bossHp / bossMaxHp);

            int bossBarWidth = 24;
            float bTotal = bossRatio * bossBarWidth;
            int bFilled = Mathf.FloorToInt(bTotal);
            float bFrac = bTotal - bFilled;
            char bTransition = bFrac >= 0.66f ? '#' : (bFrac >= 0.25f ? '=' : '-');

            string bossBar = new string('#', bFilled);
            if (bFilled < bossBarWidth)
            {
                bossBar += bTransition;
                bossBar += new string('-', bossBarWidth - bFilled - 1);
            }

            string bossColorHex = _boss.IsEnraged ? "#FF1020" : "#FF2233";

            _bossHudText.text = $"<color={bossColorHex}><b>BOSS [{bossBar}] {Mathf.CeilToInt(bossHp)}/{Mathf.CeilToInt(bossMaxHp)} HP</b></color>";
        }
    }
}
