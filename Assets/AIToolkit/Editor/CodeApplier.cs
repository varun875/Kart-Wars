// CodeApplier.cs
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;

public static class CodeApplier
{
    /// <summary>
    /// Extracts all code blocks from a markdown-formatted AI response.
    /// Supports ```csharp, ```cs, ```C#, or bare ``` blocks.
    /// </summary>
    public static List<CodeBlock> ExtractCodeBlocks(string aiResponse)
    {
        var blocks = new List<CodeBlock>();
        if (string.IsNullOrEmpty(aiResponse)) return blocks;

        // Match fenced code blocks: ```lang\n...code...\n```
        var regex = new Regex(@"```(\w*)\s*\n([\s\S]*?)```", RegexOptions.Multiline);
        var matches = regex.Matches(aiResponse);

        foreach (Match match in matches)
        {
            string lang = match.Groups[1].Value.Trim().ToLower();
            string code = match.Groups[2].Value;

            // Remove trailing newline if present
            if (code.EndsWith("\n")) code = code.Substring(0, code.Length - 1);

            blocks.Add(new CodeBlock
            {
                language = lang,
                code = code,
                fullMatch = match.Value
            });
        }

        return blocks;
    }

    /// <summary>
    /// Extracts the first C# code block from an AI response.
    /// </summary>
    public static string ExtractCSharpCode(string aiResponse)
    {
        var blocks = ExtractCodeBlocks(aiResponse);
        foreach (var block in blocks)
        {
            if (block.language == "csharp" || block.language == "cs" ||
                block.language == "c#" || block.language == "")
            {
                return block.code;
            }
        }
        return null;
    }

    /// <summary>
    /// Applies code to an existing file (overwrites content).
    /// Creates a backup first.
    /// </summary>
    public static bool ApplyToFile(string assetPath, string newContent)
    {
        string fullPath = Path.Combine(Application.dataPath, "..", assetPath);
        fullPath = Path.GetFullPath(fullPath);

        if (!File.Exists(fullPath))
        {
            Debug.LogError($"[AI Toolkit] File not found: {fullPath}");
            return false;
        }

        // Create backup
        string backupPath = fullPath + ".bak";
        File.Copy(fullPath, backupPath, true);
        Debug.Log($"[AI Toolkit] Backup created: {backupPath}");

        // Write new content
        File.WriteAllText(fullPath, newContent);
        AssetDatabase.Refresh();
        Debug.Log($"[AI Toolkit] Applied changes to: {assetPath}");
        return true;
    }

    /// <summary>
    /// Creates a new script file in the project.
    /// </summary>
    public static bool CreateFile(string assetPath, string content)
    {
        string fullPath = Path.Combine(Application.dataPath, "..", assetPath);
        fullPath = Path.GetFullPath(fullPath);

        // Ensure directory exists
        string dir = Path.GetDirectoryName(fullPath);
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        if (File.Exists(fullPath))
        {
            Debug.LogWarning($"[AI Toolkit] File already exists: {assetPath}. Use ApplyToFile to overwrite.");
            return false;
        }

        File.WriteAllText(fullPath, content);
        AssetDatabase.Refresh();
        Debug.Log($"[AI Toolkit] Created new file: {assetPath}");
        return true;
    }

    /// <summary>
    /// Restores a file from its .bak backup.
    /// </summary>
    public static bool RevertFile(string assetPath)
    {
        string fullPath = Path.Combine(Application.dataPath, "..", assetPath);
        fullPath = Path.GetFullPath(fullPath);
        string backupPath = fullPath + ".bak";

        if (!File.Exists(backupPath))
        {
            Debug.LogError($"[AI Toolkit] No backup found for: {assetPath}");
            return false;
        }

        File.Copy(backupPath, fullPath, true);
        File.Delete(backupPath);
        AssetDatabase.Refresh();
        Debug.Log($"[AI Toolkit] Reverted: {assetPath}");
        return true;
    }

    /// <summary>
    /// Generates a simple line-by-line diff between old and new content.
    /// Returns a string with +/- prefixed lines.
    /// </summary>
    public static string GenerateDiff(string oldContent, string newContent)
    {
        if (string.IsNullOrEmpty(oldContent) && string.IsNullOrEmpty(newContent))
            return "(both empty)";

        var oldLines = (oldContent ?? "").Split('\n');
        var newLines = (newContent ?? "").Split('\n');

        var diff = new System.Text.StringBuilder();
        int maxLines = Mathf.Max(oldLines.Length, newLines.Length);

        // Simple diff – highlight changed, added, removed lines
        int o = 0, n = 0;
        while (o < oldLines.Length || n < newLines.Length)
        {
            if (o < oldLines.Length && n < newLines.Length)
            {
                string oldLine = oldLines[o].TrimEnd('\r');
                string newLine = newLines[n].TrimEnd('\r');

                if (oldLine == newLine)
                {
                    diff.AppendLine($"  {oldLine}");
                    o++; n++;
                }
                else
                {
                    diff.AppendLine($"- {oldLine}");
                    diff.AppendLine($"+ {newLine}");
                    o++; n++;
                }
            }
            else if (o < oldLines.Length)
            {
                diff.AppendLine($"- {oldLines[o].TrimEnd('\r')}");
                o++;
            }
            else
            {
                diff.AppendLine($"+ {newLines[n].TrimEnd('\r')}");
                n++;
            }
        }

        return diff.ToString();
    }

    /// <summary>
    /// Gets all C# script paths in the project Assets folder.
    /// </summary>
    public static List<string> GetAllProjectScripts()
    {
        var scripts = new List<string>();
        string[] guids = AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets" });

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.EndsWith(".cs"))
                scripts.Add(path);
        }

        scripts.Sort();
        return scripts;
    }

    public struct CodeBlock
    {
        public string language;
        public string code;
        public string fullMatch;
    }
}