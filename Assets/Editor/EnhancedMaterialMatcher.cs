using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

public class EnhancedMaterialMatcher : EditorWindow
{
    private GUIStyle infoStyle;
    private GUIStyle buttonStyle;
    [MenuItem("CC美术友好小工具/一键赋予贴图工具（用过的人都说好👍）")]
    static void ShowWindow() => GetWindow<EnhancedMaterialMatcher>("一键赋予贴图工具");

    void OnGUI()
    {
        if (infoStyle == null)
        {
            infoStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 15,  // 调大提示文字字号
                wordWrap = true // 启用自动换行
            };
        }

        if (buttonStyle == null)
        {
            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 20,          // 调大按钮文字字号
                fontStyle = FontStyle.Bold // 加粗按钮文字
            };
        }

        EditorGUILayout.LabelField("选择材质球后点击下方按钮开始匹配\n材质球和贴图命名请一一对应，遵循文档格式\nTex文件夹需要与Materials文件夹平行\n仅支持标准材质球", infoStyle);
        if (GUILayout.Button("开始智能匹配", buttonStyle,GUILayout.Height(40)))
        {
            ProcessMaterials();
        }
    }

    void ProcessMaterials()
    {
      if (Selection.activeObject == null) return;

        var reportData = new List<MaterialReport>();
        int successCount = 0, missingCount = 0, formatErrorCount = 0;

        foreach (Material mat in Selection.GetFiltered<Material>(SelectionMode.Assets))
        {
            var report = new MaterialReport(mat);
            string baseName = ExtractBaseName(mat.name);
            string texFolder = GetTextureFolder(mat);

            report.albedo = FindTexture(texFolder, baseName, new[] { "Albedo", "D", "Diffuse", "BaseColor" }, ref report);
            report.metallic = FindTexture(texFolder, baseName, new[] { "MR", "Metallic", "Roughness", "MS", "MetallicSmoothness" }, ref report);
            report.normal = FindTexture(texFolder, baseName, new[] { "Normal", "N" }, ref report);
            report.ao = FindTexture(texFolder, baseName, new[] { "AO", "Ao", "AmbientOcclusion" }, ref report);
            report.emission = FindTexture(texFolder, baseName, new[] { "Emission", "E", "Emissive" }, ref report);
            ApplyTextures(mat, report);

            if (mat != null) ApplyTextures(mat, report);
            
            if (report.IsSuccess) successCount++;
            else missingCount++;
            
            formatErrorCount += report.formatErrors.Count;
            reportData.Add(report);
        }

        AssetDatabase.SaveAssets();
        EnhancedReportWindow.ShowReport(reportData, successCount, missingCount, formatErrorCount);
    }


    string GetTextureName(Texture tex) => tex ? tex.name : "";

    string ExtractBaseName(string materialName)
    {
        var match = Regex.Match(materialName, @"^(?:M_|Mat_)?(.+?)(?:_\d+)?$");
        return match.Success ? match.Groups[1].Value : materialName;
    }

    string GetTextureFolder(Material mat)
    {
        string path = AssetDatabase.GetAssetPath(mat);
        string parentFolder = Path.GetDirectoryName(Path.GetDirectoryName(path));

        foreach (var folderName in new[] { "Tex", "T", "Textures", "Texture" })
        {
            string fullPath = Path.Combine(parentFolder, folderName);
            if (Directory.Exists(fullPath)) return fullPath;
        }
        return null;
    }

    Texture FindTexture(string folder, string baseName, string[] suffixes, ref MaterialReport report)
    {
        if (!Directory.Exists(folder)) return null;

        foreach (var suffix in suffixes)
        {
            string pattern = $@"({baseName}|{baseName}_?)(_?{suffix})(\.\w+)?$";
            foreach (string file in Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories))
            {
                string fileName = Path.GetFileNameWithoutExtension(file);
                if (Regex.IsMatch(fileName, pattern, RegexOptions.IgnoreCase))
                {
                    var texture = AssetDatabase.LoadAssetAtPath<Texture>(file);
                    CheckTextureFormat(texture, suffix, ref report);
                    return texture;
                }
            }
        }
        return null;
    }

    void CheckTextureFormat(Texture tex, string type, ref MaterialReport report)
    {
        if (tex == null) return;

        string path = AssetDatabase.GetAssetPath(tex);
        string ext = Path.GetExtension(path).ToLower();
        bool isValid = true;

        switch (type.ToLower())
        {
            case "albedo":
            case "d":
            case "diffuse":
            case "basecolor":
                isValid = ext == ".jpg" || ext == ".jpeg" || ext == ".png";
                break;
            case "mr":
            case "metallic":
            case "roughness":
            case "ms":
            case "metallicsmoothness":
                isValid = ext == ".png";
                break;
            case "normal":
            case "n":
            case "ao":
            case "ambientocclusion":
            case "emission":
            case "emissive":
                isValid = ext == ".jpg" || ext == ".jpeg";
                break;
        }

        if (!isValid)
        {
            report.formatErrors.Add($"{type} ({Path.GetFileName(path)})");
        }
    }

    void ApplyTextures(Material mat, MaterialReport report)
    {
        mat.SetColor("_Color", Color.white);

        if (report.albedo)
        {
            mat.SetTexture("_MainTex", report.albedo);
            string albedoPath = AssetDatabase.GetAssetPath(report.albedo);
            if (Path.GetExtension(albedoPath).ToLower() == ".png")
            {
                mat.SetInt("_Mode", 3);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.EnableKeyword("_ALPHATEST_ON");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
        }
        if (report.metallic) mat.SetTexture("_MetallicGlossMap", report.metallic);
        if (report.normal) mat.SetTexture("_BumpMap", report.normal);
        if (report.ao) mat.SetTexture("_OcclusionMap", report.ao);
        if (report.emission) mat.SetTexture("_EmissionMap", report.emission);
    }
}
public class EnhancedReportWindow : EditorWindow
{
    private Vector2 scrollPos;
    private static List<MaterialReport> data;
    private static int totalSuccess, totalMissing, totalFormatErrors;
    private static GUIStyle successStyle, warningStyle, infoStyle, centerStyle;

    public static void ShowReport(List<MaterialReport> reportData, int success, int missing, int formatErrors)
    {
        data = reportData ?? new List<MaterialReport>();
        totalSuccess = success;
        totalMissing = missing;
        totalFormatErrors = formatErrors;
        GetWindow<EnhancedReportWindow>("匹配报告").Show();
    }

    void OnGUI()
    {
        if (data == null) return;

        InitializeStyles();

        EditorGUILayout.LabelField("材质贴图匹配报告", EditorStyles.boldLabel);
        DrawTableHeader();

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
        foreach (var report in data.Where(r => r != null))
        {
            DrawReportRow(report);
        }
        EditorGUILayout.EndScrollView();

        DrawStatistics();
    }

    void InitializeStyles()
    {
        successStyle = new GUIStyle(EditorStyles.label)
        {
            normal = { textColor = Color.green },
            richText = true,
            alignment = TextAnchor.MiddleCenter,
            fixedWidth = 80
        };

        warningStyle = new GUIStyle(EditorStyles.label)
        {
            normal = { textColor = Color.red },
            richText = true,
            alignment = TextAnchor.MiddleCenter,
            fixedWidth = 80
        };

        infoStyle = new GUIStyle(EditorStyles.label)
        {
            normal = { textColor = Color.gray },
            richText = true,
            alignment = TextAnchor.MiddleCenter,
            fixedWidth = 80
        };

        centerStyle = new GUIStyle(EditorStyles.label)
        {
            alignment = TextAnchor.MiddleCenter,
            richText = true
        };
    }

    void DrawTableHeader()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label("材质名称", EditorStyles.boldLabel, GUILayout.Width(150));
            GUILayout.Label("固有色", EditorStyles.boldLabel, GUILayout.Width(80));
            GUILayout.Label("金属/粗糙", EditorStyles.boldLabel, GUILayout.Width(80));
            GUILayout.Label("法线", EditorStyles.boldLabel, GUILayout.Width(80));
            GUILayout.Label("AO", EditorStyles.boldLabel, GUILayout.Width(80));
            GUILayout.Label("（自发光）", EditorStyles.boldLabel, GUILayout.Width(80));
        }
    }

    void DrawReportRow(MaterialReport report)
    {
        if (report?.material == null) return;

        using (new EditorGUILayout.HorizontalScope())
        {
            // 材质名称按钮
            if (GUILayout.Button(report.materialName, EditorStyles.label, GUILayout.Width(150)))
            {
                EditorGUIUtility.PingObject(report.material);
            }

            DrawTextureStatus(report.albedo, report, true);
            DrawTextureStatus(report.metallic, report, true);
            DrawTextureStatus(report.normal, report, true);
            DrawTextureStatus(report.ao, report, true);
            DrawTextureStatus(report.emission, report, false);
        }
    }

    void DrawTextureStatus(Texture tex, MaterialReport report, bool isRequired)
    {
        if (report == null) return;

        bool hasError = report.formatErrors.Any(e => e.Contains(tex?.name ?? ""));
        string status = tex ? $"✔{(hasError ? "<color=yellow>(!)</color>" : "")}" : "×";

        GUIStyle style;
        if (tex != null)
        {
            style = hasError ? successStyle : successStyle; // 保持绿色对号
        }
        else
        {
            style = isRequired ? warningStyle : infoStyle;
        }

        if (GUILayout.Button(status, style))
        {
            if (tex != null) EditorGUIUtility.PingObject(tex);
        }
    }

    void DrawStatistics()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();

            using (new EditorGUILayout.VerticalScope(GUILayout.Width(300)))
            {
                EditorGUILayout.LabelField($"完整匹配: {totalSuccess} 个材质", centerStyle);
                EditorGUILayout.LabelField($"缺失贴图: {totalMissing} 个材质", centerStyle);
                if (totalFormatErrors > 0)
                {
                    EditorGUILayout.LabelField($"格式异常: {totalFormatErrors} 处",
                        new GUIStyle(centerStyle) { normal = { textColor = Color.yellow } });
                }
            }

            GUILayout.FlexibleSpace();
        }
    }
}

public class MaterialReport
{
    public Material material;
    public string materialName;
    public Texture albedo;
    public Texture metallic;
    public Texture normal;
    public Texture ao;
    public Texture emission;
    public List<string> formatErrors = new List<string>();

    public bool IsSuccess => albedo != null && metallic != null && normal != null && ao != null;

    public MaterialReport(Material mat)
    {
        material = mat;
        materialName = mat != null ? mat.name : "Invalid Material";
    }
}