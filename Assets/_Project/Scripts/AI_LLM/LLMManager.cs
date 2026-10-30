using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Text;
using System; 

[System.Serializable]
public class LLMRequest
{
    public string model = "llama3.2"; 
    public string prompt;
    public bool stream = false;
    public string format = "json"; 
}

[System.Serializable]
public class LLMResponse
{
    public string response;
}

public class LLMManager : MonoBehaviour
{
    private string apiUrl = "http://localhost:11434/api/generate"; 

    
    public void AskLlama(string userMessage, Action<NPCResponseDTO> onResponseCallback)
    {
        StartCoroutine(SendRequestToOllama(userMessage, onResponseCallback));
    }

    private IEnumerator SendRequestToOllama(string userMessage, Action<NPCResponseDTO> onResponseCallback)
    {
       
        string fullPrompt = PromptConfig.GetGuardSystemPrompt() + "\n\nOyuncu: " + userMessage;
        
        LLMRequest requestData = new LLMRequest { prompt = fullPrompt };
        string jsonData = JsonUtility.ToJson(requestData);

        using (UnityWebRequest request = new UnityWebRequest(apiUrl, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            
            if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogWarning("LLM Bağlantı Hatası: Ollama açık mı? " + request.error);
                
                
                NPCResponseDTO fallbackResponse = new NPCResponseDTO 
                { 
                    dialogue = "Kafam karıştı... (Bağlantı koptu)", 
                    action = "WAIT" 
                };
                onResponseCallback?.Invoke(fallbackResponse);
            }
            else
            {
                
                try
                {
                    LLMResponse responseData = JsonUtility.FromJson<LLMResponse>(request.downloadHandler.text);
                    
                    
                    NPCResponseDTO npcResponse = JsonUtility.FromJson<NPCResponseDTO>(responseData.response);
                    
                    onResponseCallback?.Invoke(npcResponse);
                }
                catch (Exception e) 
                {
                    Debug.LogError("JSON Ayrıştırma (Parsing) Hatası: " + e.Message);
                    NPCResponseDTO errorResponse = new NPCResponseDTO 
                    { 
                        dialogue = "Ne dediğini anlayamıyorum gezgin... (Format Hatası)", 
                        action = "WAIT" 
                    };
                    onResponseCallback?.Invoke(errorResponse);
                }
            }
        }
    }
}