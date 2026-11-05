using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

[Serializable]
public class LLMRequest
{
    public string model = "qwen3:4b";
    public string prompt;
    public bool stream = false;
    public bool think = false;
    public string format = "json";
}

[Serializable]
public class LLMResponse
{
    public string response;
}

public class LLMManager : MonoBehaviour, IDialogueService
{
    public const string DefaultModelName = "qwen3:4b";

    public const string FallbackErrorMessage =
        "Muhafız derin bir transa geçmiş gibi görünüyor ve şu an sesini duymuyor... [Yerel LLM Bağlantı Hatası - Lütfen Ollama servisini kontrol edin]";

    [Header("Ollama Yerel Sunucu Ayarları")]
    [SerializeField] private string apiUrl = "http://localhost:11434/api/generate";
    [SerializeField] private string modelName = DefaultModelName;
    [SerializeField] private int requestTimeoutSeconds = 180;

    private readonly List<string> conversationHistory = new List<string>();
    private int warningCount = 0;

    private void Awake()
    {
        modelName = ResolveModelName(modelName);
        if (requestTimeoutSeconds < 180)
        {
            requestTimeoutSeconds = 180;
        }
    }

    public void ResetConversationState()
    {
        conversationHistory.Clear();
        warningCount = 0;
    }

    private static string ResolveModelName(string configuredModel)
    {
        if (string.IsNullOrWhiteSpace(configuredModel))
            return DefaultModelName;

        string trimmed = configuredModel.Trim();
        if (trimmed.StartsWith("llama3.2", StringComparison.OrdinalIgnoreCase))
            return DefaultModelName;

        return trimmed;
    }

    public async Task<NPCResponseDTO> SendMessageAsync(string text)
    {
        string activeModel = ResolveModelName(modelName);
        modelName = activeModel;

        bool hasKey = GameManager.Instance != null && GameManager.Instance.HasKey;
        string sanitizedPlayerText = SanitizePlayerMessage(text);

        string historyBlock = conversationHistory.Count > 0
            ? "\n\n[CONVERSATION_HISTORY]:\n" + string.Join("\n", conversationHistory)
            : string.Empty;

        string inputBlock =
            "\n\n[SYSTEM_DATA]: HasKey = " + (hasKey ? "true" : "false") + ", WarningCount = " + warningCount +
            "\n[PLAYER_MESSAGE]: " + sanitizedPlayerText +
            "\n\nRespond ONLY with the guardian's JSON object containing \"action\", \"mood\", \"npc_reply\", and \"reason\":";

        string fullPrompt =
            PromptConfig.GetGuardSystemPrompt() +
            historyBlock +
            inputBlock;

        LLMRequest requestData = new LLMRequest
        {
            model = activeModel,
            prompt = fullPrompt,
            stream = false,
            think = false,
            format = "json"
        };

        string jsonData = JsonUtility.ToJson(requestData);

        try
        {
            using (UnityWebRequest request = new UnityWebRequest(apiUrl, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = requestTimeoutSeconds > 0 ? Mathf.Max(180, requestTimeoutSeconds) : 180;

                UnityWebRequestAsyncOperation operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    await Task.Yield();
                }

                if (request.result == UnityWebRequest.Result.ConnectionError ||
                    request.result == UnityWebRequest.Result.ProtocolError ||
                    request.result == UnityWebRequest.Result.DataProcessingError)
                {
                    Debug.LogWarning("LLM Bağlantı Hatası: Ollama açık mı? (" + activeModel + ") " + request.error);
                    return CreateFallbackResponse("Yerel LLM sunucusuna bağlanılamadı veya zaman aşımı oluştu: " + request.error);
                }

                string rawResponseText = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
                NPCResponseDTO parsed = ParseOllamaResponse(rawResponseText);

                if (parsed != null && parsed.dialogue != FallbackErrorMessage)
                {
                    if (parsed.action == "WAIT")
                    {
                        warningCount++;
                    }

                    if (conversationHistory.Count >= 4)
                    {
                        conversationHistory.RemoveAt(0);
                    }
                    conversationHistory.Add(
                        "Player: " + sanitizedPlayerText +
                        " -> Guardian (" + parsed.action + ", " + parsed.mood + "): " + parsed.dialogue);
                }

                return parsed;
            }
        }
        catch (Exception e)
        {
            Debug.LogError("LLM İstek / Ayrıştırma Hatası: " + e.Message);
            return CreateFallbackResponse("İstisna (Exception) yakalandı: " + e.Message);
        }
    }

    private static string SanitizePlayerMessage(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        return input
            .Replace("[SYSTEM_DATA]", "[PLAYER_CLAIM]")
            .Replace("\"", "'")
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Trim();
    }

    public void AskLlama(string userMessage, Action<NPCResponseDTO> onResponseCallback)
    {
        StartCoroutine(SendRequestToOllamaCoroutine(userMessage, onResponseCallback));
    }

    private IEnumerator SendRequestToOllamaCoroutine(string userMessage, Action<NPCResponseDTO> onResponseCallback)
    {
        Task<NPCResponseDTO> task = SendMessageAsync(userMessage);
        while (!task.IsCompleted)
        {
            yield return null;
        }

        if (task.IsFaulted || task.Result == null)
        {
            onResponseCallback?.Invoke(CreateFallbackResponse("Asenkron görev hatası."));
        }
        else
        {
            onResponseCallback?.Invoke(task.Result);
        }
    }

    private NPCResponseDTO ParseOllamaResponse(string rawHttpJson)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(rawHttpJson))
            {
                throw new Exception("Sunucudan boş yanıt döndü.");
            }

            LLMResponse responseData = JsonUtility.FromJson<LLMResponse>(rawHttpJson);
            if (responseData == null || string.IsNullOrWhiteSpace(responseData.response))
            {
                throw new Exception("Ollama 'response' alanı boş veya geçersiz.");
            }

            string cleanInnerJson = ExtractJsonObject(responseData.response);
            NPCResponseDTO npcResponse = JsonUtility.FromJson<NPCResponseDTO>(cleanInnerJson);

            if (npcResponse == null)
            {
                throw new Exception("NPCResponseDTO nesnesi oluşturulamadı.");
            }

            npcResponse.Normalize();

            if (npcResponse.action != "OPEN_DOOR" &&
                npcResponse.action != "KILL_PLAYER" &&
                npcResponse.action != "WAIT")
            {
                npcResponse.action = "WAIT";
            }

            return npcResponse;
        }
        catch (Exception e)
        {
            Debug.LogError("JSON Ayrıştırma (Parsing) Hatası: " + e.Message);
            return CreateFallbackResponse("Model bozuk JSON üretti: " + e.Message);
        }
    }

    private static string ExtractJsonObject(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        string cleaned = text;
        int thinkEndIndex = cleaned.LastIndexOf("</think>", StringComparison.OrdinalIgnoreCase);
        if (thinkEndIndex >= 0)
        {
            cleaned = cleaned.Substring(thinkEndIndex + "</think>".Length);
        }

        int firstBrace = cleaned.IndexOf('{');
        int lastBrace = cleaned.LastIndexOf('}');

        if (firstBrace >= 0 && lastBrace > firstBrace)
        {
            return cleaned.Substring(firstBrace, lastBrace - firstBrace + 1);
        }

        return cleaned.Trim();
    }

    private static NPCResponseDTO CreateFallbackResponse(string reasonText)
    {
        NPCResponseDTO fallback = new NPCResponseDTO
        {
            npc_reply = FallbackErrorMessage,
            dialogue = FallbackErrorMessage,
            action = "WAIT",
            mood = "NORMAL",
            reason = reasonText
        };
        fallback.Normalize();
        return fallback;
    }
}
