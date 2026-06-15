using UnityEngine;
using UnityEngine.UI;
using Yarn.Unity;
using System.Collections.Generic;

/// <summary>
/// 背景管理器：負責切換場景背景圖。
/// </summary>
public class BackgroundManager : MonoBehaviour
{
    public static BackgroundManager Instance { get; private set; }

    [Header("UI 元件")]
    public Image bgImage;

    [System.Serializable]
    public struct BackgroundEntry
    {
        public string name;
        public Sprite sprite;
    }

    [Header("背景圖庫")]
    [Tooltip("手動輸入背景名稱與圖片")]
    public List<BackgroundEntry> backgrounds = new List<BackgroundEntry>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    /// <summary>
    /// 切換背景：<<bg 背景註冊名稱>>
    /// 設為 static 可避免 Yarn Spinner 去尋找同名的 GameObject，解決報錯問題。
    /// </summary>
    [YarnCommand("bg")]
    public static void ChangeBackground(string bgName)
    {
        if (Instance == null)
        {
            Debug.LogError("【背景管理】場景中找不到 BackgroundManager 實例！");
            return;
        }

        if (Instance.bgImage == null) return;

        BackgroundEntry entry = Instance.backgrounds.Find(e => e.name == bgName);
        if (entry.sprite != null)
        {
            Instance.bgImage.sprite = entry.sprite;
        }
        else
        {
            // 找不到圖片時只發出警告，不中斷遊戲
            Debug.LogWarning($"【背景管理】找不到名為 '{bgName}' 的背景註冊。請檢查 Inspector 設定。");
        }
    }
}
