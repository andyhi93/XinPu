using System;
using System.Collections.Generic;
using UnityEngine;
using Yarn.Unity;

public class TimeManager : MonoBehaviour
{
    public static TimeManager Instance { get; private set; }

    [Header("時間設定")]
    [Tooltip("現實世界多少分鐘代表遊戲中的 24 小時。")]
    [SerializeField] private float dayLengthInMinutes = 9f;
    public float DayLengthInMinutes => dayLengthInMinutes;

    [Header("狀態")]
    public bool isGamePaused = false;

    // 當前遊戲時間（秒，範圍 0 到 86400）
    private float currentTimeInSeconds;
    public float CurrentTimeInSeconds => currentTimeInSeconds;
    private int currentBranchIndex = -1;
    private int lastHealthDeductionIndex = -1; // 用來紀錄上一次扣過體力的時辰索引
    private bool hasTriggeredMorningLate = false; // 是否已觸發過「遲到的早餐」事件
    private bool hasTriggeredDinnerLate = false;   // 是否已觸發過「遲到的晚飯」事件

    private int consecutiveDaysWithoutWater = 0; // 連續幾天沒燒水
    private bool pendingNoWaterWarning = false;   // 是否已達 3 天門檻，等隔天早上觸發
    private bool hasTriggeredNoWaterWarning = false; // 今天是否已經觸發過提醒（避免重複）

    private int consecutiveDaysWithoutFeedingPig = 0;     // 連續幾天沒餵豬
    private bool pendingPigEscapeEvent = false;            // 是否已達 2 天門檻，等隔天卯時觸發
    private bool hasTriggeredPigEscapeEvent = false;       // 今天是否已經觸發過豬跑出去事件

    private int consecutiveDaysWithoutFeedingChicken = 0;  // 連續幾天沒餵雞
    private bool pendingChickenEscapeEvent = false;         // 是否已達 2 天門檻，等隔天卯時觸發
    private bool hasTriggeredChickenEscapeEvent = false;    // 今天是否已經觸發過雞跑出去事件

    private bool hasTriggeredSatisfactionCheck = false;    // 11:00 家畜滿足度判定是否已執行過
    private bool hasTriggeredSettlement = false;           // 今天 21:00 結算是否已觸發過

    private int dayNumber = 1; // 第三天會播 Scene_FinalSettlement，之後變成無限模式，繼續用每日小結算

    // 十二地支映射
    private readonly string[] earthlyBranches = {
        "子", "丑", "寅", "卯", "辰", "巳", "午", "未", "申", "酉", "戌", "亥"
    };

    [Header("地支轉盤")]
    [SerializeField] private RectTransform branchDialImage; // 顯示地支的圓形 Image，會依時辰旋轉
    [SerializeField] private bool clockwiseRotation = true; // 旋轉方向，跟美術圖對不上就取消勾選

    // 事件：當時段（時辰）改變時觸發
    public event Action<string> OnTimePeriodChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 初始化遊戲開始時間
        currentTimeInSeconds = 0f;
        UpdateBranchIndex(forceNotify: true);
    }

    private void Update()
    {
        if (isGamePaused) return;

        // 計算每秒現實時間對應多少遊戲秒數
        // 24 小時 * 3600 秒 / (dayLengthInMinutes * 60 秒)
        float gameSecondsPerRealSecond = (24f * 3600f) / (dayLengthInMinutes * 60f);

        currentTimeInSeconds += Time.deltaTime * gameSecondsPerRealSecond;

        // 滿 24 小時重置
        if (currentTimeInSeconds >= 24f * 3600f)
        {
            currentTimeInSeconds -= 24f * 3600f;
            HandleDayRollover();
        }

        CheckForTimeEvents();
        UpdateBranchIndex();
    }

    /// <summary>
    /// 跨日時的每日結算：重置「今天觸發過」的旗標，並結算連續幾天沒燒水。
    /// 不只 Update() 裡的自然跨日會呼叫，手動推進時間（advance_time_minutes 等）跨日也會呼叫到。
    /// </summary>
    private void HandleDayRollover()
    {
        dayNumber++;

        hasTriggeredMorningLate = false;
        hasTriggeredDinnerLate = false;
        hasTriggeredNoWaterWarning = false;
        hasTriggeredPigEscapeEvent = false;
        hasTriggeredChickenEscapeEvent = false;
        hasTriggeredSatisfactionCheck = false;
        hasTriggeredSettlement = false;

        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.ResetDailyIncomeExpense(); // 每日收支歸零，供 Scene_DailySettlement 顯示
        }

        if (KitchenMiniGame.Instance != null)
        {
            KitchenMiniGame.Instance.ResetDailyState(); // 讓新的一天可以重新煮早飯
        }

        if (PigFoodMiniGame.Instance != null)
        {
            PigFoodMiniGame.Instance.ResetDailyState(); // 讓新的一天可以重新煮豬菜
        }

        if (PersimmonMiniGame.Instance != null)
        {
            PersimmonMiniGame.Instance.ResetDailyState(); // 讓新的一天可以重新處理今天的柿子（庫存不受影響，留著等三天結算）
        }

        if (ShoppingMenu.Instance != null)
        {
            ShoppingMenu.Instance.ResetDailyStock(); // 市場明天重新隨機上架
        }

        if (EventManager.HasFlag("water_boiled_today"))
        {
            consecutiveDaysWithoutWater = 0;
        }
        else
        {
            consecutiveDaysWithoutWater++;
            // 只在「剛好達到 3 天」那一刻觸發一次，避免一直沒燒水的話每天都重新排隊提醒
            if (consecutiveDaysWithoutWater == 3)
            {
                pendingNoWaterWarning = true;
            }
        }

        // 必須在 ResetFixedFlags() 之前呼叫，否則 breakfast_delivered／dinner_delivered／collapsed_today
        // 等旗標已經被清成「新的一天」的 false，回復量會永遠判定成「什麼都沒吃到」。
        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.ApplySleepRecovery();
        }

        EventManager.ResetFixedFlags(); // 重置 breakfast_delivered／lunch_delivered／water_boiled_today／chicken_fed／pig_fed 等每日旗標

        Debug.Log($"【TimeManager】===跨日=== 現在是 Day{dayNumber} {FormatClock()}｜連續沒燒水 {consecutiveDaysWithoutWater} 天／沒餵豬 {consecutiveDaysWithoutFeedingPig} 天／沒餵雞 {consecutiveDaysWithoutFeedingChicken} 天｜pendingNoWaterWarning={pendingNoWaterWarning}");
    }

    /// <summary>
    /// 除錯用：把目前時間／天數/暫停狀態/所有「今天觸發過」旗標一次印出來，
    /// 方便對照「為什麼又跳出同一個強制對話」。在 Inspector 右鍵這個元件即可呼叫。
    /// </summary>
    [ContextMenu("Debug: 顯示目前時間狀態")]
    private void DebugPrintState()
    {
        Debug.Log(
            $"【TimeManager除錯】Day{dayNumber} {FormatClock()}｜isGamePaused={isGamePaused}\n" +
            $"hasTriggeredMorningLate={hasTriggeredMorningLate}／hasTriggeredDinnerLate={hasTriggeredDinnerLate}／hasTriggeredNoWaterWarning={hasTriggeredNoWaterWarning}\n" +
            $"hasTriggeredSatisfactionCheck={hasTriggeredSatisfactionCheck}／hasTriggeredPigEscapeEvent={hasTriggeredPigEscapeEvent}／hasTriggeredChickenEscapeEvent={hasTriggeredChickenEscapeEvent}／hasTriggeredSettlement={hasTriggeredSettlement}\n" +
            $"pendingNoWaterWarning={pendingNoWaterWarning}／pendingPigEscapeEvent={pendingPigEscapeEvent}／pendingChickenEscapeEvent={pendingChickenEscapeEvent}\n" +
            $"連續沒燒水={consecutiveDaysWithoutWater}／連續沒餵豬={consecutiveDaysWithoutFeedingPig}／連續沒餵雞={consecutiveDaysWithoutFeedingChicken}"
        );
    }

    /// <summary>
    /// 把 currentTimeInSeconds 轉成好讀的 HH:MM，純粹給 Debug.Log 用。
    /// </summary>
    private string FormatClock()
    {
        int totalMinutes = Mathf.FloorToInt(currentTimeInSeconds / 60f);
        int hour = (totalMinutes / 60) % 24;
        int minute = totalMinutes % 60;
        return $"{hour:00}:{minute:00}";
    }

    private void CheckForTimeEvents()
    {
        // 強制事件只能在 FreeRoam 時跳出來，避免打斷正在開著的小遊戲/選單視窗
        // （灶房選單、商店、豬欄等 GameState.Minigame 不會暫停 isGamePaused，
        // 所以 Update() 仍然會跑到這裡——但這裡直接擋掉，等玩家關掉視窗回到
        // FreeRoam 才補觸發；被擋住的這段時間裡，體力等數值仍會正常累積，
        // 只是「跳出強制對話」這個動作本身延後到安全的時機才做）。
        if (GameManager.Instance != null && GameManager.Instance.currentState != GameState.FreeRoam)
        {
            return;
        }

        // 體力倒下／強撐警告優先權最高：ResourceManager.AdjustStat 只會排隊（pending），
        // 真正開對話一定要等這裡確認目前沒有別的對話在跑（Update 會跑到這裡就代表沒在暫停）才安全觸發。
        if (ResourceManager.Instance != null && ResourceManager.Instance.ConsumePendingCollapse())
        {
            if (GameManager.Instance != null)
            {
                // 已經累倒了，今天就到這裡結束——其他「超時」類事件（早飯/晚飯遲到、沒燒水、
                // 家畜跑出去）今天不用再補觸發了，標記成「今天已經處理過」直接取消。
                hasTriggeredMorningLate = true;
                hasTriggeredDinnerLate = true;
                hasTriggeredNoWaterWarning = true;
                hasTriggeredPigEscapeEvent = true;
                hasTriggeredChickenEscapeEvent = true;
                pendingNoWaterWarning = false;
                pendingPigEscapeEvent = false;
                pendingChickenEscapeEvent = false;

                Debug.Log($"【TimeManager】Day{dayNumber} {FormatClock()} 觸發強制對話：Scene_Collapse");
                GameManager.Instance.ForceStartDialogue("Scene_Collapse");
                return;
            }
        }

        if (ResourceManager.Instance != null && ResourceManager.Instance.ConsumePendingStaminaWarning())
        {
            if (GameManager.Instance != null)
            {
                Debug.Log($"【TimeManager】Day{dayNumber} {FormatClock()} 觸發強制對話：Scene_StaminaWarning");
                GameManager.Instance.ForceStartDialogue("Scene_StaminaWarning");
                return;
            }
        }

        if (!hasTriggeredMorningLate && IsTimeAfter(8, 0))
        {
            // 飯是否煮好、是否已經在灶房盛出、或是否已經端過早飯，三個都要排除才算「真的沒飯吃」：
            // 著火事件處理完後 state 會變成 Extinguished、盛飯瞬間 isCooked 也會變 false，
            // 若只看 is_food_done() 會把「已經盛飯／端過早飯」誤判成「沒煮飯」
            bool isFoodDone = KitchenMiniGame.IsFoodDone();
            bool isFoodServed = KitchenMiniGame.IsFoodServed();
            bool breakfastDelivered = EventManager.HasFlag("breakfast_delivered");

            hasTriggeredMorningLate = true;
            if (!isFoodDone && !isFoodServed && !breakfastDelivered)
            {
                if (GameManager.Instance != null)
                {
                    Debug.Log($"【TimeManager】Day{dayNumber} {FormatClock()} 觸發強制對話：Scene_MorningLate");
                    GameManager.Instance.ForceStartDialogue("Scene_MorningLate");
                    return; // 一次只強制觸發一個對話，避免同一輪檢查裡被後面的事件（例如結算）把這個對話中斷、旗標來不及設定
                }
            }
        }

        if (!hasTriggeredDinnerLate && IsTimeAfter(19, 30))
        {
            bool isFoodDone = KitchenMiniGame.IsFoodDone();
            bool dinnerDelivered = EventManager.HasFlag("dinner_delivered");

            hasTriggeredDinnerLate = true;
            if (!isFoodDone && !dinnerDelivered)
            {
                if (GameManager.Instance != null)
                {
                    Debug.Log($"【TimeManager】Day{dayNumber} {FormatClock()} 觸發強制對話：Scene_DinnerLate");
                    GameManager.Instance.ForceStartDialogue("Scene_DinnerLate");
                    return; // 同上：避免馬上被後面的結算檢查打斷，導致 Scene_DinnerLate 還沒跑到 <<set_flag "dinner_delivered">> 就被中止
                }
            }
        }

        if (!hasTriggeredNoWaterWarning && pendingNoWaterWarning && IsTimeAfter(8, 0))
        {
            hasTriggeredNoWaterWarning = true;
            pendingNoWaterWarning = false;
            if (GameManager.Instance != null)
            {
                Debug.Log($"【TimeManager】Day{dayNumber} {FormatClock()} 觸發強制對話：Scene_NoWaterWarning");
                GameManager.Instance.ForceStartDialogue("Scene_NoWaterWarning");
                return; // 同上：一次只強制觸發一個對話
            }
        }

        // 家畜滿足度：每天 11:00 判定，沒餵分別扣 -10，連續兩天沒餵才在隔天卯時觸發跑出去事件
        if (!hasTriggeredSatisfactionCheck && IsTimeAfter(11, 0))
        {
            hasTriggeredSatisfactionCheck = true;

            if (EventManager.HasFlag("pig_fed"))
            {
                consecutiveDaysWithoutFeedingPig = 0;
            }
            else
            {
                consecutiveDaysWithoutFeedingPig++;
                ResourceManager.AdjustStat("pigSatisfaction", -10f);
                // 同上：只在剛好達到 2 天那一刻排隊，避免一直沒餵的話每天都再排一次
                if (consecutiveDaysWithoutFeedingPig == 2)
                {
                    pendingPigEscapeEvent = true;
                }
            }

            if (EventManager.HasFlag("chicken_fed"))
            {
                consecutiveDaysWithoutFeedingChicken = 0;
            }
            else
            {
                consecutiveDaysWithoutFeedingChicken++;
                ResourceManager.AdjustStat("chickenSatisfaction", -10f);
                // 同上：只在剛好達到 2 天那一刻排隊，避免一直沒餵的話每天都再排一次
                if (consecutiveDaysWithoutFeedingChicken == 2)
                {
                    pendingChickenEscapeEvent = true;
                }
            }
        }

        // 注意：上限 11:00 是為了避免「剛在當天 11 點判定出 pending」就在同一刻被當成隔天卯時觸發；
        // 兩個檢查同屬一次 CheckForTimeEvents()，當天 11:00 那一刻 IsTimeAfter(11,0) 已成立，正好把它擋住，
        // 直到隔天卯時（5:00~10:59 的窗口）才會真正觸發。
        if (!hasTriggeredPigEscapeEvent && pendingPigEscapeEvent && IsTimeAfter(5, 0) && !IsTimeAfter(11, 0))
        {
            hasTriggeredPigEscapeEvent = true;
            pendingPigEscapeEvent = false;
            if (GameManager.Instance != null)
            {
                Debug.Log($"【TimeManager】Day{dayNumber} {FormatClock()} 觸發強制對話：Scene_AnimalEscape");
                GameManager.Instance.ForceStartDialogue("Scene_AnimalEscape");
                return; // 同上：一次只強制觸發一個對話
            }
        }

        if (!hasTriggeredChickenEscapeEvent && pendingChickenEscapeEvent && IsTimeAfter(5, 0) && !IsTimeAfter(11, 0))
        {
            hasTriggeredChickenEscapeEvent = true;
            pendingChickenEscapeEvent = false;
            if (GameManager.Instance != null)
            {
                Debug.Log($"【TimeManager】Day{dayNumber} {FormatClock()} 觸發強制對話：Scene_ChickenEscape");
                GameManager.Instance.ForceStartDialogue("Scene_ChickenEscape");
                return; // 同上：一次只強制觸發一個對話
            }
        }

        // 每天 21:00（亥時）結算：每三天（Day3、6、9...）觸發一次週期結算（地租田賦／賣柿餅／賣豬），其餘是每日小結算
        if (!hasTriggeredSettlement && IsTimeAfter(21, 0))
        {
            hasTriggeredSettlement = true;
            if (GameManager.Instance != null)
            {
                string settlementNode = IsPeriodicSettlementDay() ? "Scene_FinalSettlement" : "Scene_DailySettlement";
                Debug.Log($"【TimeManager】Day{dayNumber} {FormatClock()} 觸發強制對話：{settlementNode}");
                GameManager.Instance.ForceStartDialogue(settlementNode);
            }
        }
    }

    private void UpdateBranchIndex(bool forceNotify = false)
    {
        float currentHour = currentTimeInSeconds / 3600f;

        // 地支計算邏輯：
        // 子時是 23:00 - 01:00
        // 丑時是 01:00 - 03:00，以此類推
        // 我們將時間偏移 1 小時以正確對齊每兩小時一個的時段
        int newIndex = Mathf.FloorToInt((currentHour + 1f) / 2f) % 12;

        if (newIndex != currentBranchIndex || forceNotify)
        {
            // 如果不是初始強制通知，且索引真的改變了，則扣除體力
            if (!forceNotify && currentBranchIndex != -1 && newIndex != currentBranchIndex)
            {
                if (ResourceManager.Instance != null)
                {
                    ResourceManager.AdjustStat("health", -8f);
                }
            }

            currentBranchIndex = newIndex;
            UpdateBranchDialRotation();
            OnTimePeriodChanged?.Invoke(GetCurrentTime());
            Debug.Log($"時段已切換：{GetCurrentTime()}時");
        }
    }

    /// <summary>
    /// 地支索引轉換成起始小時（子(0)->23, 丑(1)->1, 寅(2)->3, 卯(3)->5...）
    /// </summary>
    private static int BranchIndexToStartHour(int branchIndex)
    {
        return (branchIndex * 2 - 1 + 24) % 24;
    }

    /// <summary>
    /// 依目前地支旋轉圓形圖。只在地支切換的瞬間呼叫，不需要每幀更新。
    /// rotation = 該地支起始小時 × 30 度（12 小時對應 360 度）。
    /// </summary>
    private void UpdateBranchDialRotation()
    {
        if (branchDialImage == null) return;

        float angle = BranchIndexToStartHour(currentBranchIndex) * 30f;
        if (clockwiseRotation) angle = -angle;

        branchDialImage.localEulerAngles = new Vector3(0f, 0f, angle);
    }

    /// <summary>
    /// 回傳目前的遊戲時辰名稱（例如：卯時）。
    /// </summary>
    public string GetCurrentTime()
    {
        if (currentBranchIndex < 0 || currentBranchIndex >= earthlyBranches.Length)
            return "未知";

        return earthlyBranches[currentBranchIndex];
    }

    [YarnCommand("pause_time")]
    public void Pause()
    {
        isGamePaused = true;
        // 注意：這裡只設定布林值，HUD 仍可運作，對話會依據此狀態自行檢查
    }

    [YarnCommand("resume_time")]
    public void Resume()
    {
        isGamePaused = false;
    }

    [ContextMenu("Advance 1 Hour")]
    public void AdvanceOneHour()
    {
        // 這兩個是純手動測試用的工具（Context Menu／無對應 Yarn 指令在用），
        // 對話強制觸發中（isGamePaused）時直接擋掉，避免在對話卡著的時候連點，
        // 偷偷跨過好幾天的午夜，導致 hasTriggered* 系列旗標在背景被重置好幾輪，
        // 對話一播完馬上又因為「新的一天」而重新觸發一次，看起來像是同一天無限重複。
        if (isGamePaused)
        {
            Debug.LogWarning("【TimeManager】目前有強制對話正在進行（isGamePaused），暫不推進時間，請先讓對話跑完。");
            return;
        }

        // 手動推進一個小時（3600 秒）
        currentTimeInSeconds += 3600f;
        if (currentTimeInSeconds >= 24f * 3600f)
        {
            currentTimeInSeconds -= 24f * 3600f;
            HandleDayRollover();
        }
        UpdateBranchIndex();

        if (KitchenMiniGame.Instance != null)
        {
            KitchenMiniGame.Instance.AdvanceByGameMinutes(60f);
        }

        Debug.Log("【TimeManager】已手動推進 1 小時");
    }

    [YarnCommand("advance_time")]
    public void AdvanceTime()
    {
        // 同上：避免在強制對話進行中被連續呼叫，背景偷偷跨過好幾天
        if (isGamePaused)
        {
            Debug.LogWarning("【TimeManager】目前有強制對話正在進行（isGamePaused），暫不推進時間，請先讓對話跑完。");
            return;
        }

        // 手動推進一個時段（2 小時）
        currentTimeInSeconds += 2f * 3600f;
        if (currentTimeInSeconds >= 24f * 3600f)
        {
            currentTimeInSeconds -= 24f * 3600f;
            HandleDayRollover();
        }
        UpdateBranchIndex();

        if (KitchenMiniGame.Instance != null)
        {
            KitchenMiniGame.Instance.AdvanceByGameMinutes(120f);
        }
    }

    /// <summary>
    /// 透過 Yarn Spinner 推進指定的分鐘數
    /// </summary>
    [YarnCommand("advance_time_minutes")]
    public static void AdvanceTimeMinutes(float minutes)
    {
        if (Instance == null) return;
        Instance.currentTimeInSeconds += minutes * 60f;
        if (Instance.currentTimeInSeconds >= 24f * 3600f)
        {
            Instance.currentTimeInSeconds -= 24f * 3600f;
            Instance.HandleDayRollover();
        }
        Instance.UpdateBranchIndex();

        // 灶上的火不會因為玩家在做別的事而停下來，這段跳過的遊戲時間要讓廚房小遊戲補跑對應的火力／燃料／烹煮進度
        if (KitchenMiniGame.Instance != null)
        {
            KitchenMiniGame.Instance.AdvanceByGameMinutes(minutes);
        }
    }

    /// <summary>
    /// 透過 Yarn Spinner 設定時間
    /// 支援輸入時辰（如 "卯" 或 "卯時"）或 24 小時制數字（如 "5"、"14"）
    /// </summary>
    [YarnCommand("set_time")]
    public static void SetTime(string timeInput)
    {
        if (Instance == null) return;

        // 嘗試解析為數字 (24小時制)
        if (int.TryParse(timeInput, out int hour))
        {
            hour = Mathf.Clamp(hour, 0, 23);
            Instance.currentTimeInSeconds = hour * 3600f;
            Debug.Log($"【TimeManager】時間已設定為 {hour}:00");
        }
        else
        {
            // 嘗試解析為時辰
            string branchStr = timeInput.Replace("時", "").Trim();
            int branchIndex = Array.IndexOf(Instance.earthlyBranches, branchStr);

            if (branchIndex != -1)
            {
                // 計算該時辰的起始小時
                int startHour = BranchIndexToStartHour(branchIndex);
                Instance.currentTimeInSeconds = startHour * 3600f;
                Debug.Log($"【TimeManager】時間已設定為 {branchStr}時 ({startHour}:00)");
            }
            else
            {
                Debug.LogWarning($"【TimeManager】無法解析的時間格式：{timeInput}，請輸入 0~23 的數字或十二地支（例如：卯）。");
                return;
            }
        }

        Instance.UpdateBranchIndex(forceNotify: true);
    }

    /// <summary>
    /// 結算用：手動跨到隔天卯時（會觸發一次 HandleDayRollover 的每日重置，
    /// 不等待 Update() 自然跨過 24:00，因為結算發生在 21:00，時間是往「回」跳的）。
    /// </summary>
    [YarnCommand("advance_to_next_day")]
    public static void AdvanceToNextDay()
    {
        if (Instance == null) return;
        Instance.HandleDayRollover();
        SetTime("卯");
    }

    /// <summary>
    /// 回傳當前一天的進度 (0.0 到 1.0)
    /// </summary>
    public float GetDayProgress()
    {
        return currentTimeInSeconds / (24f * 3600f);
    }

    /// <summary>
    /// 供 Yarn 腳本查詢目前遊戲時間是否已經到達或超過指定的時:分。
    /// 用法：<<if is_time_after(12, 0)>>
    /// </summary>
    [YarnFunction("is_time_after")]
    public static bool IsTimeAfter(int hour, int minute)
    {
        if (Instance == null) return false;
        return Instance.currentTimeInSeconds >= hour * 3600f + minute * 60f;
    }

    /// <summary>
    /// 供 Yarn 腳本查詢目前是第幾天。
    /// </summary>
    [YarnFunction("get_day_number")]
    public static float GetDayNumber()
    {
        return Instance != null ? Instance.dayNumber : 1;
    }

    /// <summary>
    /// 供 Yarn 腳本查詢今天是不是每三天一次的週期結算日（Day3、6、9...），
    /// 該跳 Scene_FinalSettlement 還是 Scene_DailySettlement 都用這個判斷，避免在 .yarn 裡寫 % 運算。
    /// 用法：<<if is_periodic_settlement_day()>>
    /// </summary>
    [YarnFunction("is_periodic_settlement_day")]
    public static bool IsPeriodicSettlementDay()
    {
        return Instance != null && Instance.dayNumber % 3 == 0;
    }
}
