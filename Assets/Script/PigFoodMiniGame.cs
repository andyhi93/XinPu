using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Yarn.Unity;

/// <summary>
/// 豬菜小遊戲控制器
/// 負責剁菜及烹煮進度 management，並與 Yarn Spinner 對話系統整合
/// </summary>
public class PigFoodMiniGame : MonoBehaviour
{
    public static PigFoodMiniGame Instance { get; private set; }

    [Header("UI 元件")]
    [SerializeField] private Button chopButton;
    [SerializeField] private Button addToStoveButton;
    [SerializeField] private Button fireButton;
    [SerializeField] private Button addWoodButton;
    [SerializeField] private Button serveFoodButton;
    [SerializeField] private Slider cookProgressBar;
    [SerializeField] private Slider fireBar;
    [SerializeField] private Slider fuelBar;
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private GameObject pigFoodGame;
    [SerializeField] private GameObject chopSection;
    [SerializeField] private GameObject cookSection;

    [Header("設定")]
    [SerializeField] private int chopRequired = 5;        // 需要剁幾下，預設 5
    [SerializeField] private float cookDuration = 30f;    // 煮豬菜需要幾秒

    [Header("狀態變數")]
    [SerializeField] private int chopCount = 0;
    [SerializeField] private bool isAtStove = false;      // 是否已下鍋 (進入 CookSection)
    [SerializeField] private bool isCooking = false;
    [SerializeField] private bool isDone = false;
    [SerializeField] private bool hasCollected = false;   // 是否已盛出豬菜
    [SerializeField] private float cookProgress = 0f;
    [SerializeField] private float fireLevel = 0f;
    [SerializeField] private float fuelLevel = 0f;

    private CanvasGroup chopButtonCG;
    private CanvasGroup addToStoveButtonCG;
    private CanvasGroup fireButtonCG;
    private CanvasGroup addWoodCG;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // 綁定按鈕事件
        if (chopButton != null)
        {
            chopButton.onClick.AddListener(OnClickChop);
            chopButtonCG = chopButton.GetComponent<CanvasGroup>();
        }
        if (addToStoveButton != null)
        {
            addToStoveButton.onClick.AddListener(OnClickAddToStove);
            addToStoveButtonCG = addToStoveButton.GetComponent<CanvasGroup>();
        }
        if (fireButton != null)
        {
            fireButton.onClick.AddListener(OnClickFire);
            fireButtonCG = fireButton.GetComponent<CanvasGroup>();
        }
        if (addWoodButton != null)
        {
            addWoodButton.onClick.AddListener(OnClickAddWood);
            addWoodCG = addWoodButton.GetComponent<CanvasGroup>();
        }
        if (serveFoodButton != null)
        {
            serveFoodButton.onClick.AddListener(OnClickServeFood);
        }
    }

    private void Start()
    {
        UpdateUI();
        RefreshSections();
    }

    private void RefreshSections()
    {
        if (chopSection != null) chopSection.SetActive(!isAtStove);
        if (cookSection != null) cookSection.SetActive(isAtStove);
        if (serveFoodButton != null) serveFoodButton.gameObject.SetActive(isDone && !hasCollected);
    }

    /// <summary>
    /// 剁菜按鈕點擊事件
    /// </summary>
    public void OnClickChop()
    {
        if (chopCount >= chopRequired || isAtStove) return;

        chopCount++;
        UpdateUI();
    }

    /// <summary>
    /// 下鍋按鍋按鈕點擊事件
    /// </summary>
    public void OnClickAddToStove()
    {
        if (chopCount < chopRequired || isAtStove) return;
        
        isAtStove = true;
        RefreshSections();
        UpdateUI();
    }

    /// <summary>
    /// 點火按鈕點擊事件
    /// </summary>
    public void OnClickFire()
    {
        if (!isAtStove) return;
        
        fireLevel = Mathf.Min(1.0f, fireLevel + 0.5f);
        fuelLevel = Mathf.Min(1.0f, fuelLevel + 0.5f);
        
        if (fireLevel >= 1.0f)
        {
            isCooking = true;
        }
        UpdateUI();
    }

    /// <summary>
    /// 加柴按鈕點擊事件
    /// </summary>
    public void OnClickAddWood()
    {
        if (!isAtStove) return;
        Debug.Log("【豬菜小遊戲】加柴！燃料與火力補滿。");
        fireLevel = 1.0f;
        fuelLevel = 1.0f;
        isCooking = true;
        UpdateUI();
    }

    private void Update()
    {
        // 若非烹煮中或已完成，則不更新進度
        if (!isCooking || isDone) return;

        // 更新烹煮進度，速率隨火力大小變化 (1.0 為正常速率)
        if (fireLevel > 0)
        {
            cookProgress += (1.0f / cookDuration) * fireLevel * Time.deltaTime;
            cookProgress = Mathf.Min(1.0f, cookProgress);
        }
        
        bool justFinished = false;
        if (cookProgress >= 1.0f)
        {
            isDone = true;
            justFinished = true;
        }
        
        // 視窗開啟時才更新 UI 表現
        if (pigFoodGame != null && pigFoodGame.activeSelf)
        {
            if (justFinished) RefreshSections();
            UpdateUI();
        }
    }

    /// <summary>
    /// 更新 UI 表現（文字、進度條、按鈕可用性與透明度）
    /// </summary>
    private void UpdateUI()
    {
        if (messageText != null)
        {
            if (!isAtStove)
            {
                if (chopCount < chopRequired)
                    messageText.text = $"剁菜中 {chopCount}/{chopRequired} 刀";
                else
                    messageText.text = "準備下鍋";
            }
            else
            {
                if (!isCooking)
                {
                    if (fireLevel > 0)
                        messageText.text = "需要加柴";
                    else
                        messageText.text = "準備點火";
                }
                else
                    messageText.text = isDone ? "豬菜好了" : "烹煮中";
            }
        }
        
        if (cookProgressBar != null)
            cookProgressBar.value = cookProgress;

        if (fireBar != null)
            fireBar.value = fireLevel;

        if (fuelBar != null)
            fuelBar.value = fuelLevel;

        // 更新按鈕狀態
        SetButtonState(chopButton, chopButtonCG, chopCount < chopRequired && !isAtStove);
        SetButtonState(addToStoveButton, addToStoveButtonCG, chopCount >= chopRequired && !isAtStove);
        
        // 點火按鈕：在爐灶前且火力未滿時可用
        SetButtonState(fireButton, fireButtonCG, isAtStove && fireLevel < 1.0f);
        
        // 加柴按鈕：在爐灶前且火力未滿時可用
        SetButtonState(addWoodButton, addWoodCG, isAtStove && fireLevel < 1.0f);
    }

    private void SetButtonState(Button btn, CanvasGroup cg, bool interactable)
    {
        if (btn != null) btn.interactable = interactable;
        if (cg != null)
        {
            cg.alpha = interactable ? 1.0f : 0.5f;
            cg.interactable = interactable;
            cg.blocksRaycasts = interactable;
        }
    }

    /// <summary>
    /// 盛菜按鈕點擊事件 (煮完後)
    /// </summary>
    public void OnClickServeFood()
    {
        if (isDone && !hasCollected)
        {
            hasCollected = true;
            RefreshSections();
            OnClickExit(); // 盛出後自動關閉視窗
        }
    }

    /// <summary>
    /// 退出按鈕點擊事件
    /// </summary>
    public void OnClickExit()
    {
        if (pigFoodGame != null)
        {
            pigFoodGame.SetActive(false);
        }

        // 通知 GameManager 關閉小遊戲並返回三合院
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ClosePigFoodGame();
        }
    }

    /// <summary>
    /// 供外部或 Yarn 檢查豬菜是否完成並盛出
    /// </summary>
    public bool IsPigFoodDone() => hasCollected;

    /// <summary>
    /// 供外部或 Yarn 檢查豬菜是否正在烹煮中（包含已完成但尚未盛出）
    /// </summary>
    public bool IsPigFoodCooking() => isCooking && !hasCollected;

    /// <summary>
    /// 開啟豬菜小遊戲視窗
    /// </summary>
    public void OpenPigFoodWindow()
    {
        if (pigFoodGame != null)
        {
            pigFoodGame.SetActive(true);
            RefreshSections(); // 確保重新開啟時，介面區塊顯示正確 (例如：背景煮好時顯示盛菜按鈕)
            UpdateUI();
        }
    }
}
