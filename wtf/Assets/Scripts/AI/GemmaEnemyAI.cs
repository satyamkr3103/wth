using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using AsciiActionGame.Enemies;

namespace AsciiActionGame.AI
{
    public class GemmaEnemyAI : MonoBehaviour, IEnemyDecisionMaker
    {
        [Header("Gemma Server Configuration")]
        [SerializeField] private string _serverUrl = "http://localhost:11434/api/generate";
        [SerializeField] private string _modelName = "gemma2:2b";
        [SerializeField] private float _queryInterval = 2.0f;
        [SerializeField] private float _timeoutSeconds = 2.0f;

        [Header("Offline Emulation")]
        [Tooltip("If true, simulates Gemma 2 tactical JSON output when the local Ollama server is offline.")]
        [SerializeField] private bool _enableOfflineEmulationFallback = true;

        public string DecisionMakerName => "Gemma 2 LLM Strategic Agent";
        public bool IsReady => true;

        private EnemyEntity _enemy;
        private ScriptedEnemyAI _fallbackAI;
        private AIDecisionResult _currentDecision;
        private float _lastQueryTime = -999f;
        private bool _isQueryInProgress = false;

        public event Action<string> OnGemmaDialogueReceived;

        private void Awake()
        {
            _fallbackAI = new ScriptedEnemyAI();
            _currentDecision = AIDecisionResult.Default;
        }

        public void Initialize(EnemyEntity enemy)
        {
            _enemy = enemy;
            _fallbackAI.Initialize(enemy);
        }

        public void Tick(float deltaTime)
        {
            _fallbackAI.Tick(deltaTime);

            if (_enemy != null && !_enemy.IsDead && !_isQueryInProgress && Time.time >= _lastQueryTime + _queryInterval)
            {
                StartCoroutine(QueryGemmaLLM());
            }
        }

        public AIDecisionResult MakeDecision(EnemyGameState context)
        {
            if (_currentDecision.should_sacrifice)
            {
                return _currentDecision;
            }

            AIDecisionResult scriptedResult = _fallbackAI.MakeDecision(context);
            if (!string.IsNullOrEmpty(_currentDecision.dialogue))
            {
                scriptedResult.dialogue = _currentDecision.dialogue;
            }
            if (!string.IsNullOrEmpty(_currentDecision.target_limb))
            {
                scriptedResult.target_limb = _currentDecision.target_limb;
            }

            return scriptedResult;
        }

        private IEnumerator QueryGemmaLLM()
        {
            _isQueryInProgress = true;
            _lastQueryTime = Time.time;

            if (_enemy == null || _enemy.IsDead)
            {
                _isQueryInProgress = false;
                yield break;
            }

            var activeLimbs = _enemy.GetActiveBodyParts();
            string[] limbNames = new string[activeLimbs.Count];
            for (int i = 0; i < activeLimbs.Count; i++)
            {
                limbNames[i] = activeLimbs[i].PartType.ToString();
            }

            var player = FindFirstObjectByType<Player.PlayerController>();
            float playerDist = player != null ? Vector2.Distance(_enemy.Position, player.Position) : 10f;
            float hp = _enemy.CorePart != null && _enemy.CorePart.Damageable != null ? _enemy.CorePart.Damageable.CurrentHealth : 100f;

            string jsonState = $"{{\"enemy_health\":{Mathf.RoundToInt(hp)},\"player_dist\":{playerDist:F1},\"player_weapon\":\"EraserBeam\",\"intact_limbs\":[\"{string.Join("\",\"", limbNames)}\"]}}";

            string prompt = $"You are a tactical combat AI for a cyber drone in a top-down game. Given game state: {jsonState}. Respond ONLY with raw valid JSON in this format: {{\"action\":\"SACRIFICE_LIMB\"|\"STRAFE\"|\"ADVANCE\"|\"RETREAT\",\"target_limb\":\"LeftArm\"|\"RightArm\"|\"ChestArmor\"|\"None\",\"attack_type\":\"HIGH_SPREAD_SHRAPNEL\"|\"DIRECT\",\"dialogue\":\"short 1-line cyber message\"}}";

            string ollamaPayload = $"{{\"model\":\"{_modelName}\",\"prompt\":\"{prompt.Replace("\"", "\\\"")}\",\"stream\":false,\"format\":\"json\"}}";

            using (UnityWebRequest request = new UnityWebRequest(_serverUrl, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(ollamaPayload);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = Mathf.CeilToInt(_timeoutSeconds);

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    ParseGemmaResponse(request.downloadHandler.text);
                }
                else
                {
                    if (_enableOfflineEmulationFallback)
                    {
                        GenerateEmulatedGemmaDecision(hp, playerDist, limbNames);
                    }
                }
            }

            _isQueryInProgress = false;
        }

        private void ParseGemmaResponse(string rawResponse)
        {
            try
            {
                string jsonToParse = rawResponse;
                int responseIdx = rawResponse.IndexOf("\"response\":");
                if (responseIdx != -1)
                {
                    int startQuote = rawResponse.IndexOf("\"", responseIdx + 11);
                    int endQuote = rawResponse.LastIndexOf("\"");
                    if (startQuote != -1 && endQuote > startQuote)
                    {
                        jsonToParse = rawResponse.Substring(startQuote + 1, endQuote - startQuote - 1).Replace("\\\"", "\"").Replace("\\n", "");
                    }
                }

                if (jsonToParse.Contains("SACRIFICE_LIMB"))
                {
                    _currentDecision.action = AIActionType.SacrificeLimb;
                    _currentDecision.should_sacrifice = true;
                    _currentDecision.target_limb = ExtractJsonValue(jsonToParse, "target_limb");
                    _currentDecision.dialogue = ExtractJsonValue(jsonToParse, "dialogue");
                }
                else if (jsonToParse.Contains("RETREAT"))
                {
                    _currentDecision.action = AIActionType.Retreat;
                    _currentDecision.should_sacrifice = false;
                    _currentDecision.dialogue = ExtractJsonValue(jsonToParse, "dialogue");
                }
                else
                {
                    _currentDecision.action = AIActionType.Strafe;
                    _currentDecision.should_sacrifice = false;
                    _currentDecision.dialogue = ExtractJsonValue(jsonToParse, "dialogue");
                }

                if (!string.IsNullOrEmpty(_currentDecision.dialogue))
                {
                    OnGemmaDialogueReceived?.Invoke(_currentDecision.dialogue);
                    Debug.Log($"[Gemma 2 LLM] {_currentDecision.dialogue}");
                }
            }
            catch
            {
            }
        }

        private void GenerateEmulatedGemmaDecision(float hp, float dist, string[] limbs)
        {
            if (hp <= 50f && limbs.Length > 1 && dist <= 8.5f)
            {
                string chosenLimb = limbs[0] == "Core" ? (limbs.Length > 1 ? limbs[1] : "LeftArm") : limbs[0];
                _currentDecision.action = AIActionType.SacrificeLimb;
                _currentDecision.should_sacrifice = true;
                _currentDecision.target_limb = chosenLimb;
                _currentDecision.dialogue = $"[GEMMA-AI]: CORE AT {hp:F0}% COMPROMISED. SACRIFICING {chosenLimb.ToUpper()} AS SHRAPNEL CLUSTER.";
            }
            else
            {
                _currentDecision.action = dist < 3f ? AIActionType.Retreat : AIActionType.Strafe;
                _currentDecision.should_sacrifice = false;
                _currentDecision.dialogue = $"[GEMMA-AI]: TARGET DISTANCE {dist:F1}M. EXECUTING ORBITAL SUPPRESSION.";
            }

            OnGemmaDialogueReceived?.Invoke(_currentDecision.dialogue);
        }

        private string ExtractJsonValue(string json, string key)
        {
            string pattern = $"\"{key}\":\"";
            int idx = json.IndexOf(pattern);
            if (idx == -1) return "";

            int start = idx + pattern.Length;
            int end = json.IndexOf("\"", start);
            if (end == -1) return "";

            return json.Substring(start, end - start);
        }
    }
}
