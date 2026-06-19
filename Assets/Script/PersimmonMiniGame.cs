using System.Collections;
using UnityEngine;
using UnityEngine.UI;
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
    [SerializeField] private Image processImage;          // 柿子狀態圖 (顯示在框內的柿子)
    [SerializeField] private TextMeshProUGUI messageText; // 顯示進度的文字
    [SerializeField] private Button peelButton;          // 削皮按鈕（半透明點擊區）
    [SerializeField] private Button stemButton;          // 去蒂按鈕
    [SerializeField] private Button nextButton;          // 下一顆按鈕

    [Header("隨機移動設定")]
    [SerializeField] private RectTransform moveArea;      // 按鈕可以隨機出現的範圍框
    private RectTransform peelButtonRect;                 // 削皮按鈕本身的 RectTransform

    [Header("圖片差分設定")]
    [Tooltip("剝皮過程的圖片。索引 0 是初始未剝皮狀態，索引 1 是剝 1 次，依此類推。")]
    [SerializeField] private Sprite[] peelSprites;        
    [Tooltip("去蒂完成後的圖片。")]
    [SerializeField] private Sprite stemmedSprite;        

    // 運行時狀態變數
    private int currentPersimmonIndex = 1;
    private int peelCount = 0;
    private bool canStem = false;
    private bool isWaiting = false;

    private void Awake()
    {
        if (peelButton != null) 
        {
            peelButton.onClick.AddListener(OnPeelClick);
            peelButtonRect = peelButton.GetComponent<RectTransform>();
        }
        if (stemButton != null) stemButton.onClick.AddListener(OnStemClick);
        if (nextButton != null) nextButton.onClick.AddListener(OnNextClick);
    }

    private void Start()
    {
        InitializeGame();
    }

    private void InitializeGame()
    {
        currentPersimmonIndex = 1;
        peelCount = 0;
        canStem = false;
        isWaiting = false;
        
        if (nextButton != null) nextButton.gameObject.SetActive(false);
        
        // 確保一開始削皮按鈕是顯示的，並給予隨機位置
        if (peelButton != null) peelButton.gameObject.SetActive(true);
        MoveButtonToRandomPosition(); 
        
        // 設定初始未剝皮的圖片
        UpdatePersimmonImage();
        UpdateUI();
    }

    /// <summary>
    /// 將削皮按鈕移動到指定範圍內的隨機位置
    /// </summary>
    private void MoveButtonToRandomPosition()
    {
        if (moveArea == null || peelButtonRect == null) return;

        float minX = moveArea.rect.xMin + (peelButtonRect.rect.width / 2f);
        float maxX = moveArea.rect.xMax - (peelButtonRect.rect.width / 2f);
        float minY = moveArea.rect.yMin + (peelButtonRect.rect.height / 2f);
        float maxY = moveArea.rect.yMax - (peelButtonRect.rect.height / 2f);

        if (minX > maxX) { float mid = (minX + maxX) / 2f; minX = mid; maxX = mid; }
        if (minY > maxY) { float mid = (minY + maxY) / 2f; minY = mid; maxY = mid; }

        float randomX = Random.Range(minX, maxX);
        float randomY = Random.Range(minY, maxY);
        Vector3 localRandomPos = new Vector3(randomX, randomY, 0);

        Vector3 worldPos = moveArea.TransformPoint(localRandomPos);
        peelButtonRect.position = worldPos;
    }

    /// <summary>
    /// 更新柿子的圖片差分
    /// </summary>
    private void UpdatePersimmonImage()
    {
        if (processImage == null) return;

        // 若剝皮次數在陣列範圍內，則顯示對應的剝皮階段圖片
        if (peelSprites != null && peelCount < peelSprites.Length)
        {
            processImage.sprite = peelSprites[peelCount];
        }
    }

    private void UpdateUI()
    {
        if (messageText != null)
        {
            messageText.text = $"第 {currentPersimmonIndex}/{dailyTarget} 顆";
        }

        bool isDone = nextButton != null && nextButton.gameObject.activeSelf;

        bool peelInteractable = !isWaiting && !canStem && !isDone;
        SetButtonState(peelButton, peelInteractable);

        bool stemInteractable = !isWaiting && canStem && !isDone;
        SetButtonState(stemButton, stemInteractable);
    }

    private void SetButtonState(Button button, bool interactable)
    {
        if (button != null)
        {
            button.interactable = interactable;
        }
    }

    private void OnPeelClick()
    {
        if (!isWaiting && !canStem)
        {
            StartCoroutine(PeelRoutine());
        }
    }

    private IEnumerator PeelRoutine()
    {
        peelCount++;
        
        // 更新削皮圖片
        UpdatePersimmonImage();

        if (peelCount >= peelRequiredCount)
        {
            canStem = true; 
            // 達到規定次數，隱藏削皮按鈕
            if (peelButton != null) peelButton.gameObject.SetActive(false);
        }
        else
        {
            MoveButtonToRandomPosition();
        }
        
        UpdateUI();
        yield return null; 
    }

    private void OnStemClick()
    {
        if (!isWaiting && canStem)
        {
            StartCoroutine(StemRoutine());
        }
    }

    private IEnumerator StemRoutine()
    {
        isWaiting = true;
        UpdateUI();
        
        yield return new WaitForSeconds(3.0f);
        
        // 替換成去蒂完成的圖片
        if (processImage != null && stemmedSprite != null)
        {
            processImage.sprite = stemmedSprite;
        }

        isWaiting = false;
        if (nextButton != null) nextButton.gameObject.SetActive(true); 
        UpdateUI();
    }

    private void OnNextClick()
    {
        currentPersimmonIndex++;
        peelCount = 0;
        canStem = false;
        isWaiting = false;
        
        if (nextButton != null) nextButton.gameObject.SetActive(false);
        
        // 重新顯示削皮按鈕並給予新位置
        if (peelButton != null)
        {
            peelButton.gameObject.SetActive(true);
            MoveButtonToRandomPosition();
        }
        
        // 重置為初始圖片
        UpdatePersimmonImage();
        UpdateUI();
    }

    public void HandleExit()
    {
        if (gamePanel != null)
        {
            gamePanel.SetActive(false);
        }
        
        StopAllCoroutines();
        isWaiting = false;
        UpdateUI();
    }
}