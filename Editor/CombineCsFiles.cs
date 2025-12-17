#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

using UnityEditor;

using UnityEngine;

public class CombineCsFiles : EditorWindow
{
    DefaultAsset targetFolder;
    bool recurse = true;
    bool generateTypeIndex = true;
    string status = "Idle";

    const int MAX_LINES_PER_PART = 12000;
    static readonly UTF8Encoding UTF8NoBom = new(false);

    [MenuItem("Tools/ExoLabs/Combine C# Files…")]
    static void ShowWindow() => GetWindow<CombineCsFiles>("Combine C# Files");

    void OnGUI()
    {
        GUILayout.Label("Source Folder", EditorStyles.boldLabel);
        targetFolder = (DefaultAsset)EditorGUILayout.ObjectField("Folder", targetFolder, typeof(DefaultAsset), false);
        recurse = EditorGUILayout.Toggle("Recurse sub‑folders", recurse);
        generateTypeIndex = EditorGUILayout.Toggle("Generate type index", generateTypeIndex);
        using (new EditorGUI.DisabledScope(targetFolder == null))
        {
            if (GUILayout.Button("Combine & Save")) Combine();
        }
        GUILayout.Space(4);
        GUILayout.Label($"Status: {status}", EditorStyles.helpBox);
    }

    void Combine()
    {
        try
        {
            string assetPath = AssetDatabase.GetAssetPath(targetFolder);
            if (!AssetDatabase.IsValidFolder(assetPath))
            {
                status = "Invalid folder";
                Repaint();
                return;
            }

            status = "Scanning…";
            Repaint();

            string[] guids = AssetDatabase.FindAssets("t:TextAsset", new[] { assetPath });
            var csFiles = guids.Select(AssetDatabase.GUIDToAssetPath)
                               .Where(p => p.EndsWith(".cs"))
                               .Where(p => recurse || Path.GetDirectoryName(p).Replace('\\', '/') == assetPath)
                               .OrderBy(p => p, StringComparer.Ordinal)
                               .ToArray();
            if (csFiles.Length == 0)
            {
                status = "No .cs files found";
                Repaint();
                return;
            }

            status = $"Combining {csFiles.Length} files…";
            Repaint();

            var buffers = new Dictionary<string, StringBuilder>
            {
                ["main"] = new StringBuilder(4096),
                ["test"] = new StringBuilder(2048),
                ["editor"] = new StringBuilder(2048)
            };
            var lineCounts = new Dictionary<string, int>
            {
                ["main"] = 0,
                ["test"] = 0,
                ["editor"] = 0
            };
            var partNum = new Dictionary<string, int>
            {
                ["main"] = 1,
                ["test"] = 1,
                ["editor"] = 1
            };

            string folderName = Path.GetFileName(assetPath.TrimEnd('/', '\\'));
            string combinedRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "combined-code"));
            Directory.CreateDirectory(combinedRoot);

            foreach (string old in Directory.EnumerateFiles(combinedRoot, $"{folderName}-*.cs"))
                File.Delete(old);
            foreach (string old in Directory.EnumerateFiles(combinedRoot, $"{folderName}-*.json"))
                File.Delete(old);

            var typeMap = new Dictionary<string, string>(StringComparer.Ordinal);
            var includesMap = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var createdFiles = new List<string>();

            try
            {
                for (int i = 0; i < csFiles.Length; i++)
                {
                    string file = csFiles[i];
                    string category = Categorise(file);
                    string relAsset = file.StartsWith("Assets/") ? file.Substring("Assets/".Length) : file;
                    string combinedKey = GetCombinedFileName(folderName, category, partNum[category]);

                    string contents = File.ReadAllText(file);
                    int lines = CountLines(contents);
                    string md5 = QuickMD5(contents);

                    if (generateTypeIndex)
                    {
                        string codeNoComments = Regex.Replace(contents, @"//.*?$|/\*.*?\*/", string.Empty, RegexOptions.Singleline | RegexOptions.Multiline);
                        foreach (Match m in Regex.Matches(codeNoComments, @"\b(class|struct|interface|enum)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)"))
                        {
                            string t = m.Groups["name"].Value;
                            if (!char.IsUpper(t[0])) continue;
                            if (!typeMap.ContainsKey(t)) typeMap[t] = relAsset;
                        }
                    }

                    if (!includesMap.TryGetValue(combinedKey, out var list))
                    {
                        list = new List<string>();
                        includesMap[combinedKey] = list;
                    }
                    list.Add(relAsset);

                    if (lineCounts[category] + lines > MAX_LINES_PER_PART && lineCounts[category] > 0)
                    {
                        string flushed = WriteBuffer(buffers[category], combinedRoot, folderName, category, partNum[category]);
                        if (!string.IsNullOrEmpty(flushed))
                        {
                            createdFiles.Add(flushed);
                            WriteIncludesJson(flushed, includesMap[Path.GetFileName(flushed)]);
                            createdFiles.Add(Path.ChangeExtension(flushed, ".json"));
                        }
                        buffers[category].Clear();
                        lineCounts[category] = 0;
                        partNum[category]++;
                        combinedKey = GetCombinedFileName(folderName, category, partNum[category]);
                    }

                    var sb = buffers[category];
                    sb.AppendLine("// ───────────────────────────────────────────────────────────────");
                    sb.AppendLine($"//  FILE : {relAsset}");
                    sb.AppendLine($"//  LINES: {lines}    MD5: {md5}");
                    sb.AppendLine("// ───────────────────────────────────────────────────────────────");
                    sb.AppendLine(contents);
                    sb.AppendLine();

                    lineCounts[category] += lines;

                    EditorUtility.DisplayProgressBar("Combining C# Files", relAsset, (float)(i + 1) / csFiles.Length);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            foreach (var kv in buffers)
            {
                string flushed = WriteBuffer(kv.Value, combinedRoot, folderName, kv.Key, partNum[kv.Key]);
                if (!string.IsNullOrEmpty(flushed))
                {
                    createdFiles.Add(flushed);
                    WriteIncludesJson(flushed, includesMap[Path.GetFileName(flushed)]);
                    createdFiles.Add(Path.ChangeExtension(flushed, ".json"));
                }
            }

            if (generateTypeIndex && typeMap.Count > 0)
            {
                var sb = new StringBuilder(2048);
                sb.AppendLine("{");
                sb.AppendLine($"  \"generated\": \"{DateTime.UtcNow:O}\",");
                sb.AppendLine("  \"types\": {");
                int idx = 0;
                foreach (var kv in typeMap.OrderBy(k => k.Key, StringComparer.Ordinal))
                {
                    sb.Append($"    \"{kv.Key}\": \"{kv.Value}\"");
                    sb.AppendLine(idx == typeMap.Count - 1 ? string.Empty : ",");
                    idx++;
                }
                sb.AppendLine("  }");
                sb.AppendLine("}");
                string tiPath = Path.Combine(combinedRoot, $"{folderName}-types.json");
                File.WriteAllText(tiPath, sb.ToString(), UTF8NoBom);
                createdFiles.Add(tiPath);
            }

            status = "Zipping…";
            Repaint();
            string zipPath = Path.Combine(combinedRoot, $"{folderName}-upload.zip");
            if (File.Exists(zipPath)) File.Delete(zipPath);

            using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                foreach (string f in createdFiles.Distinct())
                {
                    zip.CreateEntryFromFile(f, Path.GetFileName(f), System.IO.Compression.CompressionLevel.Optimal);
                }
            }

            status = "Done";
            Repaint();
            EditorUtility.RevealInFinder(zipPath);
        }
        catch (Exception ex)
        {
            status = "Error: " + ex.Message;
            Repaint();
            Debug.LogException(ex);
        }
    }

    static string Categorise(string path)
    {
        string p = path.Replace('\\', '/').ToLowerInvariant();
        if (Regex.IsMatch(p, @"/tests?/")) return "test";
        if (p.Contains("/editor/")) return "editor";
        return "main";
    }

    static int CountLines(string s) => s.Count(c => c == '\n') + 1;

    static string QuickMD5(string input)
    {
        using var md5 = MD5.Create();
        byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
        return BitConverter.ToString(hash).Replace("-", "").Substring(0, 8);
    }

    static string GetCombinedFileName(string folder, string category, int part)
    {
        string suffix = part > 1 ? $"{category}-{part}" : category;
        return $"{folder}-{suffix}.cs";
    }

    static string WriteBuffer(StringBuilder sb, string root, string folder, string category, int part)
    {
        if (sb.Length == 0) return string.Empty;
        string path = Path.Combine(root, GetCombinedFileName(folder, category, part));
        File.WriteAllText(path, sb.ToString(), UTF8NoBom);
        Debug.Log($"Wrote {path}");
        return path;
    }

    static void WriteIncludesJson(string combinedFilePath, List<string> includes)
    {
        if (includes == null || includes.Count == 0) return;
        var sb = new StringBuilder(1024);
        sb.AppendLine("{");
        sb.AppendLine($"  \"combined\": \"{Path.GetFileName(combinedFilePath)}\",");
        sb.AppendLine($"  \"generated\": \"{DateTime.UtcNow:O}\",");
        sb.AppendLine("  \"includes\": [");
        for (int i = 0; i < includes.Count; i++)
        {
            sb.Append($"    \"{includes[i]}\"");
            sb.AppendLine(i == includes.Count - 1 ? string.Empty : ",");
        }
        sb.AppendLine("  ]");
        sb.AppendLine("}");
        string jsonPath = Path.ChangeExtension(combinedFilePath, ".json");
        File.WriteAllText(jsonPath, sb.ToString(), UTF8NoBom);
    }
}
#endif
