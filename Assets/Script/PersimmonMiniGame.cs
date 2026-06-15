using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// 柿子加工小遊戲邏輯控制器
/// </summary>
public class PersimmonMiniGame : MonoBehaviour
{
    [Header("遊戲設定")]
    [SerializeField] private int peelRequiredCount = 7;    // 需要按幾下才算削皮完成
    [SerializeField] private int dailyTarget = 10;        // 今天的目標總顆數

    [Header("UI 元件")]
    [SerializeField] private GameObject gamePanel;        // 整個小遊戲的視窗面板
    [SerializeField] private Image processImage;           // 柿子狀態圖（保留元件，目前不實作圖標邏輯）
    [SerializeField] private TextMeshProUGUI messageText; // 顯示進度的文字，如「第 X/N 顆」
    [SerializeField] private Button peelButton;          // 削皮按鈕
    [SerializeField] private Button stemButton;          // 去蒂按鈕
    [SerializeField] private Button nextButton;          // 下一顆按鈕

    // 運行時狀態變數
    private int currentPersimmonIndex = 1; // 當前是第幾顆
    private int peelCount = 0;             // 目前這顆已按下的削皮次數
    private bool canStem = false;          // 是否可以進行去蒂
    private bool isWaiting = false;        // 是否正在冷卻等待中（處理中不可點擊）

    private void Awake()
    {
        // 綁定按鈕點擊事件
        if (peelButton != null) peelButton.onClick.AddListener(OnPeelClick);
        if (stemButton != null) stemButton.onClick.AddListener(OnStemClick);
        if (nextButton != null) nextButton.onClick.AddListener(OnNextClick);
    }

    private void Start()
    {
        // 遊戲初始化
        InitializeGame();
    }

    /// <summary>
    /// 初始化遊戲狀態
    /// </summary>
    private void InitializeGame()
    {
        currentPersimmonIndex = 1;
        peelCount = 0;
        canStem = false;
        isWaiting = false;
        if (nextButton != null) nextButton.gameObject.SetActive(false);
        UpdateUI();
    }

    /// <summary>
    /// 根據目前狀態更新 UI 顯示內容與按鈕可用性
    /// </summary>
    private void UpdateUI()
    {
        // 更新進度文字
        if (messageText != null)
        {
            messageText.text = $"第 {currentPersimmonIndex}/{dailyTarget} 顆";
        }

        // 檢查目前這顆是否已經完全處理完畢（Next 按鈕出現即代表完成）
        bool isDone = nextButton != null && nextButton.gameObject.activeSelf;

        // 削皮按鈕可用條件：不在等待中 且 還沒削皮完成 且 還沒整顆處理完
        bool peelInteractable = !isWaiting && !canStem && !isDone;
        SetButtonState(peelButton, peelInteractable);

        // 去蒂按鈕可用條件：不在等待中 且 已經削皮完成 且 還沒整顆處理完
        bool stemInteractable = !isWaiting && canStem && !isDone;
        SetButtonState(stemButton, stemInteractable);
    }

    /// <summary>
    /// 設定按鈕的互動狀態與圖片透明度
    /// </summary>
    private void SetButtonState(Button button, bool interactable)
    {
        if (button != null)
        {
            button.interactable = interactable;
            
            // 直接抓 Button 物件上的 Image 組件來改 Alpha
            Image btnImg = button.GetComponent<Image>();
            if (btnImg != null)
            {
                Color c = btnImg.color;
                c.a = interactable ? 1.0f : 0.5f;
                btnImg.color = c;
            }
        }
    }

    /// <summary>
    /// 點擊削皮按鈕
    /// </summary>
    private void OnPeelClick()
    {
        if (!isWaiting && !canStem)
        {
            StartCoroutine(PeelRoutine());
        }
    }

    /// <summary>
    /// 削皮動作協程：等待 1 秒
    /// </summary>
    private IEnumerator PeelRoutine()
    {
        isWaiting = true;
        UpdateUI(); 
        
        yield return new WaitForSeconds(1.0f);
        
        peelCount++;
        if (peelCount >= peelRequiredCount)
        {
            canStem = true; 
        }
        
        isWaiting = false;
        UpdateUI();
    }

    /// <summary>
    /// 點擊去蒂按鈕
    /// </summary>
    private void OnStemClick()
    {
        if (!isWaiting && canStem)
        {
            StartCoroutine(StemRoutine());
        }
    }

    /// <summary>
    /// 去蒂動作協程：等待 3 秒
    /// </summary>
    private IEnumerator StemRoutine()
    {
        isWaiting = true;
        UpdateUI();
        
        yield return new WaitForSeconds(3.0f);
        
        isWaiting = false;
        if (nextButton != null) nextButton.gameObject.SetActive(true); 
        UpdateUI();
    }

    /// <summary>
    /// 點擊下一顆按鈕：重置單顆狀態
    /// </summary>
    private void OnNextClick()
    {
        currentPersimmonIndex++;
        peelCount = 0;
        canStem = false;
        isWaiting = false;
        if (nextButton != null) nextButton.gameObject.SetActive(false);
        UpdateUI();
    }

    /// <summary>
    /// 處理退出邏輯（提供給 Inspector 中的事件調用，如 Button.onClick 或 GenericEventTrigger.OnClick）
    /// </summary>
    public void HandleExit()
    {
        if (gamePanel != null)
        {
            gamePanel.SetActive(false);
        }
        
        StopAllCoroutines();
        isWaiting = false;
        
        // 狀態保留
        UpdateUI();
    }
}
