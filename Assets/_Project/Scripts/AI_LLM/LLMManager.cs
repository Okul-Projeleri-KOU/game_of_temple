using System;
using System.Collections;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

[Serializable]
public class LLMRequest
{
    public string model = "llama3.2";
    public string prompt;
    public bool stream = false;
    public string format = "json";
}

[Serializable]
public class LLMResponse
{
    public string response;
}

public class LLMManager : MonoBehaviour, IDialogueService
{
    public const string FallbackErrorMessage =
        "Muhafız derin bir transa geçmiş gibi görünüyor ve şu an sesini duymuyor... [Yerel LLM Bağlantı Hatası - Lütfen Ollama servisini kontrol edin]";

    [Header("Ollama Yerel Sunucu Ayarları")]
    [SerializeField] private string apiUrl = "http://localhost:11434/api/generate";
    [SerializeField] private string modelName = "llama3.2";
    [SerializeField] private int requestTimeoutSeconds = 15;

    public async Task<NPCResponseDTO> SendMessageAsync(string text)
    {
        string fullPrompt = PromptConfig.GetGuardSystemPrompt() + "\n\nOyuncu: " + text;

        LLMRequest requestData = new LLMRequest
        {
            model = string.IsNullOrWhiteSpace(modelName) ? "llama3.2" : modelName.Trim(),
            prompt = fullPrompt,
            stream = false,
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
                request.timeout = Mathf.Max(5, requestTimeoutSeconds);

                UnityWebRequestAsyncOperation operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    await Task.Yield();
                }

                if (request.result == UnityWebRequest.Result.ConnectionError ||
                    request.result == UnityWebRequest.Result.ProtocolError ||
                    request.result == UnityWebRequest.Result.DataProcessingError)
                {
                    Debug.LogWarning("LLM Bağlantı Hatası: Ollama açık mı? " + request.error);
                    return CreateFallbackResponse("Yerel LLM sunucusuna bağlanılamadı veya zaman aşımı oluştu: " + request.error);
                }

                string rawResponseText = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
                return ParseOllamaResponse(rawResponseText);
            }
        }
        catch (Exception e)
        {
            Debug.LogError("LLM İstek / Ayrıştırma Hatası: " + e.Message);
            return CreateFallbackResponse("İstisna (Exception) yakalandı: " + e.Message);
        }
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

            if (string.IsNullOrWhiteSpace(npcResponse.dialogue))
            {
                throw new Exception("Muhafız yanıt metni (npc_reply / dialogue) boş döndü.");
            }

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

        int firstBrace = text.IndexOf('{');
        int lastBrace = text.LastIndexOf('}');

        if (firstBrace >= 0 && lastBrace > firstBrace)
        {
            return text.Substring(firstBrace, lastBrace - firstBrace + 1);
        }

        return text.Trim();
    }

    private static NPCResponseDTO CreateFallbackResponse(string reasonText)
    {
        NPCResponseDTO fallback = new NPCResponseDTO
        {
            npc_reply = FallbackErrorMessage,
            dialogue = FallbackErrorMessage,
            action = "WAIT",
            reason = reasonText
        };
        fallback.Normalize();
        return fallback;
    }
}
