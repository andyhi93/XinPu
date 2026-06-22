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
    [SerializeField] private float fireBoostPerClick = 0.5f; // 加柴每次點擊增加的火力與燃料量（0~1 尺度）

    [Header("狀態變數")]
    [SerializeField] private int chopCount = 0;
    [SerializeField] private bool isAtStove = false;      // 是否已下鍋 (進入 CookSection)
    [SerializeField] private bool isCooking = false;
    [SerializeField] private bool isDone = false;
    [SerializeField] private bool hasCollected = false;   // 是否已盛出豬菜
    [SerializeField] private bool hasStoppedFire = false; // 是否已經按過熄火
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
    /// 點火按鈕點擊事件：只有「還沒點火」時才能點，火已經點著或豬菜已經煮好都不能再按
    /// （跟加柴是兩個不同的動作）。點下去直接點燃（火力／燃料不會隨時間下降，降低玩家負擔），
    /// 之後可以用加柴把火力補到最大，煮得更快。
    /// </summary>
    public void OnClickFire()
    {
        if (!isAtStove || isCooking || isDone) return;

        fireLevel = fireBoostPerClick;
        fuelLevel = fireBoostPerClick;
        isCooking = true;

        EventManager.SetFlag("pigfood_fire_lit", true);
        UpdateUI();
    }

    /// <summary>
    /// 加柴按鈕點擊事件：只有「火已經點著、還在燒」時才能加柴，沒點火或已經煮好都不能按
    /// （跟點火是兩個不同的動作，邏輯參考 KitchenMiniGame 的 OnClickAddWood）
    /// </summary>
    public void OnClickAddWood()
    {
        if (!isAtStove || !isCooking || isDone) return;

        fireLevel = Mathf.Min(1.0f, fireLevel + fireBoostPerClick);
        fuelLevel = Mathf.Min(1.0f, fuelLevel + fireBoostPerClick);

        EventManager.SetFlag("pigfood_fire_lit", true);
        UpdateUI();
    }

    private void Update()
    {
        // 不論視窗是否開啟，都要持續檢查著火計時器、累積烹煮進度
        CheckFireTimer();
        UpdateCookProgress();

        // 視窗開啟時才更新 UI 表現
        if (pigFoodGame != null && pigFoodGame.activeSelf)
        {
            UpdateUI();
        }
    }

    /// <summary>
    /// 累積烹煮進度，速率隨火力大小變化（火力／燃料不會隨時間下降，只有玩家自己選擇加柴才會變化）
    /// </summary>
    private void UpdateCookProgress()
    {
        if (!isCooking || isDone) return;

        if (fireLevel > 0)
        {
            cookProgress += (1.0f / cookDuration) * fireLevel * Time.deltaTime;
            cookProgress = Mathf.Min(1.0f, cookProgress);
        }

        if (cookProgress >= 1.0f)
        {
            cookProgress = 1.0f;
            isDone = true;
            StartFireTimer();
            RefreshSections(); // 確保盛菜按鈕的顯示狀態立刻更新
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
                    messageText.text = "準備點火";
                else
                    messageText.text = isDone ? "豬菜好了" : "烹煮中，加柴可以煮更快";
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
        
        // 點火按鈕：在爐灶前、還沒點火、也還沒煮好時可用（已經煮好就不能再點火，跟煮飯一致）
        SetButtonState(fireButton, fireButtonCG, isAtStove && !isCooking && !isDone);

        // 加柴按鈕：火已經點著、還在燒、且火力未滿時可用（必須先點火才能加柴）
        SetButtonState(addWoodButton, addWoodCG, isAtStove && isCooking && !isDone && fireLevel < 1.0f);

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

        // 熄火按鈕：煮好之前不能按；已經熄過火就直接隱藏，不會一直顯示
        if (stopButton != null)
        {
            bool canStop = isDone && !hasStoppedFire;
            stopButton.interactable = canStop;
            stopButton.gameObject.SetActive(canStop);
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
        hasStoppedFire = true;

        fireTimerActive = false;
        fireEventTriggered = false;

        EventManager.SetFlag("pigfood_fire_lit", false);

        if (LocationManager.Instance != null)
        {
            LocationManager.Instance.SetEventDot(LocationManager.Location.灶房, false, false);
        }

        RefreshSections(); // 確保熄火後盛菜按鈕的顯示狀態（isDone && !hasCollected）有重新整理
        UpdateUI();
    }

    /// <summary>
    /// 熄火按鈕點擊事件，煮好後才能按
    /// </summary>
    public void OnClickStop()
    {
        if (!isDone || hasStoppedFire) return;

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
    /// 跨日時呼叫：重置整輪剁菜／烹煮狀態，讓新的一天可以重新煮豬菜（跟煮飯一樣一天只能煮一次）
    /// </summary>
    public void ResetDailyState()
    {
        chopCount = 0;
        isAtStove = false;
        isCooking = false;
        isDone = false;
        hasCollected = false;
        hasStoppedFire = false;
        cookProgress = 0f;
        fireLevel = 0f;
        fuelLevel = 0f;

        RefreshSections();
        UpdateUI();
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
    /// 供 Yarn 腳本檢查：這一輪豬菜已經盛出、火也關了（跟煮飯一樣，今天不能再進去）
    /// </summary>
    [YarnFunction("is_pig_food_done_for_now")]
    public static bool IsPigFoodDoneForNow()
    {
        if (Instance == null) return false;
        return Instance.hasCollected && Instance.hasStoppedFire;
    }

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
