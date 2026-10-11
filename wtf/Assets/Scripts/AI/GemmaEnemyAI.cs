using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using AsciiActionGame.Enemies;
using AsciiActionGame.Player;

namespace AsciiActionGame.AI
{
    public class GemmaEnemyAI : MonoBehaviour, IEnemyDecisionMaker
    {
        [Header("Google AI Studio Configuration")]
        [SerializeField] private bool _useGoogleAiStudio = true;
        [SerializeField] private string _apiKey = "";
        [SerializeField] private string _modelName = "gemini-2.0-flash";
        [SerializeField] private float _queryInterval = 1.6f;
        [SerializeField] private float _timeoutSeconds = 2.5f;

        [Header("Local Ollama Server Fallback")]
        [SerializeField] private string _localServerUrl = "http://localhost:11434/api/generate";

        [Header("Offline Emulation")]
        [SerializeField] private bool _enableOfflineEmulationFallback = true;

        public string DecisionMakerName => "Gemma Tactical Combat AI";
        public bool IsReady => true;

        private EnemyEntity _enemy;
        private ScriptedEnemyAI _fallbackAI;
        private AIDecisionResult _currentDecision;
        private float _lastQueryTime = -999f;
        private bool _isQueryInProgress = false;
        private int _incomingThreatCounter = 0;

        public event Action<string> OnGemmaDialogueReceived;

        private void Awake()
        {
            _fallbackAI = new ScriptedEnemyAI();
            _currentDecision = AIDecisionResult.Default;

            LoadApiKey();
        }

        private void LoadApiKey()
        {
            if (!string.IsNullOrEmpty(_apiKey) && !_apiKey.StartsWith("PASTE_")) return;

            string[] candidateFilenames = new string[]
            {
                "google_ai_key.txt",
                "api_key.txt",
                "gemma_api_key.txt",
                "gemma_api_key.json"
            };

            string[] searchDirs = new string[]
            {
                Directory.GetCurrentDirectory(),
                Application.dataPath,
                Path.Combine(Application.dataPath, ".."),
                Path.Combine(Application.dataPath, "Config")
            };

            foreach (string dir in searchDirs)
            {
                if (!Directory.Exists(dir)) continue;

                foreach (string file in candidateFilenames)
                {
                    string fullPath = Path.Combine(dir, file);
                    if (File.Exists(fullPath))
                    {
                        try
                        {
                            string content = File.ReadAllText(fullPath).Trim();
                            if (string.IsNullOrEmpty(content)) continue;

                            if (content.Contains("{") && content.Contains("}"))
                            {
                                string extracted = ExtractJsonValue(content, "api_key");
                                if (string.IsNullOrEmpty(extracted))
                                {
                                    extracted = ExtractJsonValue(content, "apiKey");
                                }
                                if (!string.IsNullOrEmpty(extracted) && !extracted.StartsWith("PASTE_"))
                                {
                                    _apiKey = extracted;
                                    return;
                                }
                            }
                            else
                            {
                                string[] lines = content.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                                foreach (string line in lines)
                                {
                                    string trimmed = line.Trim();
                                    if (trimmed.StartsWith("#") || trimmed.StartsWith("//")) continue;
                                    if (!string.IsNullOrEmpty(trimmed) && !trimmed.StartsWith("PASTE_"))
                                    {
                                        _apiKey = trimmed;
                                        return;
                                    }
                                }
                            }
                        }
                        catch
                        {
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(_apiKey) || _apiKey.StartsWith("PASTE_"))
            {
                string envKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
                if (string.IsNullOrEmpty(envKey))
                {
                    envKey = Environment.GetEnvironmentVariable("GOOGLE_API_KEY");
                }
                if (!string.IsNullOrEmpty(envKey))
                {
                    _apiKey = envKey;
                }
            }
        }

        public void Initialize(EnemyEntity enemy)
        {
            _enemy = enemy;
            _fallbackAI.Initialize(enemy);
        }

        public void Tick(float deltaTime)
        {
            _fallbackAI.Tick(deltaTime);

            CheckRealtimeAttackPrediction();

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
            if (_currentDecision.action == AIActionType.DodgeDash || _currentDecision.action == AIActionType.TeleportBehind)
            {
                scriptedResult.action = _currentDecision.action;
            }

            return scriptedResult;
        }

        private void CheckRealtimeAttackPrediction()
        {
            if (_enemy == null || _enemy.IsDead) return;

            var player = FindFirstObjectByType<PlayerController>();
            if (player == null || player.IsDead) return;

            var playerCombat = player.GetComponent<PlayerCombat>();
            Vector2 playerPos = player.Position;
            Vector2 aimDir = player.AimDirection.normalized;
            Vector2 toEnemy = _enemy.Position - playerPos;
            float dist = toEnemy.magnitude;

            if (dist > 18f) return;

            float alignment = Vector2.Dot(aimDir, toEnemy.normalized);
            bool isAimedAt = alignment > 0.93f;
            bool isFiring = playerCombat != null && playerCombat.ActiveWeapon != null && playerCombat.ActiveWeapon.IsFiring;

            if (isAimedAt)
            {
                if (isFiring || _incomingThreatCounter >= 2)
                {
                    ExecuteTacticalEmergencyEvasion(player, aimDir);
                }
                else
                {
                    _incomingThreatCounter = Mathf.Min(3, _incomingThreatCounter + 1);
                }
            }
            else
            {
                _incomingThreatCounter = Mathf.Max(0, _incomingThreatCounter - 1);
            }
        }

        private void ExecuteTacticalEmergencyEvasion(PlayerController player, Vector2 aimDir)
        {
            if (_enemy == null || _enemy.IsDead) return;

            if (_enemy.CanTeleport && (_currentDecision.action == AIActionType.TeleportBehind || UnityEngine.Random.value < 0.35f))
            {
                _enemy.PerformTeleportBehind(player.transform);
                _incomingThreatCounter = 0;
                return;
            }

            if (_enemy.CanDash)
            {
                Vector2 perp = new Vector2(-aimDir.y, aimDir.x);
                Vector2 toEnemy = _enemy.Position - player.Position;
                float dotSide = Vector2.Dot(perp, toEnemy);
                Vector2 dodgeDir = (dotSide >= 0f ? perp : -perp).normalized;

                _enemy.PerformDodgeDash(dodgeDir);
                _incomingThreatCounter = 0;
            }
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

            var player = FindFirstObjectByType<PlayerController>();
            float playerDist = player != null ? Vector2.Distance(_enemy.Position, player.Position) : 10f;
            Vector2 playerAim = player != null ? player.AimDirection : Vector2.right;
            float hp = _enemy.CurrentHealth;

            string jsonState = $"{{\"hp\":{Mathf.RoundToInt(hp)},\"player_dist\":{playerDist:F1},\"aim_x\":{playerAim.x:F2},\"aim_y\":{playerAim.y:F2},\"can_dash\":{(_enemy.CanDash ? "true" : "false")},\"can_teleport\":{(_enemy.CanTeleport ? "true" : "false")},\"intact_limbs\":[\"{string.Join("\",\"", limbNames)}\"]}}";

            string promptText = $"You are a predictive tactical combat AI for an agile enemy mech in a fast-paced game. Game state: {jsonState}. Predict incoming player attack and choose survival maneuver. Respond ONLY with valid raw JSON: {{\"action\":\"DODGE_DASH\"|\"TELEPORT_BEHIND\"|\"SACRIFICE_LIMB\"|\"STRAFE\"|\"ADVANCE\"|\"RETREAT\",\"target_limb\":\"LeftArm\"|\"RightArm\"|\"ChestArmor\"|\"None\"}}";

            if (_useGoogleAiStudio && !string.IsNullOrEmpty(_apiKey))
            {
                string url = $"https://generativelanguage.googleapis.com/v1beta/models/{_modelName}:generateContent?key={_apiKey}";
                string studioPayload = $"{{\"contents\":[{{\"parts\":[{{\"text\":\"{promptText.Replace("\"", "\\\"")}\"}}]}}],\"generationConfig\":{{\"temperature\":0.2,\"maxOutputTokens\":120}}}}";

                using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
                {
                    byte[] bodyRaw = Encoding.UTF8.GetBytes(studioPayload);
                    request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Content-Type", "application/json");
                    request.timeout = Mathf.CeilToInt(_timeoutSeconds);

                    yield return request.SendWebRequest();

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        ParseGoogleStudioResponse(request.downloadHandler.text);
                        _isQueryInProgress = false;
                        yield break;
                    }
                }
            }

            if (!string.IsNullOrEmpty(_localServerUrl))
            {
                string ollamaPayload = $"{{\"model\":\"{_modelName}\",\"prompt\":\"{promptText.Replace("\"", "\\\"")}\",\"stream\":false,\"format\":\"json\"}}";
                using (UnityWebRequest request = new UnityWebRequest(_localServerUrl, "POST"))
                {
                    byte[] bodyRaw = Encoding.UTF8.GetBytes(ollamaPayload);
                    request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Content-Type", "application/json");
                    request.timeout = Mathf.CeilToInt(_timeoutSeconds);

                    yield return request.SendWebRequest();

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        ParseOllamaResponse(request.downloadHandler.text);
                        _isQueryInProgress = false;
                        yield break;
                    }
                }
            }

            if (_enableOfflineEmulationFallback)
            {
                GenerateEmulatedGemmaDecision(hp, playerDist, limbNames, playerAim);
            }

            _isQueryInProgress = false;
        }

        private void ParseGoogleStudioResponse(string rawResponse)
        {
            try
            {
                string textContent = rawResponse;
                int textIdx = rawResponse.IndexOf("\"text\":");
                if (textIdx != -1)
                {
                    int startQuote = rawResponse.IndexOf("\"", textIdx + 7);
                    int endQuote = rawResponse.IndexOf("\"", startQuote + 1);
                    if (startQuote != -1 && endQuote > startQuote)
                    {
                        textContent = rawResponse.Substring(startQuote + 1, endQuote - startQuote - 1).Replace("\\\"", "\"").Replace("\\n", "");
                    }
                }

                ApplyParsedAction(textContent);
            }
            catch
            {
            }
        }

        private void ParseOllamaResponse(string rawResponse)
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

                ApplyParsedAction(jsonToParse);
            }
            catch
            {
            }
        }

        private void ApplyParsedAction(string json)
        {
            OnGemmaDialogueReceived?.Invoke(json);

            if (json.Contains("TELEPORT_BEHIND"))
            {
                _currentDecision.action = AIActionType.TeleportBehind;
                _currentDecision.should_sacrifice = false;
            }
            else if (json.Contains("DODGE_DASH"))
            {
                _currentDecision.action = AIActionType.DodgeDash;
                _currentDecision.should_sacrifice = false;
            }
            else if (json.Contains("SACRIFICE_LIMB"))
            {
                _currentDecision.action = AIActionType.SacrificeLimb;
                _currentDecision.should_sacrifice = true;
                _currentDecision.target_limb = ExtractJsonValue(json, "target_limb");
            }
            else if (json.Contains("RETREAT"))
            {
                _currentDecision.action = AIActionType.Retreat;
                _currentDecision.should_sacrifice = false;
            }
            else if (json.Contains("ADVANCE"))
            {
                _currentDecision.action = AIActionType.Advance;
                _currentDecision.should_sacrifice = false;
            }
            else
            {
                _currentDecision.action = AIActionType.Strafe;
                _currentDecision.should_sacrifice = false;
            }
        }

        private void GenerateEmulatedGemmaDecision(float hp, float dist, string[] limbs, Vector2 playerAim)
        {
            if (_enemy == null || _enemy.IsDead) return;

            if (_enemy.CanTeleport && (dist < 5.0f || hp < 120f) && UnityEngine.Random.value < 0.45f)
            {
                _currentDecision.action = AIActionType.TeleportBehind;
                _currentDecision.should_sacrifice = false;
            }
            else if (_enemy.CanDash && dist < 12.0f && UnityEngine.Random.value < 0.60f)
            {
                _currentDecision.action = AIActionType.DodgeDash;
                _currentDecision.should_sacrifice = false;
            }
            else if (hp <= 90f && limbs.Length > 1 && dist <= 7.5f)
            {
                string chosenLimb = limbs[0] == "Core" ? (limbs.Length > 1 ? limbs[1] : "LeftArm") : limbs[0];
                _currentDecision.action = AIActionType.SacrificeLimb;
                _currentDecision.should_sacrifice = true;
                _currentDecision.target_limb = chosenLimb;
            }
            else
            {
                _currentDecision.action = dist < 3.5f ? AIActionType.Retreat : AIActionType.Strafe;
                _currentDecision.should_sacrifice = false;
            }
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
