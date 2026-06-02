using UnityEngine;
using UnityEngine.UI;
using Yarn.Unity; // 引入 Yarn 命名空間

[RequireComponent(typeof(CanvasGroup))]
public class CharacterMovement : MonoBehaviour
{
    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;

    [Header("=== 顏色調整設定 ===")]
    public Color dimColor = new Color(0.6f, 0.6f, 0.6f, 1f); 
    private Color normalColor = Color.white;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
    }

    void Start() { }

    // 【功能一】瞬移（把第二個參數改成 string，解決 Yarn 3.x 參數解析失敗的 Bug）
    [YarnCommand("teleport")]
    public void TeleportCharacter(string nameParameter, string targetXStr)
    {
        if (nameParameter == gameObject.name)
        {
            // 將字串安全的轉換成 float 數字，如果失敗預設為 0
            if (float.TryParse(targetXStr, out float targetX))
            {
                Vector2 currentPos = rectTransform.anchoredPosition;
                currentPos.x = targetX;
                rectTransform.anchoredPosition = currentPos;
            }
            else
            {
                Debug.LogError($"【瞬移錯誤】無法將輸入的座標 '{targetXStr}' 轉換成數字！");
            }
        }
    }

    // 【功能二】開關角色功能（同樣改成 string 來接收 true/false，最安全）
    [YarnCommand("show")]
    public void SetCharacterActive(string nameParameter, string isActiveStr)
    {
        if (nameParameter == gameObject.name)
        {
            bool isActive = isActiveStr.ToLower() == "true";
            canvasGroup.alpha = isActive ? 1f : 0f;
            canvasGroup.blocksRaycasts = isActive;
        }
    }

    // 【功能三】手動變亮
    [YarnCommand("light")]
    public void LightCharacter(string nameParameter)
    {
        if (nameParameter == gameObject.name)
        {
            canvasGroup.alpha = 1f;
        }
    }

    // 【功能四】手動變暗
    [YarnCommand("dim")]
    public void DimCharacter(string nameParameter)
    {
        if (nameParameter == gameObject.name)
        {
            canvasGroup.alpha = 0.5f;
        }
    }
}