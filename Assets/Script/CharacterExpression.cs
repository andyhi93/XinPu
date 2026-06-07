using UnityEngine;
using UnityEngine.UI;
using Yarn.Unity; // 記得引入 Yarn 命名空間

public class CharacterExpression : MonoBehaviour
{
    [Header("=== 立繪設定 ===")]
    public Image characterImage; // 拖入畫面上顯示立繪的 UI Image
    // 這裡放入你的表情差分圖片
    public Sprite idleSprite;
    public Sprite tiredSprite;

    [Header("=== 背景設定 ===")]
    public Image bgImage;        // 拖入畫面上顯示背景的 UI Image
    // 這裡放入你想切換的背景圖片
    public Sprite[] bgSprites;

    // 必須在 Start 或 Awake 中註冊指令
    void Start()
    {
        // 尋找場景中的 DialogueRunner 并註冊指令
        var dialogueRunner = FindFirstObjectByType<DialogueRunner>();
        
        // 1. 註冊一個叫做 <<face 差分名稱>> 的指令
        dialogueRunner.AddCommandHandler<string>("face", ChangeExpression);

        // 2. 註冊一個叫做 <<bg 背景名稱>> 的指令
        dialogueRunner.AddCommandHandler<string>("bg", ChangeBackground);
    }

    // 這個方法會在 Yarn 呼叫 <<face xxx>> 時執行
    void ChangeExpression(string expressionName)
    {
        switch (expressionName)
        {
            case "idle":
                characterImage.sprite = idleSprite;
                break;
            case "tired":
                characterImage.sprite = tiredSprite;
                break;
            default:
                Debug.LogWarning($"找不到名為 {expressionName} 的表情差分");
                break;
        }
    }

    // 這個方法會在 Yarn 呼叫 <<bg xxx>> 時執行
    void ChangeBackground(string bgName)
    {
        switch (bgName)
        {
            case "house_am":
                bgImage.sprite = bgSprites[0];
                break;
            case "DryingYard":
                bgImage.sprite = bgSprites[1];
                break;
            case "FirstMarket":
                bgImage.sprite = bgSprites[2];
                break;
            case "lake":
                bgImage.sprite = bgSprites[3];
                break;
            case "house_pm":
                bgImage.sprite = bgSprites[4];
                break;
            default:
                Debug.LogWarning($"找不到名為 {bgName} 的背景圖片");
                break;
        }
    }
}