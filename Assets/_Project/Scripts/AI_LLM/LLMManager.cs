using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Text;


[System.Serializable]
public class LLMRequest
{
    public string model = "llama3.2"; 
    public string prompt;
    public bool stream = false;
}


[System.Serializable]
public class LLMResponse
{
    public string response;
}

public class LLMManager : MonoBehaviour
{
    
    private string apiUrl = "http://localhost:11434/api/generate"; 

    public void AskLlama(string userPrompt, System.Action<string> onResponseCallback)
    {
        StartCoroutine(SendRequestToOllama(userPrompt, onResponseCallback));
    }

    private IEnumerator SendRequestToOllama(string prompt, System.Action<string> onResponseCallback)
    {
        LLMRequest requestData = new LLMRequest { prompt = prompt };
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
                Debug.LogError("Llama 3.2 Bağlantı Hatası: " + request.error);
                onResponseCallback?.Invoke("Bağlantı hatası.");
            }
            else
            {
                LLMResponse responseData = JsonUtility.FromJson<LLMResponse>(request.downloadHandler.text);
                onResponseCallback?.Invoke(responseData.response);
            }
        }
    }
}