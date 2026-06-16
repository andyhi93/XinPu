using UnityEngine;
using UnityEngine.UI;
using Yarn.Unity;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// 角色控制器：管理單一角色的位置、表情、顯隱與名稱顯示。
/// 掛載在場景中代表該角色的 GameObject 上（名字需與 Yarn 腳本中呼叫的一致）。
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class CharacterController : MonoBehaviour
{
    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;

    [Header("UI 元件")]
    public Image characterImage;
    public TMP_Text nameText;

    [System.Serializable]
    public struct ExpressionEntry
    {
        public string name;
        public Sprite sprite;
    }

    [Header("表情設定")]
    [Tooltip("手動輸入對應名稱與圖片")]
    public List<ExpressionEntry> expressions = new List<ExpressionEntry>();

    [Header("轉向設定")]
    [Tooltip("目前的面向：勾選為右，不勾選為左")]
    public bool isFacingRight = true;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
    }

    /// <summary>
    /// 瞬移到指定座標：<<teleport 角色名字 X [Y]>>
    /// 若沒有提供 Y，則保持原本的 Y 軸高度。
    /// </summary>
    [YarnCommand("teleport")]
    public void Teleport(float x, float y = -99999f)
    {
        Vector2 pos = rectTransform.anchoredPosition;
        pos.x = x;
        // 如果傳入的 y 不是我們設定的極端預設值，就代表有傳入 Y 座標
        if (y != -99999f)
        {
            pos.y = y;
        }
        rectTransform.anchoredPosition = pos;
    }

    /// <summary>
    /// 設定表情：<<face 角色名字 註冊名稱>>
    /// </summary>
    [YarnCommand("face")]
    public void SetFace(string expressionName)
    {
        if (characterImage == null) return;

        ExpressionEntry entry = expressions.Find(e => e.name == expressionName);
        if (entry.sprite != null)
        {
            characterImage.sprite = entry.sprite;
        }
        else
        {
            Debug.LogWarning($"【角色控制】{gameObject.name} 找不到名為 '{expressionName}' 的表情註冊。");
        }
    }

    /// <summary>
    /// 顯示或隱藏角色：<<show 角色名字 true/false>>
    /// </summary>
    [YarnCommand("show")]
    public void SetShow(bool isActive)
    {
        canvasGroup.alpha = isActive ? 1f : 0f;
        canvasGroup.blocksRaycasts = isActive;
    }

    /// <summary>
    /// 變亮 (100% 不透明)：<<light 角色名字>>
    /// </summary>
    [YarnCommand("light")]
    public void Light()
    {
        canvasGroup.alpha = 1f;
    }

    /// <summary>
    /// 變暗 (50% 不透明)：<<dim 角色名字>>
    /// </summary>
    [YarnCommand("dim")]
    public void Dim()
    {
        canvasGroup.alpha = 0.5f;
    }

    /// <summary>
    /// 轉向控制：<<flip 角色名字 [L/R/toggle]>>
    /// </summary>
    /// <param name="mode">"L"、"R" 或 "toggle" (預設為 toggle)</param>
    [YarnCommand("flip")]
    public void Flip(string mode = "toggle")
    {
        string m = mode.ToLower();
        bool targetFacingRight = isFacingRight;

        if (m == "r")
        {
            targetFacingRight = true;
        }
        else if (m == "l")
        {
            targetFacingRight = false;
        }
        else
        {
            targetFacingRight = !isFacingRight;
        }

        // 如果目標面向與現在不同，就直接翻轉 X 軸，這樣就能適應預設朝左或朝右的圖片
        if (targetFacingRight != isFacingRight)
        {
            Vector3 scale = transform.localScale;
            scale.x *= -1f;
            transform.localScale = scale;
            isFacingRight = targetFacingRight;
        }
    }

    /// <summary>
    /// 修改顯示名字：<<name 角色名字 顯示文字>>
    /// </summary>
    [YarnCommand("name")]
    public void SetDisplayName(string text)
    {
        if (nameText != null) nameText.text = text;
    }
}
