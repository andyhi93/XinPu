using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Yarn.Unity;

/// <summary>
/// 廚房煮飯小遊戲控制器
/// 管理火力、燃料、烹煮進度及 UI 表現
/// </summary>
public class KitchenMiniGame : MonoBehaviour
{
    public static KitchenMiniGame Instance { get; private set; }

    // 遊戲狀態：待點火、暖火中、烹煮中、已完成、已熄滅
    public enum KitchenState { Unlit, Warming, Cooking, Done, Extinguished }

    [Header("UI 元件")]
    [SerializeField] private Slider fireBar;          // 火力條，0–100
    [SerializeField] private Slider fuelBar;          // 燃料條，0–100
    [SerializeField] private Slider cookBar;          // 煮飯進度條，0–100
    [SerializeField] private Image fireFill;          // fireBar 的填滿圖像，用於顏色變化
    [SerializeField] private TextMeshProUGUI statusText;   // 狀態文字顯示
    [SerializeField] private Button lightFireButton;  // 點火按鈕
    [SerializeField] private Button addWoodButton;    // 加柴按鈕
    [SerializeField] private Button serveFoodButton;  // 盛飯按鈕
    [SerializeField] private Button stopButton;        // 熄火按鈕，煮好後才能按
    [SerializeField] private GameObject kitchenGame;  // 整個小遊戲視窗物件
    [SerializeField] private Image cookFire;          // 本爐火焰圖，點火時啟用
    [SerializeField] private Image pigfoodFire;        // 豬菜爐火狀態指示，與 PigFoodMiniGame 同步

    [Header("可調參數")]
    [SerializeField] private float cookTotalSeconds = 120f;     // 完整煮熟需要的秒數
    [SerializeField] private float fuelDrainRate = 1.5f;        // 燃料每秒消耗率（正常火力下）
    [SerializeField] private float fireDecayRate = 2f;          // 火力每秒自然下降率
    [SerializeField] private float fireRiseRate = 15f;          // 加柴後火力上升的速率
    [SerializeField] private float fuelPerAdd = 30f;            // 每次加柴補充的燃料量
    [SerializeField] private float firePerAdd = 20f;            // 每次加柴目標增加的火力值
    [SerializeField] private float warmThreshold = 40f;         // 火力超過此值才開始計算烹煮進度
    [SerializeField] private float overheatThreshold = 75f;     // 火力超過此值進入過熱狀態（進度加快但易焦）
    [SerializeField] private Color colorLow = Color.gray;       // 低火力顏色（熄火/微火）
    [SerializeField] private Color colorNormal = new Color(1f, 0.5f, 0f); // 正常火力顏色（橘色）
    [SerializeField] private Color colorOverheat = Color.red;   // 過熱火力顏色（紅色）

    [Header("狀態變數")]
    [SerializeField] private KitchenState state = KitchenState.Unlit; // 當前狀態
    [SerializeField] private float fireLevel = 0;      // 當前火力值
    [SerializeField] private float fuelLevel = 0;      // 當前燃料值
    [SerializeField] private float cookProgress = 0;   // 當前烹煮進度
    [SerializeField] private bool isAddingFire = false; // 是否正在執行加柴動畫（防止連點）
    [SerializeField] private bool hasServed = false;     // 是否已經盛飯
    [SerializeField] private bool hasStoppedFire = false; // 這一輪是否已經按過熄火

    private CanvasGroup lightFireCG; // 點火按鈕的透明度控制
    private CanvasGroup addWoodCG;   // 加柴按鈕的透明度控制

    private const float FireTimerLimitSeconds = 60f * 60f; // 著火計時器上限：1 小時遊戲時間
    private bool fireTimerActive = false;     // 著火計時器是否在跑
    private float fireTimerStartSeconds = 0f; // 飯煮好當下的遊戲時間
    private bool fireEventTriggered = false;  // 是否已經觸發過 Scene_FireEvent

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        // 綁定點火按鈕事件並獲取 CanvasGroup
        if (lightFireButton != null)
        {
            lightFireButton.onClick.AddListener(OnClickLightFire);
            lightFireCG = lightFireButton.GetComponent<CanvasGroup>();
        }
        // 綁定加柴按鈕事件並獲取 CanvasGroup
        if (addWoodButton != null)
        {
            addWoodButton.onClick.AddListener(OnClickAddWood);
            addWoodCG = addWoodButton.GetComponent<CanvasGroup>();
        }
        // 綁定盛飯按鈕事件
        if (serveFoodButton != null)
        {
            serveFoodButton.onClick.AddListener(OnClickServeFood);
        }
        // 綁定熄火按鈕事件
        if (stopButton != null)
        {
            stopButton.onClick.AddListener(OnClickStop);
        }
    }

    private void Update()
    {
        // 如果遊戲處於暫停狀態（例如開啟對話），則停止邏輯運算
        if (TimeManager.Instance != null && TimeManager.Instance.isGamePaused)
        {
            // Debug.Log("【廚房小遊戲】全域時間暫停中，邏輯停止。");
            return;
        }

        // 不論視窗是否開啟，邏輯（火力、燃料、進度）都持續更新
        UpdateGameLogic();
        CheckFireTimer();

        // 只有視窗開啟時，才更新 UI 表現以節省效能
        if (kitchenGame != null && kitchenGame.activeSelf)
        {
            UpdateUI();
        }
    }

    /// <summary>
    /// 處理遊戲邏輯更新（火力下降、燃料消耗、進度累積）
    /// </summary>
    private void UpdateGameLogic()
    {
        // 只有在暖火或烹煮中才執行自動衰減邏輯
        if (state != KitchenState.Warming && state != KitchenState.Cooking) return;

        // --- 1. 計算燃料消耗 (火力影響燃料) ---
        float fuelDrainMultiplier;
        if (fireLevel < 20) fuelDrainMultiplier = 0.5f;
        else if (fireLevel < 75) fuelDrainMultiplier = 1.0f;
        else fuelDrainMultiplier = 1.5f;

        fuelLevel -= fuelDrainRate * fuelDrainMultiplier * Time.deltaTime;
        fuelLevel = Mathf.Max(0, fuelLevel);

        // --- 2. 計算火力衰退 (燃料影響火力) ---
        float fireDecayMultiplier;
        if (fuelLevel <= 0) fireDecayMultiplier = 5.0f;
        else if (fuelLevel < 20) fireDecayMultiplier = 3.0f;
        else if (fuelLevel <= 50) fireDecayMultiplier = 2.0f;
        else fireDecayMultiplier = 1.0f;

        fireLevel -= fireDecayRate * fireDecayMultiplier * Time.deltaTime;
        fireLevel = Mathf.Max(0, fireLevel);

        // --- 3. 檢查熄火條件 ---
        if (fireLevel <= 0)
        {
            state = KitchenState.Extinguished;
            EventManager.SetFlag("kitchen_fire_lit", false);
            return; // 熄火後不繼續計算進度
        }

        // --- 4. 處理進度累積與狀態切換 ---
        switch (state)
        {
            case KitchenState.Warming:
                if (fireLevel >= warmThreshold)
                {
                    state = KitchenState.Cooking;
                }
                break;

            case KitchenState.Cooking:
                float progressSpeed = 100f / cookTotalSeconds;
                if (fireLevel >= overheatThreshold)
                {
                    // 過熱區間：進度推行較快（1.3倍）
                    cookProgress += progressSpeed * 1.3f * Time.deltaTime;
                }
                else if (fireLevel >= warmThreshold)
                {
                    // 正常區間：進度正常推行
                    cookProgress += progressSpeed * Time.deltaTime;
                }

                // 檢查是否完成
                if (cookProgress >= 100f)
                {
                    cookProgress = 100f;
                    state = KitchenState.Done;
                    StartFireTimer();
                }
                break;
        }
    }

    /// <summary>
    /// 供 Yarn 腳本檢查飯是否煮好
    /// </summary>
    [YarnFunction("is_food_done")]
    public static bool IsFoodDone()
    {
        if (Instance == null) return false;
        return Instance.state == KitchenState.Done;
    }

    /// <summary>
    /// 供 Yarn 腳本檢查：飯已經煮好，但火還沒關，或還沒盛飯（這一輪還有事情沒做完）
    /// </summary>
    [YarnFunction("is_kitchen_unfinished")]
    public static bool IsKitchenUnfinished()
    {
        if (Instance == null) return false;
        return Instance.state == KitchenState.Done || (Instance.hasStoppedFire && !Instance.hasServed);
    }

    /// <summary>
    /// 供 Yarn 腳本檢查：這一輪飯已經盛好、火也關了（流程已完整跑完一次）
    /// </summary>
    [YarnFunction("is_kitchen_done_for_now")]
    public static bool IsKitchenDoneForNow()
    {
        if (Instance == null) return false;
        return Instance.hasStoppedFire && Instance.hasServed;
    }

    /// <summary>
    /// 更新 UI 表現（進度條、顏色、文字、按鈕狀態）
    /// </summary>
    private void UpdateUI()
    {
        // 更新進度條值（映射至 0~1）
        if (fireBar != null) fireBar.value = fireLevel / 100f;
        if (fuelBar != null) fuelBar.value = fuelLevel / 100f;
        if (cookBar != null) cookBar.value = cookProgress / 100f;

        // 根據火力區間更新 Fill 顏色
        if (fireFill != null)
        {
            if (fireLevel < 16) fireFill.color = colorLow;
            else if (fireLevel < 75) fireFill.color = colorNormal;
            else fireFill.color = colorOverheat;
        }

        // 更新狀態文字內容
        if (statusText != null)
        {
            switch (state)
            {
                case KitchenState.Unlit: statusText.text = "待點火"; break;
                case KitchenState.Warming: statusText.text = "暖火中"; break;
                case KitchenState.Cooking: statusText.text = "烹煮中"; break;
                case KitchenState.Extinguished:
                    statusText.text = (hasStoppedFire && hasServed)
                        ? "今天早上的飯已經煮過了，晚上再來煮晚飯吧。"
                        : "火熄了，重新點火";
                    break;
                case KitchenState.Done: statusText.text = "煮好了"; break;
            }
        }

        // 更新點火按鈕可用性（僅在未點火或已熄滅時可用；這一輪已經盛飯+熄火就不能再點）
        bool lightFireInteractable = (state == KitchenState.Unlit || state == KitchenState.Extinguished) && !(hasStoppedFire && hasServed);
        SetButtonState(lightFireButton, lightFireCG, lightFireInteractable);

        // 更新加柴按鈕可用性（在暖火或烹煮中，且動畫未執行時可用）
        bool addWoodInteractable = (state == KitchenState.Warming || state == KitchenState.Cooking) && !isAddingFire;
        SetButtonState(addWoodButton, addWoodCG, addWoodInteractable);

        // 更新盛飯按鈕顯示狀態：煮好且尚未盛過才顯示
        if (serveFoodButton != null)
        {
            serveFoodButton.gameObject.SetActive(state == KitchenState.Done && !hasServed);
        }

        // 本爐火焰圖：有火（暖火/烹煮/已完成）時顯示
        if (cookFire != null)
        {
            cookFire.gameObject.SetActive(state == KitchenState.Warming || state == KitchenState.Cooking || state == KitchenState.Done);
        }

        // 豬菜爐火狀態指示：跟 PigFoodMiniGame 的旗標同步
        if (pigfoodFire != null)
        {
            pigfoodFire.gameObject.SetActive(EventManager.HasFlag("pigfood_fire_lit"));
        }

        // 熄火按鈕：煮好之前不能按，直接隱藏
        if (stopButton != null)
        {
            bool canStop = state == KitchenState.Done;
            stopButton.interactable = canStop;
            stopButton.gameObject.SetActive(canStop);
        }
    }

    /// <summary>
    /// 設定按鈕的互動狀態及透明度表現
    /// </summary>
    private void SetButtonState(Button button, CanvasGroup cg, bool interactable)
    {
        if (button != null) button.interactable = interactable;
        if (cg != null) cg.alpha = interactable ? 1.0f : 0.5f;
    }

    /// <summary>
    /// 點火按鈕點擊事件
    /// </summary>
    public void OnClickLightFire()
    {
        if (hasStoppedFire && hasServed) return; // 這一輪已經盛飯+熄火，先不能重新點火

        if (state == KitchenState.Unlit || state == KitchenState.Extinguished)
        {
            state = KitchenState.Warming;
            fuelLevel = 40f; // 點火後預設給予 40 燃料

            // 先給予基礎火力，避免 Update 立即判定小於 16 而熄滅
            fireLevel = 20f;

            // 開始新一輪煮飯，重置上一輪的盛飯／熄火紀錄
            hasServed = false;
            hasStoppedFire = false;

            StartCoroutine(RaiseFireCoroutine(20f)); // 觸發火力進一步上升
            EventManager.SetFlag("kitchen_fire_lit", true);
            UpdateUI();
        }
    }

    /// <summary>
    /// 加柴按鈕點擊事件
    /// </summary>
    public void OnClickAddWood()
    {
        if ((state == KitchenState.Warming || state == KitchenState.Cooking) && !isAddingFire)
        {
            isAddingFire = true;
            fuelLevel = Mathf.Min(100f, fuelLevel + fuelPerAdd); // 補充燃料但不超過 100
            StartCoroutine(RaiseFireCoroutine(firePerAdd)); // 觸發火力上升
        }
    }

    /// <summary>
    /// 火力上升的平滑過渡協程
    /// 改為「增量注入」邏輯，避免與 Update 衰減邏輯產生死循環
    /// </summary>
    /// <param name="amount">這次動作總共要增加的火力總量</param>
    private IEnumerator RaiseFireCoroutine(float amount)
    {
        float addedSoFar = 0f;
        
        // 只要還沒加完，且火力還沒滿，就持續注入
        while (addedSoFar < amount && fireLevel < 100f)
        {
            float step = fireRiseRate * Time.deltaTime;
            
            // 確保這一幀加的量不會超過剩下的總量
            float remaining = amount - addedSoFar;
            float actualStep = Mathf.Min(step, remaining);
            
            fireLevel = Mathf.Min(100f, fireLevel + actualStep);
            addedSoFar += actualStep;
            
            yield return null;
        }
        
        isAddingFire = false; // 注入結束，允許下次加柴
    }

    /// <summary>
    /// 開啟小遊戲介面
    /// </summary>
    public void OpenGame()
    {
        if (kitchenGame != null)
        {
            kitchenGame.SetActive(true);
            UpdateUI(); // 開啟時立即更新一次 UI
        }
    }

    /// <summary>
    /// 開啟廚房煮飯小遊戲視窗
    /// </summary>
    public void OpenKitchenWindow()
    {
        if (kitchenGame != null)
        {
            kitchenGame.SetActive(true);
            UpdateUI();
        }
    }

    /// <summary>
    /// 盛飯按鈕點擊事件
    /// </summary>
    public void OnClickServeFood()
    {
        hasServed = true;

        if (kitchenGame != null)
        {
            kitchenGame.SetActive(false);
        }

        // 透過 GameManager 啟動對話，這會自動處理狀態切換
        if (GameManager.Instance != null)
        {
            GameManager.Instance.CloseKitchenGame(); // 先回到 FreeRoam 並切換地點
            GameManager.Instance.StartDialogue("Scene_ServeFood"); // 接著啟動對話進入 Dialogue 模式
        }
    }

    /// <summary>
    /// 飯煮好的瞬間啟動著火計時器（使用遊戲時間），並亮起灶房 EventDot（黃色警示）
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
        state = KitchenState.Extinguished;
        fireLevel = 0f;
        fuelLevel = 0f;
        cookProgress = 0f; // 避免重新點火後因殘留進度立即又判定為煮好
        hasStoppedFire = true; // 這一輪已經熄火，hasServed 留到下次點火才重置

        fireTimerActive = false;
        fireEventTriggered = false;

        EventManager.SetFlag("kitchen_fire_lit", false);

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
        if (state != KitchenState.Done) return;

        ExtinguishFire();
        EventManager.SetFlag("fire_extinguished", true);
    }

    /// <summary>
    /// 退出按鈕點擊事件，僅隱藏介面，不重置狀態
    /// </summary>
    public void OnClickExit()
    {
        if (kitchenGame != null)
        {
            kitchenGame.SetActive(false);
        }

        // 通知 GameManager 關閉小遊戲並返回三合院
        if (GameManager.Instance != null)
        {
            GameManager.Instance.CloseKitchenGame();
        }
    }
}
