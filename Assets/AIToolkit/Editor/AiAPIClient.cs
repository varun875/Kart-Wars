// AIApiClient.cs
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using System.Text;
using System.Collections.Generic;

public static class AIApiClient
{
    public static async Task<string> Ask(string prompt)
    {
        var settings = AIToolkitSettings.LoadOrCreate();

        if (settings.selectedProvider == "Local")
            return await AskLocal(prompt, settings);
        else if (settings.selectedProvider == "Gemini")
            return await AskGemini(prompt, settings);
        else
            return await AskOpenAI(prompt, settings);
    }

    // ── OpenAI ──────────────────────────────────────────────
    private static async Task<string> AskOpenAI(string prompt, AIToolkitSettings settings)
    {
        string model = string.IsNullOrEmpty(settings.selectedOpenAIModel)
            ? "gpt-4o" : settings.selectedOpenAIModel;

        string json = JsonUtility.ToJson(new OpenAIRequest
        {
            model = model,
            messages = new[] { new OAIMessage { role = "user", content = prompt } }
        });

        using var req = new UnityWebRequest("https://api.openai.com/v1/chat/completions", "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("Authorization", $"Bearer {settings.openAIKey}");

        await req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
            return $"OpenAI Error: {req.error}\n{req.downloadHandler.text}";

        return ParseOpenAIResponse(req.downloadHandler.text);
    }

    // ── Local (LM Studio / Ollama) ──────────────────────────
    private static async Task<string> AskLocal(string prompt, AIToolkitSettings settings)
    {
        string model = string.IsNullOrEmpty(settings.selectedLocalModel)
            ? "local-model" : settings.selectedLocalModel;

        string json = JsonUtility.ToJson(new OpenAIRequest
        {
            model = model,
            messages = new[] { new OAIMessage { role = "user", content = prompt } },
            temperature = 0.7f // Helps with local models
        });

        // Ensure endpoint ends generally with /chat/completions since both tools are OpenAI-compatible
        string endpoint = settings.localEndpoint;
        if (endpoint.EndsWith("/")) endpoint = endpoint.Substring(0, endpoint.Length - 1);
        
        using var req = new UnityWebRequest($"{endpoint}/chat/completions", "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        await req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
            return $"Local API Error: {req.error}\nHost: {endpoint}\nResponse: {req.downloadHandler.text}";

        // Local models using LM Studio or Ollama respond with an OpenAI-compatible JSON shape
        return ParseOpenAIResponse(req.downloadHandler.text);
    }

    // ── Gemini ──────────────────────────────────────────────
    private static async Task<string> AskGemini(string prompt, AIToolkitSettings settings)
    {
        string model = string.IsNullOrEmpty(settings.selectedGeminiModel)
            ? "gemini-2.0-flash-001" : settings.selectedGeminiModel;

        string url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={settings.geminiKey}";

        string json = JsonUtility.ToJson(new GeminiRequest
        {
            contents = new[]
            {
                new GeminiContent
                {
                    parts = new[] { new GeminiPart { text = prompt } }
                }
            }
        });

        using var req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        await req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
            return $"Gemini Error: {req.error}\n{req.downloadHandler.text}";

        return ParseGeminiResponse(req.downloadHandler.text);
    }

    // ── Fetch Available Models ──────────────────────────────
    public static async Task<List<string>> FetchOpenAIModels(string apiKey)
    {
        var models = new List<string>();
        if (string.IsNullOrEmpty(apiKey)) return models;

        using var req = UnityWebRequest.Get("https://api.openai.com/v1/models");
        req.SetRequestHeader("Authorization", $"Bearer {apiKey}");

        await req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"Failed to fetch OpenAI models: {req.error}");
            return models;
        }

        string response = req.downloadHandler.text;
        // Parse model IDs from the JSON response
        // Response format: { "data": [ { "id": "model-id", ... }, ... ] }
        int searchStart = 0;
        while (true)
        {
            int idIndex = response.IndexOf("\"id\":", searchStart);
            if (idIndex == -1) break;

            int valueStart = response.IndexOf("\"", idIndex + 5) + 1;
            int valueEnd = response.IndexOf("\"", valueStart);
            if (valueStart <= 0 || valueEnd <= 0) break;

            string modelId = response.Substring(valueStart, valueEnd - valueStart);

            // Only include chat-compatible models (gpt, o1, o3, o4, etc.)
            if (modelId.StartsWith("gpt-") || modelId.StartsWith("o1") ||
                modelId.StartsWith("o3") || modelId.StartsWith("o4"))
            {
                if (!models.Contains(modelId))
                    models.Add(modelId);
            }

            searchStart = valueEnd + 1;
        }

        models.Sort();
        return models;
    }

    public static async Task<List<string>> FetchLocalModels(string localEndpoint)
    {
        var models = new List<string>();
        if (string.IsNullOrEmpty(localEndpoint)) return models;

        string endpoint = localEndpoint;
        if (endpoint.EndsWith("/")) endpoint = endpoint.Substring(0, endpoint.Length - 1);

        using var req = UnityWebRequest.Get($"{endpoint}/models");
        
        await req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"Failed to fetch Local models: {req.error}");
            return models;
        }

        string response = req.downloadHandler.text;
        int searchStart = 0;
        while (true)
        {
            int idIndex = response.IndexOf("\"id\":", searchStart);
            if (idIndex == -1) break;

            int valueStart = response.IndexOf("\"", idIndex + 5) + 1;
            int valueEnd = response.IndexOf("\"", valueStart);
            if (valueStart <= 0 || valueEnd <= 0) break;

            string modelId = response.Substring(valueStart, valueEnd - valueStart);
            
            // Allow all local models
            if (!models.Contains(modelId))
                models.Add(modelId);

            searchStart = valueEnd + 1;
        }

        models.Sort();
        return models;
    }

    public static async Task<List<string>> FetchGeminiModels(string apiKey)
    {
        var models = new List<string>();
        if (string.IsNullOrEmpty(apiKey)) return models;

        string url = $"https://generativelanguage.googleapis.com/v1beta/models?key={apiKey}";
        using var req = UnityWebRequest.Get(url);

        await req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"Failed to fetch Gemini models: {req.error}");
            return models;
        }

        string response = req.downloadHandler.text;
        // Parse model names from the JSON response
        // Response format: { "models": [ { "name": "models/gemini-xxx", ... }, ... ] }
        int searchStart = 0;
        while (true)
        {
            int nameIndex = response.IndexOf("\"name\":", searchStart);
            if (nameIndex == -1) break;

            int valueStart = response.IndexOf("\"", nameIndex + 7) + 1;
            int valueEnd = response.IndexOf("\"", valueStart);
            if (valueStart <= 0 || valueEnd <= 0) break;

            string fullName = response.Substring(valueStart, valueEnd - valueStart);

            // Strip "models/" prefix to get just the model ID
            string modelId = fullName.StartsWith("models/")
                ? fullName.Substring(7) : fullName;

            // Only include generateContent-capable models (gemini-*)
            if (modelId.StartsWith("gemini-") && !models.Contains(modelId))
            {
                models.Add(modelId);
            }

            searchStart = valueEnd + 1;
        }

        models.Sort();
        return models;
    }

    // ── Response Parsers ────────────────────────────────────
    private static string ParseOpenAIResponse(string json)
    {
        // Extracts the text content from OpenAI response
        int choicesIndex = json.IndexOf("\"content\":");
        if (choicesIndex == -1) return "Could not parse OpenAI response.";

        int start = json.IndexOf("\"", choicesIndex + 10) + 1;
        int end = json.IndexOf("\"", start);
        return json.Substring(start, end - start);
    }

    private static string ParseGeminiResponse(string json)
    {
        // Extracts the text content from Gemini response
        int textIndex = json.IndexOf("\"text\":");
        if (textIndex == -1) return "Could not parse Gemini response.";

        int start = json.IndexOf("\"", textIndex + 7) + 1;
        int end = json.IndexOf("\"", start);
        return json.Substring(start, end - start);
    }

    // ── OpenAI Data Classes ─────────────────────────────────
    [System.Serializable] class OpenAIRequest { public string model; public OAIMessage[] messages; public float temperature; }
    [System.Serializable] class OAIMessage { public string role; public string content; }

    // ── Gemini Data Classes ─────────────────────────────────
    [System.Serializable] class GeminiRequest { public GeminiContent[] contents; }
    [System.Serializable] class GeminiContent { public GeminiPart[] parts; }
    [System.Serializable] class GeminiPart { public string text; }
}