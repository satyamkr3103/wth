using UnityEngine;
using UnityEngine.SceneManagement;
using AsciiActionGame.Core;
using AsciiActionGame.Rendering;
using AsciiActionGame.Player;

namespace AsciiActionGame.World
{
    [RequireComponent(typeof(BoxCollider2D))]
    public class SceneTransitionTrigger : MonoBehaviour
    {
        [SerializeField] private string _targetSceneName = "BossFight";
        [SerializeField] private Color32 _portalColor = new Color32(0, 240, 255, 255);
        [SerializeField] private Color32 _runeColor = new Color32(180, 255, 255, 255);

        private BoxCollider2D _collider;
        private bool _isTransitioning = false;

        private void Awake()
        {
            _collider = GetComponent<BoxCollider2D>();
            _collider.isTrigger = true;
        }

        private void OnEnable()
        {
            AsciiGridRenderer.OnRenderDynamicEntities += OnRenderPortal;
        }

        private void OnDisable()
        {
            AsciiGridRenderer.OnRenderDynamicEntities -= OnRenderPortal;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_isTransitioning) return;

            var player = other.GetComponent<PlayerController>() ?? other.GetComponentInParent<PlayerController>();
            if (player != null)
            {
                _isTransitioning = true;
                if (AsciiPostProcessing.Instance != null)
                {
                    AsciiPostProcessing.TriggerScreenShake(0.5f);
                }
                SceneManager.LoadScene(_targetSceneName);
            }
        }

        private void OnRenderPortal(IAsciiRenderer renderer)
        {
            Vector2 pos = transform.position;
            float cellW = renderer.CellWidth > 0f ? renderer.CellWidth : 0.25f;
            float cellH = renderer.CellHeight > 0f ? renderer.CellHeight : 0.25f;

            float pulse = Mathf.Sin(Time.time * 4f);
            Color32 glow = pulse > 0f ? _portalColor : _runeColor;

            char coreRune = (Time.time % 0.4f > 0.2f) ? '@' : '*';

            renderer.DrawWorldGlyph(pos + new Vector2(-2 * cellW,  1 * cellH), '/', glow, Color.clear, GameConstants.LAYER_ENVIRONMENT_DECOR);
            renderer.DrawWorldGlyph(pos + new Vector2(-1 * cellW,  1 * cellH), '=', glow, Color.clear, GameConstants.LAYER_ENVIRONMENT_DECOR);
            renderer.DrawWorldGlyph(pos + new Vector2( 0 * cellW,  1 * cellH), '^', Color.white, Color.clear, GameConstants.LAYER_ENVIRONMENT_DECOR);
            renderer.DrawWorldGlyph(pos + new Vector2( 1 * cellW,  1 * cellH), '=', glow, Color.clear, GameConstants.LAYER_ENVIRONMENT_DECOR);
            renderer.DrawWorldGlyph(pos + new Vector2( 2 * cellW,  1 * cellH), '\\', glow, Color.clear, GameConstants.LAYER_ENVIRONMENT_DECOR);

            renderer.DrawWorldGlyph(pos + new Vector2(-2 * cellW,  0 * cellH), '<', glow, Color.clear, GameConstants.LAYER_ENVIRONMENT_DECOR);
            renderer.DrawWorldGlyph(pos + new Vector2(-1 * cellW,  0 * cellH), '[', Color.white, Color.clear, GameConstants.LAYER_ENVIRONMENT_DECOR);
            renderer.DrawWorldGlyph(pos + new Vector2( 0 * cellW,  0 * cellH), coreRune, Color.white, Color.clear, GameConstants.LAYER_ENVIRONMENT_DECOR);
            renderer.DrawWorldGlyph(pos + new Vector2( 1 * cellW,  0 * cellH), ']', Color.white, Color.clear, GameConstants.LAYER_ENVIRONMENT_DECOR);
            renderer.DrawWorldGlyph(pos + new Vector2( 2 * cellW,  0 * cellH), '>', glow, Color.clear, GameConstants.LAYER_ENVIRONMENT_DECOR);

            renderer.DrawWorldGlyph(pos + new Vector2(-2 * cellW, -1 * cellH), '\\', glow, Color.clear, GameConstants.LAYER_ENVIRONMENT_DECOR);
            renderer.DrawWorldGlyph(pos + new Vector2(-1 * cellW, -1 * cellH), '=', glow, Color.clear, GameConstants.LAYER_ENVIRONMENT_DECOR);
            renderer.DrawWorldGlyph(pos + new Vector2( 0 * cellW, -1 * cellH), 'v', Color.white, Color.clear, GameConstants.LAYER_ENVIRONMENT_DECOR);
            renderer.DrawWorldGlyph(pos + new Vector2( 1 * cellW, -1 * cellH), '=', glow, Color.clear, GameConstants.LAYER_ENVIRONMENT_DECOR);
            renderer.DrawWorldGlyph(pos + new Vector2( 2 * cellW, -1 * cellH), '/', glow, Color.clear, GameConstants.LAYER_ENVIRONMENT_DECOR);

            string label = "BOSS PORTAL";
            int halfLen = label.Length / 2;
            for (int i = 0; i < label.Length; i++)
            {
                renderer.DrawWorldGlyph(pos + new Vector2((i - halfLen) * cellW, 2 * cellH), label[i], new Color32(255, 230, 80, 255), Color.clear, GameConstants.LAYER_UI_OVERLAY);
            }
        }
    }
}
