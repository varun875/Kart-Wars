// AIToolkitWindow.cs
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;

public class AIToolkitWindow : EditorWindow
{
    // ── Mode ────────────────────────────────────────────────
    private enum Mode { Chat, EditCode, GenerateCode }
    private Mode currentMode = Mode.Chat;
    private readonly string[] modeLabels = { "💬 Chat", "✏️ Edit Code", "🆕 Generate Code" };

    // ── Chat State ──────────────────────────────────────────
    private string prompt = "";
    private string response = "";
    private bool isLoading = false;

    // ── Edit Code State ─────────────────────────────────────
    private List<string> projectScripts = new List<string>();
    private int selectedScriptIdx = 0;
    private string originalCode = "";
    private string proposedCode = "";
    private string diffPreview = "";
    private bool hasProposal = false;
    private bool showDiff = true;

    // ── Generate Code State ─────────────────────────────────
    private string newFileName = "NewScript.cs";
    private string newFilePath = "Assets/Scripts/";
    private string generatedCode = "";
    private bool hasGenerated = false;

    // ── Scroll Positions ────────────────────────────────────
    private Vector2 scrollResponse;
    private Vector2 scrollDiff;
    private Vector2 scrollGenerated;
    private Vector2 scrollPrompt;

    // ── GUI Styles ──────────────────────────────────────────
    private bool stylesInitialized = false;
    private GUIStyle headerStyle;
    private GUIStyle containerBox;
    private GUIStyle codeBox;
    private GUIStyle modernButton;
    private GUIStyle actionButton;
    private GUIStyle rejectButton;

    [MenuItem("Tools/AI Toolkit %#a")] // Ctrl+Shift+A shortcut
    public static void ShowWindow()
    {
        var window = GetWindow<AIToolkitWindow>("AI Toolkit ✨");
        window.minSize = new Vector2(550, 450);
    }

    private void OnEnable()
    {
        RefreshScriptList();
    }

    private void RefreshScriptList()
    {
        projectScripts = CodeApplier.GetAllProjectScripts();
    }

    private void InitStyles()
    {
        if (stylesInitialized) return;

        headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 18,
            alignment = TextAnchor.MiddleLeft,
            margin = new RectOffset(10, 10, 10, 10)
        };

        containerBox = new GUIStyle(EditorStyles.helpBox)
        {
            padding = new RectOffset(12, 12, 12, 12),
            margin = new RectOffset(10, 10, 5, 5)
        };

        codeBox = new GUIStyle(EditorStyles.textArea)
        {
            font = Font.CreateDynamicFontFromOSFont("Consolas", 13),
            wordWrap = true,
            richText = true,
            padding = new RectOffset(10, 10, 10, 10)
        };

        modernButton = new GUIStyle(GUI.skin.button)
        {
            fontSize = 13,
            padding = new RectOffset(10, 10, 8, 8),
            margin = new RectOffset(2, 2, 2, 2)
        };

        actionButton = new GUIStyle(modernButton) { fontStyle = FontStyle.Bold };
        rejectButton = new GUIStyle(modernButton) { fontStyle = FontStyle.Bold };

        stylesInitialized = true;
    }

    private void OnGUI()
    {
        InitStyles();
        var settings = AIToolkitSettings.LoadOrCreate();

        DrawHeader(settings);
        
        // Separator line
        Rect sep = EditorGUILayout.GetControlRect(false, 1);
        EditorGUI.DrawRect(sep, new Color(0.5f, 0.5f, 0.5f, 0.3f));
        GUILayout.Space(10);

        DrawModeSelector();

        GUILayout.Space(10);

        switch (currentMode)
        {
            case Mode.Chat:        DrawChatMode(settings); break;
            case Mode.EditCode:    DrawEditMode(settings); break;
            case Mode.GenerateCode: DrawGenerateMode(settings); break;
        }
    }

    // ── Header ──────────────────────────────────────────────
    private void DrawHeader(AIToolkitSettings settings)
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("AI Toolkit ✨", headerStyle);
        GUILayout.FlexibleSpace();

        EditorGUILayout.BeginVertical(GUILayout.ExpandHeight(true));
        GUILayout.Space(15);
        string model = "Unknown";
        if (settings.selectedProvider == "Local")
            model = string.IsNullOrEmpty(settings.selectedLocalModel) ? "No Model" : settings.selectedLocalModel;
        else if (settings.selectedProvider == "Gemini")
            model = string.IsNullOrEmpty(settings.selectedGeminiModel) ? "Gemini" : settings.selectedGeminiModel;
        else
            model = string.IsNullOrEmpty(settings.selectedOpenAIModel) ? "OpenAI" : settings.selectedOpenAIModel;

        GUILayout.Label($"<b>{settings.selectedProvider}</b> : {model}", new GUIStyle(EditorStyles.label) { richText = true, fontSize = 11, alignment = TextAnchor.MiddleRight });
        EditorGUILayout.EndVertical();

        GUILayout.Space(10);
        EditorGUILayout.BeginVertical();
        GUILayout.Space(12);
        if (GUILayout.Button("⚙ Settings", modernButton, GUILayout.Width(90)))
            SettingsService.OpenProjectSettings("Project/AI Toolkit");
        EditorGUILayout.EndVertical();
        GUILayout.Space(10);

        EditorGUILayout.EndHorizontal();
    }

    // ── Mode Selector ───────────────────────────────────────
    private void DrawModeSelector()
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(10);
        for (int i = 0; i < modeLabels.Length; i++)
        {
            bool active = (int)currentMode == i;
            if (active) GUI.backgroundColor = new Color(0.2f, 0.6f, 1f, 1f);
            
            if (GUILayout.Button(modeLabels[i], active ? actionButton : modernButton, GUILayout.Height(35)))
            {
                currentMode = (Mode)i;
                if (currentMode == Mode.EditCode) RefreshScriptList();
                GUI.FocusControl(null);
            }
            if (active) GUI.backgroundColor = Color.white;
        }
        GUILayout.Space(10);
        EditorGUILayout.EndHorizontal();
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // MODE 1: CHAT
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    private void DrawChatMode(AIToolkitSettings settings)
    {
        EditorGUILayout.BeginVertical(containerBox);
        GUILayout.Label("Prompt:", EditorStyles.boldLabel);
        
        scrollPrompt = EditorGUILayout.BeginScrollView(scrollPrompt, GUILayout.Height(80));
        var promptStyle = new GUIStyle(EditorStyles.textArea) { wordWrap = true, fontSize = 13, padding = new RectOffset(6,6,6,6) };
        prompt = EditorGUILayout.TextArea(prompt, promptStyle, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();

        GUILayout.Space(8);
        
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        EditorGUI.BeginDisabledGroup(isLoading || string.IsNullOrWhiteSpace(prompt));
        GUI.backgroundColor = new Color(0.2f, 0.6f, 1f, 1f);
        if (GUILayout.Button(isLoading ? "⌛ Thinking..." : "▶ Send Message", actionButton, GUILayout.Width(150), GUILayout.Height(35)))
            SendChat();
        GUI.backgroundColor = Color.white;
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        GUILayout.Space(5);

        if (!string.IsNullOrEmpty(response))
        {
            EditorGUILayout.BeginVertical(containerBox);
            GUILayout.Label("Response:", EditorStyles.boldLabel);
            
            scrollResponse = EditorGUILayout.BeginScrollView(scrollResponse, GUILayout.ExpandHeight(true));
            EditorGUILayout.SelectableLabel(response, codeBox, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
            EditorGUILayout.EndScrollView();

            GUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("📋 Copy", modernButton, GUILayout.Width(80)))
                EditorGUIUtility.systemCopyBuffer = response;
            if (GUILayout.Button("🗑 Clear", modernButton, GUILayout.Width(80)))
            { prompt = ""; response = ""; }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // MODE 2: EDIT CODE
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    private void DrawEditMode(AIToolkitSettings settings)
    {
        EditorGUILayout.BeginVertical(containerBox);
        
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Target Script:", EditorStyles.boldLabel, GUILayout.Width(90));
        
        if (projectScripts.Count > 0)
        {
            selectedScriptIdx = Mathf.Clamp(selectedScriptIdx, 0, projectScripts.Count - 1);
            selectedScriptIdx = EditorGUILayout.Popup(selectedScriptIdx, projectScripts.ToArray(), GUILayout.Height(22));
        }
        else
        {
            GUILayout.Label("(no scripts found)", EditorStyles.miniLabel);
        }
        
        if (GUILayout.Button("↻", GUILayout.Width(30), GUILayout.Height(22)))
            RefreshScriptList();
        EditorGUILayout.EndHorizontal();

        // Load selected file preview count
        if (projectScripts.Count > 0)
        {
            try {
                string selectedPath = projectScripts[selectedScriptIdx];
                string fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", selectedPath));
                if (File.Exists(fullPath))
                {
                    int lineCount = File.ReadAllText(fullPath).Split('\n').Length;
                    GUILayout.Space(2);
                    GUILayout.Label($"File: {Path.GetFileName(selectedPath)} | Lines: {lineCount}", EditorStyles.miniLabel);
                }
            } catch {}
        }

        GUILayout.Space(10);
        GUILayout.Label("Instruction (what should the AI change?):", EditorStyles.boldLabel);
        scrollPrompt = EditorGUILayout.BeginScrollView(scrollPrompt, GUILayout.Height(65));
        var promptStyle = new GUIStyle(EditorStyles.textArea) { wordWrap = true, fontSize = 13, padding = new RectOffset(6,6,6,6) };
        prompt = EditorGUILayout.TextArea(prompt, promptStyle, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();

        GUILayout.Space(8);
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        EditorGUI.BeginDisabledGroup(isLoading || string.IsNullOrWhiteSpace(prompt) || projectScripts.Count == 0);
        GUI.backgroundColor = new Color(0.2f, 0.6f, 1f, 1f);
        if (GUILayout.Button(isLoading ? "⌛ AI is editing..." : "✏️ Request Edit", actionButton, GUILayout.Width(150), GUILayout.Height(35)))
            SendEditRequest();
        GUI.backgroundColor = Color.white;
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.EndVertical();

        // Show diff / proposal
        if (hasProposal)
        {
            GUILayout.Space(5);
            EditorGUILayout.BeginVertical(containerBox);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Preview Changes", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            showDiff = GUILayout.Toggle(showDiff, showDiff ? "👀 Show diff" : "📄 Show full code", "Button", GUILayout.Width(130));
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(5);

            scrollDiff = EditorGUILayout.BeginScrollView(scrollDiff, GUILayout.ExpandHeight(true));
            string displayText = showDiff ? diffPreview : proposedCode;
            EditorGUILayout.SelectableLabel(displayText, codeBox, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
            EditorGUILayout.EndScrollView();
            GUILayout.Space(10);

            // Action buttons
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.1f, 0.7f, 0.2f);
            if (GUILayout.Button("✅ Apply To File", actionButton, GUILayout.Height(35)))
            {
                string path = projectScripts[selectedScriptIdx];
                if (CodeApplier.ApplyToFile(path, proposedCode))
                {
                    EditorUtility.DisplayDialog("AI Toolkit", $"Changes applied to {Path.GetFileName(path)}!\nBackup saved as .bak", "Awesome!");
                    hasProposal = false;
                }
            }
            GUI.backgroundColor = new Color(0.9f, 0.2f, 0.2f);
            if (GUILayout.Button("❌ Reject", rejectButton, GUILayout.Width(90), GUILayout.Height(35)))
            {
                hasProposal = false;
                proposedCode = "";
                diffPreview = "";
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }
        else if (!string.IsNullOrEmpty(response) && !isLoading)
        {
            GUILayout.Space(5);
            EditorGUILayout.BeginVertical(containerBox);
            GUILayout.Label("AI Output:", EditorStyles.boldLabel);
            scrollResponse = EditorGUILayout.BeginScrollView(scrollResponse, GUILayout.ExpandHeight(true));
            EditorGUILayout.SelectableLabel(response, codeBox, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        DrawRevertBackupButton();
    }

    private void DrawRevertBackupButton()
    {
        if (projectScripts.Count == 0) return;
        string currentPath = projectScripts[selectedScriptIdx];
        string bakPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", currentPath)) + ".bak";
        if (File.Exists(bakPath))
        {
            GUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUI.backgroundColor = new Color(0.9f, 0.6f, 0.1f);
            if (GUILayout.Button("⏪ Revert Last Edit (From Backup)", modernButton, GUILayout.Width(250), GUILayout.Height(28)))
            {
                if (EditorUtility.DisplayDialog("Revert?", "Are you sure you want to revert to the backup version of this script?", "Yes, Revert", "Cancel"))
                {
                    CodeApplier.RevertFile(currentPath);
                    hasProposal = false;
                }
            }
            GUI.backgroundColor = Color.white;
            GUILayout.Space(10);
            EditorGUILayout.EndHorizontal();
        }
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // MODE 3: GENERATE CODE
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    private void DrawGenerateMode(AIToolkitSettings settings)
    {
        EditorGUILayout.BeginVertical(containerBox);
        
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Directory:", EditorStyles.boldLabel, GUILayout.Width(75));
        newFilePath = EditorGUILayout.TextField(newFilePath);
        GUILayout.Label("File:", EditorStyles.boldLabel, GUILayout.Width(35));
        newFileName = EditorGUILayout.TextField(newFileName);
        EditorGUILayout.EndHorizontal();
        
        GUILayout.Space(10);
        
        GUILayout.Label("Description of the new script:", EditorStyles.boldLabel);
        scrollPrompt = EditorGUILayout.BeginScrollView(scrollPrompt, GUILayout.Height(70));
        var promptStyle = new GUIStyle(EditorStyles.textArea) { wordWrap = true, fontSize = 13, padding = new RectOffset(6,6,6,6) };
        prompt = EditorGUILayout.TextArea(prompt, promptStyle, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();

        GUILayout.Space(8);
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        EditorGUI.BeginDisabledGroup(isLoading || string.IsNullOrWhiteSpace(prompt));
        GUI.backgroundColor = new Color(0.2f, 0.6f, 1f, 1f);
        if (GUILayout.Button(isLoading ? "⌛ Generating..." : "🆕 Create Script", actionButton, GUILayout.Width(150), GUILayout.Height(35)))
            SendGenerateRequest();
        GUI.backgroundColor = Color.white;
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.EndVertical();

        if (hasGenerated && !string.IsNullOrEmpty(generatedCode))
        {
            GUILayout.Space(5);
            EditorGUILayout.BeginVertical(containerBox);
            GUILayout.Label("Generated Code Preview", EditorStyles.boldLabel);

            scrollGenerated = EditorGUILayout.BeginScrollView(scrollGenerated, GUILayout.ExpandHeight(true));
            EditorGUILayout.SelectableLabel(generatedCode, codeBox, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
            EditorGUILayout.EndScrollView();
            GUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.1f, 0.7f, 0.2f);
            if (GUILayout.Button("💾 Save To Project", actionButton, GUILayout.Height(35)))
            {
                string savePath = Path.Combine(newFilePath, newFileName);
                if (CodeApplier.CreateFile(savePath, generatedCode))
                {
                    EditorUtility.DisplayDialog("AI Toolkit", $"Script created successfully at:\n{savePath}", "OK");
                    hasGenerated = false;
                    RefreshScriptList();
                }
                else
                {
                    if (EditorUtility.DisplayDialog("File Exists", $"{savePath} already exists. Overwrite?", "Overwrite", "Cancel"))
                    {
                        CodeApplier.ApplyToFile(savePath, generatedCode);
                        hasGenerated = false;
                        RefreshScriptList();
                    }
                }
            }
            GUI.backgroundColor = new Color(0.9f, 0.2f, 0.2f);
            if (GUILayout.Button("❌ Discard", rejectButton, GUILayout.Width(90), GUILayout.Height(35)))
            {
                hasGenerated = false;
                generatedCode = "";
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
        }
        else if (!string.IsNullOrEmpty(response) && !isLoading && !hasGenerated)
        {
            GUILayout.Space(5);
            EditorGUILayout.BeginVertical(containerBox);
            GUILayout.Label("AI Output:", EditorStyles.boldLabel);
            scrollResponse = EditorGUILayout.BeginScrollView(scrollResponse, GUILayout.ExpandHeight(true));
            EditorGUILayout.SelectableLabel(response, codeBox, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // ASYNC SENDERS
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    private async void SendChat()
    {
        isLoading = true;
        response = "";
        Repaint();

        try { response = await AIApiClient.Ask(prompt); }
        catch (System.Exception e) { response = $"Error: {e.Message}"; }

        isLoading = false;
        Repaint();
    }

    private async void SendEditRequest()
    {
        if (projectScripts.Count == 0) return;

        isLoading = true;
        response = "";
        hasProposal = false;
        Repaint();

        try
        {
            string path = projectScripts[selectedScriptIdx];
            string fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));
            originalCode = File.ReadAllText(fullPath);

            string fullPrompt =
                "You are a Unity C# code editor. The user will give you a C# script and an instruction.\n" +
                "Return ONLY the complete modified script inside a single ```csharp code block.\n" +
                "Do NOT add any explanation outside the code block.\n" +
                "Do NOT omit any part of the file — return the FULL file.\n\n" +
                $"=== FILE: {Path.GetFileName(path)} ===\n```csharp\n{originalCode}\n```\n\n" +
                $"=== INSTRUCTION ===\n{prompt}";

            response = await AIApiClient.Ask(fullPrompt);

            proposedCode = CodeApplier.ExtractCSharpCode(response);
            if (!string.IsNullOrEmpty(proposedCode))
            {
                diffPreview = CodeApplier.GenerateDiff(originalCode, proposedCode);
                hasProposal = true;
            }
            else response = "⚠️ Could not extract code from AI response.\n\nRaw:\n" + response;
        }
        catch (System.Exception e) { response = $"Error: {e.Message}"; }

        isLoading = false;
        Repaint();
    }

    private async void SendGenerateRequest()
    {
        isLoading = true;
        response = "";
        hasGenerated = false;
        Repaint();

        try
        {
            string fullPrompt =
                "You are a Unity C# script generator.\n" +
                "Generate a complete, ready-to-use Unity C# script based on the user's description.\n" +
                "Return ONLY the complete script inside a single ```csharp code block.\n" +
                "Do NOT add any explanation outside the code block.\n" +
                "Include proper using statements, namespace (if needed), and comments.\n\n" +
                $"=== DESCRIPTION ===\n{prompt}\n\n" +
                $"=== FILENAME ===\n{newFileName}";

            response = await AIApiClient.Ask(fullPrompt);

            generatedCode = CodeApplier.ExtractCSharpCode(response);
            if (!string.IsNullOrEmpty(generatedCode)) hasGenerated = true;
            else response = "⚠️ Could not extract code from AI response.\n\nRaw:\n" + response;
        }
        catch (System.Exception e) { response = $"Error: {e.Message}"; }

        isLoading = false;
        Repaint();
    }
}