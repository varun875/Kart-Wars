// AIToolkitSettings.cs
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public class AIToolkitSettings : ScriptableObject
{
    public string openAIKey;
    public string anthropicKey;
    public string geminiKey;
    public string localEndpoint = "http://localhost:1234/v1"; // LM Studio default
    public string selectedProvider = "OpenAI";
    public string selectedOpenAIModel = "";
    public string selectedGeminiModel = "";
    public string selectedLocalModel = "";

    // Cached model lists (not serialized – fetched at runtime)
    [System.NonSerialized] public List<string> cachedOpenAIModels = new List<string>();
    [System.NonSerialized] public List<string> cachedGeminiModels = new List<string>();
    [System.NonSerialized] public List<string> cachedLocalModels = new List<string>();
    [System.NonSerialized] public bool isFetchingOpenAI = false;
    [System.NonSerialized] public bool isFetchingGemini = false;
    [System.NonSerialized] public bool isFetchingLocal = false;

    [SettingsProvider]
    public static SettingsProvider CreateSettingsProvider()
    {
        return new SettingsProvider("Project/AI Toolkit", SettingsScope.Project)
        {
            guiHandler = (searchCtx) =>
            {
                var settings = LoadOrCreate();

                // ── API Keys & Endpoints ──────────────────────────────
                GUILayout.Label("API Keys & Endpoints", EditorStyles.boldLabel);
                settings.openAIKey = EditorGUILayout.TextField("OpenAI API Key", settings.openAIKey);
                settings.anthropicKey = EditorGUILayout.TextField("Anthropic API Key", settings.anthropicKey);
                settings.geminiKey = EditorGUILayout.TextField("Gemini API Key", settings.geminiKey);
                GUILayout.Space(5);
                settings.localEndpoint = EditorGUILayout.TextField(
                    new GUIContent("Local API Endpoint", "Ollama: http://localhost:11434/v1\nLM Studio: http://localhost:1234/v1"),
                    settings.localEndpoint);

                GUILayout.Space(10);

                // ── Provider Selection ──────────────────────────────
                GUILayout.Label("Provider", EditorStyles.boldLabel);
                int current = 0;
                if (settings.selectedProvider == "Gemini") current = 1;
                else if (settings.selectedProvider == "Local") current = 2;

                int selected = EditorGUILayout.Popup("Active Provider", current, new[] { "OpenAI", "Gemini", "Local" });
                if (selected == 0) settings.selectedProvider = "OpenAI";
                else if (selected == 1) settings.selectedProvider = "Gemini";
                else if (selected == 2) settings.selectedProvider = "Local";

                GUILayout.Space(10);

                // ── Model Selection ─────────────────────────────────
                GUILayout.Label("Model Selection", EditorStyles.boldLabel);

                // OpenAI
                EditorGUILayout.BeginHorizontal();
                if (settings.cachedOpenAIModels.Count > 0)
                {
                    int idx = Mathf.Max(0, settings.cachedOpenAIModels.IndexOf(settings.selectedOpenAIModel));
                    idx = EditorGUILayout.Popup("OpenAI Model", idx, settings.cachedOpenAIModels.ToArray());
                    settings.selectedOpenAIModel = settings.cachedOpenAIModels[idx];
                }
                else EditorGUILayout.TextField("OpenAI Model", settings.selectedOpenAIModel);

                if (GUILayout.Button(settings.isFetchingOpenAI ? "Fetching..." : "Refresh", GUILayout.Width(80)))
                    if (!settings.isFetchingOpenAI) FetchOpenAIModelsAsync(settings);
                EditorGUILayout.EndHorizontal();

                // Gemini
                EditorGUILayout.BeginHorizontal();
                if (settings.cachedGeminiModels.Count > 0)
                {
                    int idx = Mathf.Max(0, settings.cachedGeminiModels.IndexOf(settings.selectedGeminiModel));
                    idx = EditorGUILayout.Popup("Gemini Model", idx, settings.cachedGeminiModels.ToArray());
                    settings.selectedGeminiModel = settings.cachedGeminiModels[idx];
                }
                else EditorGUILayout.TextField("Gemini Model", settings.selectedGeminiModel);

                if (GUILayout.Button(settings.isFetchingGemini ? "Fetching..." : "Refresh", GUILayout.Width(80)))
                    if (!settings.isFetchingGemini) FetchGeminiModelsAsync(settings);
                EditorGUILayout.EndHorizontal();

                // Local
                EditorGUILayout.BeginHorizontal();
                if (settings.cachedLocalModels.Count > 0)
                {
                    int idx = Mathf.Max(0, settings.cachedLocalModels.IndexOf(settings.selectedLocalModel));
                    idx = EditorGUILayout.Popup("Local Model", idx, settings.cachedLocalModels.ToArray());
                    settings.selectedLocalModel = settings.cachedLocalModels[idx];
                }
                else EditorGUILayout.TextField("Local Model", settings.selectedLocalModel);

                if (GUILayout.Button(settings.isFetchingLocal ? "Fetching..." : "Refresh", GUILayout.Width(80)))
                    if (!settings.isFetchingLocal) FetchLocalModelsAsync(settings);
                EditorGUILayout.EndHorizontal();

                GUILayout.Space(5);
                EditorGUILayout.HelpBox(
                    "Click 'Refresh' to fetch available models from the API. " +
                    "Make sure your API keys or local endpoint are correct.",
                    MessageType.Info);

                EditorUtility.SetDirty(settings);
            }
        };
    }

    // ── Async Model Fetchers ────────────────────────────────
    private static async void FetchOpenAIModelsAsync(AIToolkitSettings settings)
    {
        settings.isFetchingOpenAI = true;
        try {
            var models = await AIApiClient.FetchOpenAIModels(settings.openAIKey);
            settings.cachedOpenAIModels = models;
            if (models.Count > 0 && (string.IsNullOrEmpty(settings.selectedOpenAIModel) || !models.Contains(settings.selectedOpenAIModel)))
                settings.selectedOpenAIModel = models.Find(m => m.Contains("gpt-5")) ?? models.Find(m => m.Contains("gpt-4")) ?? models[0];
        } catch {}
        finally { settings.isFetchingOpenAI = false; EditorUtility.SetDirty(settings); }
    }

    private static async void FetchGeminiModelsAsync(AIToolkitSettings settings)
    {
        settings.isFetchingGemini = true;
        try {
            var models = await AIApiClient.FetchGeminiModels(settings.geminiKey);
            settings.cachedGeminiModels = models;
            if (models.Count > 0 && (string.IsNullOrEmpty(settings.selectedGeminiModel) || !models.Contains(settings.selectedGeminiModel)))
                settings.selectedGeminiModel = models.Find(m => m.Contains("gemini-3.1-pro")) ?? models.Find(m => m.Contains("gemini-3-flash")) ?? models[0];
        } catch {}
        finally { settings.isFetchingGemini = false; EditorUtility.SetDirty(settings); }
    }

    private static async void FetchLocalModelsAsync(AIToolkitSettings settings)
    {
        settings.isFetchingLocal = true;
        try {
            var models = await AIApiClient.FetchLocalModels(settings.localEndpoint);
            settings.cachedLocalModels = models;
            if (models.Count > 0 && (string.IsNullOrEmpty(settings.selectedLocalModel) || !models.Contains(settings.selectedLocalModel)))
                settings.selectedLocalModel = models[0]; // Pick first available local model
            else if (models.Count == 0) Debug.LogWarning("[AI Toolkit] No local models returned. Check if Ollama/LM Studio is running.");
        } catch {}
        finally { settings.isFetchingLocal = false; EditorUtility.SetDirty(settings); }
    }

    // ── Load / Create ───────────────────────────────────────
    public static AIToolkitSettings LoadOrCreate()
    {
        var settings = AssetDatabase.LoadAssetAtPath<AIToolkitSettings>("Assets/AIToolkit/Editor/Settings.asset");
        if (settings == null)
        {
            settings = CreateInstance<AIToolkitSettings>();
            AssetDatabase.CreateAsset(settings, "Assets/AIToolkit/Editor/Settings.asset");
            AssetDatabase.SaveAssets();
        }
        return settings;
    }
}