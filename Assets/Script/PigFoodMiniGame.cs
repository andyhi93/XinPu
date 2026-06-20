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
    [SerializeField] private Button stopButton; // 熄火按鈕，煮好後才能按
    [SerializeField] private Slider cookProgressBar;
    [SerializeField] private Slider fireBar;
    [SerializeField] private Slider fuelBar;
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private GameObject pigFoodGame;
    [SerializeField] private GameObject chopSection;
    [SerializeField] private GameObject cookSection;
    [SerializeField] private Image pigfoodFire; // 本爐火焰圖，點火時啟用
    [SerializeField] private Image cookFire;    // 廚房爐火狀態指示，與 KitchenMiniGame 同步

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

    private const float FireTimerLimitSeconds = 60f * 60f; // 著火計時器上限：1 小時遊戲時間
    private bool fireTimerActive = false;     // 著火計時器是否在跑
    private float fireTimerStartSeconds = 0f; // 豬菜煮好當下的遊戲時間
    private bool fireEventTriggered = false;  // 是否已經觸發過 Scene_FireEvent

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
        if (stopButton != null)
        {
            stopButton.onClick.AddListener(OnClickStop);
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

        EventManager.SetFlag("pigfood_fire_lit", fireLevel > 0);
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

        EventManager.SetFlag("pigfood_fire_lit", true);
        UpdateUI();
    }

    private void Update()
    {
        // 不論是否還在烹煮，都要持續檢查著火計時器
        CheckFireTimer();

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
            StartFireTimer();
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

        // 本爐火焰圖：有火力時顯示
        if (pigfoodFire != null)
        {
            pigfoodFire.gameObject.SetActive(fireLevel > 0);
        }

        // 廚房爐火狀態指示：跟 KitchenMiniGame 的旗標同步
        if (cookFire != null)
        {
            cookFire.gameObject.SetActive(EventManager.HasFlag("kitchen_fire_lit"));
        }

        // 熄火按鈕：煮好之前不能按，直接隱藏
        if (stopButton != null)
        {
            stopButton.interactable = isDone;
            stopButton.gameObject.SetActive(isDone);
        }
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
    /// 豬菜煮好的瞬間啟動著火計時器（使用遊戲時間），並亮起灶房 EventDot（黃色警示）
    /// </summary>
    private void StartFireTimer()
    {
        fireTimerActive = true;
        fireEventTriggered = false;
        fireTimerStartSeconds = TimeManager.Instance != null ? TimeManager.Instance.CurrentTimeInSeconds : 0f;

        if (LocationManager.Instance != null)
        {
            LocationManager.Instance.SetEventDot(LocationManager.Location.灶房, true, false);
        }
    }

    /// <summary>
    /// 每幀檢查著火計時器：超過時限就轉紅並觸發 Scene_FireEvent；
    /// 若火災已經透過劇情解決（fire_extinguished 旗標），就自動清除計時器
    /// </summary>
    private void CheckFireTimer()
    {
        if (!fireTimerActive) return;

        if (fireEventTriggered && EventManager.HasFlag("fire_extinguished"))
        {
            ExtinguishFire();
            return;
        }

        if (fireEventTriggered || TimeManager.Instance == null) return;

        float elapsed = TimeManager.Instance.CurrentTimeInSeconds - fireTimerStartSeconds;
        if (elapsed < 0) elapsed += 24f * 3600f; // 處理跨日情況

        if (elapsed >= FireTimerLimitSeconds)
        {
            fireEventTriggered = true;

            if (LocationManager.Instance != null)
            {
                LocationManager.Instance.SetEventDot(LocationManager.Location.灶房, true, true);
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.ForceStartDialogue("Scene_FireEvent");
            }
        }
    }

    /// <summary>
    /// 熄火：清除本爐的著火狀態、計時器與灶房 EventDot
    /// </summary>
    private void ExtinguishFire()
    {
        // 注意：isCooking／isDone 不重置，代表「烹煮流程已開始/已完成」，與 messageText 顯示邏輯一致
        fireLevel = 0f;
        fuelLevel = 0f;

        fireTimerActive = false;
        fireEventTriggered = false;

        EventManager.SetFlag("pigfood_fire_lit", false);

        if (LocationManager.Instance != null)
        {
            LocationManager.Instance.SetEventDot(LocationManager.Location.灶房, false, false);
        }

        UpdateUI();
    }

    /// <summary>
    /// 熄火按鈕點擊事件，煮好後才能按
    /// </summary>
    public void OnClickStop()
    {
        if (!isDone) return;

        ExtinguishFire();
        EventManager.SetFlag("fire_extinguished", true);
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
