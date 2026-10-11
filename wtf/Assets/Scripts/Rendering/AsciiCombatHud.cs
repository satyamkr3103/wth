using UnityEngine;
using AsciiActionGame.Core;
using AsciiActionGame.Player;
using AsciiActionGame.Rendering;

namespace AsciiActionGame.Rendering
{
    public class AsciiCombatHud : MonoBehaviour
    {
        private PlayerController _player;
        private PlayerCombat _combat;

        private void Awake()
        {
            FindPlayer();
        }

        private void OnEnable()
        {
            AsciiGridRenderer.OnPostProcessBuffer += RenderHud;
        }

        private void OnDisable()
        {
            AsciiGridRenderer.OnPostProcessBuffer -= RenderHud;
        }

        private void FindPlayer()
        {
            if (_player == null) _player = FindFirstObjectByType<PlayerController>();
            if (_combat == null && _player != null) _combat = _player.GetComponent<PlayerCombat>();
        }

        private void RenderHud(AsciiCell[,] buffer, int columns, int rows)
        {
            if (_player == null)
            {
                FindPlayer();
                if (_player == null) return;
            }

            float hp = Mathf.Max(0f, _player.CurrentHealth);
            float maxHp = Mathf.Max(1f, _player.MaxHealth);
            float ratio = Mathf.Clamp01(hp / maxHp);

            int barWidth = 16;
            int filledChars = Mathf.RoundToInt(ratio * barWidth);

            Color32 hpCol;
            if (ratio > 0.5f) hpCol = new Color32(0, 255, 200, 255);
            else if (ratio > 0.25f) hpCol = new Color32(255, 190, 40, 255);
            else hpCol = (Mathf.Sin(Time.time * 16f) > 0f) ? new Color32(255, 40, 40, 255) : new Color32(255, 255, 255, 255);

            string hpText = $"HP [{new string('|', filledChars)}{new string('.', barWidth - filledChars)}] {Mathf.CeilToInt(hp)}/{Mathf.CeilToInt(maxHp)}";
            int ammo = _combat != null ? _combat.BodyPartAmmo : 0;
            string partsText = $"  PARTS: [{ammo}] (RMB Throw)";

            string fullHud = hpText + partsText;

            int startX = 2;
            int row = 1;

            if (row < rows)
            {
                for (int i = 0; i < fullHud.Length; i++)
                {
                    int x = startX + i;
                    if (x >= columns) break;

                    char ch = fullHud[i];
                    Color32 col = (i < hpText.Length) ? hpCol : new Color32(0, 220, 255, 255);

                    buffer[x, row].Set(ch, col, new Color32(10, 14, 20, 220), GameConstants.LAYER_UI_OVERLAY);
                }
            }
        }
    }
}
