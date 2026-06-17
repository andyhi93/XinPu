using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Yarn.Unity;

/// <summary>
/// 豬菜小遊戲控制器
/// 負責剁菜及烹煮進度管理，並與 Yarn Spinner 對話系統整合
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
    [SerializeField] private float cookProgress = 0f;
    [SerializeField] private float fireLevel = 0f;
    [SerializeField] private float fuelLevel = 0f;

    private CanvasGroup chopButtonCG;
    private CanvasGroup addToStoveButtonCG;
    private CanvasGroup fireButtonCG;
    private CanvasGroup addWoodButtonCG;

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
            addWoodButtonCG = addWoodButton.GetComponent<CanvasGroup>();
        }
        if (serveFoodButton != null)
        {
            serveFoodButton.onClick.AddListener(OnClickExit); // 暫時用退出當作盛菜
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
        if (serveFoodButton != null) serveFoodButton.gameObject.SetActive(isDone);
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
        if (!isAtStove || isCooking) return;
        
        isCooking = true;
        fireLevel = 50f;  // 點火後火力先到一半
        fuelLevel = 50f;  // 點火後燃料給一半
        UpdateUI();
    }

    /// <summary>
    /// 加柴按鈕點擊事件
    /// </summary>
    public void OnClickAddWood()
    {
        Debug.Log("【豬菜小遊戲】加柴！燃料與火力補滿。");
        fireLevel = 100f;
        fuelLevel = 100f;
        UpdateUI();
    }

    private void Update()
    {
        // 若視窗未開啟，則不執行邏輯
        if (pigFoodGame == null || !pigFoodGame.activeSelf) return;
        
        // 若非烹煮中或已完成，則不更新進度
        if (!isCooking || isDone) return;

        // 更新烹煮進度
        cookProgress += (100f / cookDuration) * Time.deltaTime;
        cookProgress = Mathf.Min(100f, cookProgress);
        
        if (cookProgress >= 100f)
        {
            isDone = true;
            RefreshSections(); // 顯示盛菜按鈕
        }
        
        UpdateUI();
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
                    messageText.text = "準備點火";
                else
                    messageText.text = isDone ? "豬菜好了" : "烹煮中";
            }
        }
        
        if (cookProgressBar != null)
            cookProgressBar.value = cookProgress / 100f; // 統一使用 0-1 映射

        if (fireBar != null)
            fireBar.value = fireLevel / 100f;

        if (fuelBar != null)
            fuelBar.value = fuelLevel / 100f;

        // 更新按鈕狀態
        SetButtonState(chopButton, chopButtonCG, chopCount < chopRequired && !isAtStove);
        SetButtonState(addToStoveButton, addToStoveButtonCG, chopCount >= chopRequired && !isAtStove);
        SetButtonState(fireButton, fireButtonCG, isAtStove && !isCooking);
        SetButtonState(addWoodButton, addWoodButtonCG, isAtStove);
    }

    private void SetButtonState(Button btn, CanvasGroup cg, bool interactable)
    {
        if (btn != null) btn.interactable = interactable;
        if (cg != null) cg.alpha = interactable ? 1.0f : 0.5f;
    }

    /// <summary>
    /// 退出按鈕點擊事件
    /// </summary>
    public void OnClickExit()
    {
        if (pigFoodGame != null)
            pigFoodGame.SetActive(false);
    }

    /// <summary>
    /// 供外部或 Yarn 檢查豬菜是否完成
    /// </summary>
    public bool IsPigFoodDone() => isDone;

    /// <summary>
    /// 供外部或 Yarn 檢查豬菜是否正在烹煮中（且尚未完成）
    /// </summary>
    public bool IsPigFoodCooking() => isCooking && !isDone;

    /// <summary>
    /// Yarn 指令：開啟豬菜小遊戲視窗
    /// </summary>
    [YarnCommand("open_pig_food")]
    public void OpenPigFoodWindow()
    {
        if (pigFoodGame != null)
        {
            pigFoodGame.SetActive(true);
            UpdateUI();
        }
    }

    /// <summary>
    /// Yarn 函數：檢查豬菜是否完成
    /// </summary>
    [YarnFunction("is_pig_food_done")]
    public static bool IsPigFoodDoneStatic()
    {
        return Instance != null && Instance.IsPigFoodDone();
    }

    /// <summary>
    /// Yarn 函數：檢查豬菜是否正在烹煮中
    /// </summary>
    [YarnFunction("is_pig_food_cooking")]
    public static bool IsPigFoodCookingStatic()
    {
        return Instance != null && Instance.IsPigFoodCooking();
    }
}
