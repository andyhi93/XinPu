using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

/// <summary>
/// 場景裡所有 Button 底下的標籤目前是 Legacy Text（用 Anton.ttf，沒有中文字），
/// 在 WebGL 沒有系統字型可以動態補字，所以按鈕文字整個消失。
/// 這個工具把它們批次換成 TextMeshProUGUI，並套用專案裡的中文字體。
/// </summary>
public static class ButtonLegacyTextConverter
{
    // 預設套用 GenSenRounded2-R（專案裡最常用的中文字體）。要換成 acgyosyo 就改這裡的搜尋字串。
    private const string TargetFontName = "GenSenRounded2-R SDF";

    [MenuItem("Tools/把按鈕的 Legacy Text 換成 TextMeshPro")]
    public static void ConvertButtonLabels()
    {
        TMP_FontAsset targetFont = FindFontAsset(TargetFontName);
        if (targetFont == null)
        {
            Debug.LogError($"找不到字體資源：{TargetFontName}，請確認名稱或手動修改腳本裡的 TargetFontName。");
            return;
        }

        Button[] buttons = Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int converted = 0;

        foreach (var button in buttons)
        {
            Text legacyText = button.GetComponentInChildren<Text>(true);
            if (legacyText == null) continue;

            GameObject go = legacyText.gameObject;
            string content = legacyText.text;
            Color color = legacyText.color;
            TextAnchor anchor = legacyText.alignment;
            FontStyle style = legacyText.fontStyle;
            float fontSize = legacyText.fontSize;
            bool raycastTarget = legacyText.raycastTarget;

            Undo.DestroyObjectImmediate(legacyText);
            TextMeshProUGUI tmp = Undo.AddComponent<TextMeshProUGUI>(go);
            tmp.text = content;
            tmp.color = color;
            tmp.font = targetFont;
            tmp.fontSize = fontSize;
            tmp.raycastTarget = raycastTarget;
            tmp.alignment = ConvertAlignment(anchor);
            tmp.fontStyle = ConvertStyle(style);

            converted++;
            Debug.Log($"已轉換：{GetPath(go.transform)}");
        }

        Debug.Log($"【按鈕字體轉換】共轉換 {converted} 個按鈕標籤，套用字體：{TargetFontName}。請檢查排版後存檔。");
    }

    private static TMP_FontAsset FindFontAsset(string name)
    {
        string[] guids = AssetDatabase.FindAssets($"{name} t:TMP_FontAsset");
        if (guids.Length == 0) return null;
        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
    }

    private static TextAlignmentOptions ConvertAlignment(TextAnchor anchor)
    {
        switch (anchor)
        {
            case TextAnchor.UpperLeft: return TextAlignmentOptions.TopLeft;
            case TextAnchor.UpperCenter: return TextAlignmentOptions.Top;
            case TextAnchor.UpperRight: return TextAlignmentOptions.TopRight;
            case TextAnchor.MiddleLeft: return TextAlignmentOptions.Left;
            case TextAnchor.MiddleCenter: return TextAlignmentOptions.Center;
            case TextAnchor.MiddleRight: return TextAlignmentOptions.Right;
            case TextAnchor.LowerLeft: return TextAlignmentOptions.BottomLeft;
            case TextAnchor.LowerCenter: return TextAlignmentOptions.Bottom;
            case TextAnchor.LowerRight: return TextAlignmentOptions.BottomRight;
            default: return TextAlignmentOptions.Center;
        }
    }

    private static FontStyles ConvertStyle(FontStyle style)
    {
        switch (style)
        {
            case FontStyle.Bold: return FontStyles.Bold;
            case FontStyle.Italic: return FontStyles.Italic;
            case FontStyle.BoldAndItalic: return FontStyles.Bold | FontStyles.Italic;
            default: return FontStyles.Normal;
        }
    }

    private static string GetPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }
}
