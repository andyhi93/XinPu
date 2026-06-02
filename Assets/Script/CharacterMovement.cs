using UnityEngine;
using UnityEngine.UI;
using Yarn.Unity; // 引入 Yarn 命名空間
using TMPro;

[RequireComponent(typeof(CanvasGroup))]
public class CharacterMovement : MonoBehaviour
{
    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    public TMP_Text nameText;

    [Header("=== 顏色調整設定 ===")]
    public Color dimColor = new Color(0.6f, 0.6f, 0.6f, 1f); 
    private Color normalColor = Color.white;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
    }

    void Start() { }

    // 【功能一】瞬移
    // 在 Yarn Spinner 中，如果 [YarnCommand] 放在非靜態方法上，
    // 第一個參數會自動被 Yarn 用來尋找物件，因此 C# 方法不需要再接收物件名稱。
    [YarnCommand("teleport")]
    public void TeleportCharacter(string targetXStr)
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

    // 【功能二】開關角色功能
    [YarnCommand("show")]
    public void SetCharacterActive(string isActiveStr)
    {
        bool isActive = isActiveStr.ToLower() == "true";
        canvasGroup.alpha = isActive ? 1f : 0f;
        canvasGroup.blocksRaycasts = isActive;
    }

    // 【功能三】手動變亮
    [YarnCommand("light")]
    public void LightCharacter()
    {
        canvasGroup.alpha = 1f;
    }

    // 【功能四】手動變暗
    [YarnCommand("dim")]
    public void DimCharacter()
    {
        canvasGroup.alpha = 0.5f;
    }

    [YarnCommand("flip")]
    public void FlipCharacter()
    {
        Vector3 currentScale = transform.localScale;
        currentScale.x *= -1f;
        transform.localScale = currentScale;
    }

    [YarnCommand("name")]
    public void NameCharacter(string name)
    {
        nameText.text = name;
    }
}