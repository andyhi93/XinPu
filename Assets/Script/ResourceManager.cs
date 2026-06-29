using System;
using System.Collections.Generic;
using UnityEngine;
using Yarn.Unity;

public class ResourceManager : MonoBehaviour
{
    public static ResourceManager        Instance { get; private set; }

    public enum FavorLevel { 差, 普通, 好 }

    // 體力三階段：Normal 正常／Tired 疲憊（30 以下）／Critical 強撐（10 以下）／Collapsed 倒下（歸零）
    public enum StaminaState { Normal, Tired, Critical, Collapsed }

    [Header("基礎數值")]
    [SerializeField] private int health = 100;
    [SerializeField] private int mood = 100;
    [SerializeField] private int money = 150;
    [SerializeField] private FavorLevel motherInLawFavor = FavorLevel.普通;

    [Header("預留數值 (原型開發用)")]
    public float persimmon_progress = 0f;

    [Header("家畜滿足度")]
    public int pigSatisfaction = 80;     // 0-100，影響賣豬收入
    public int chickenSatisfaction = 80; // 0-100，影響雞蛋產量

    // 每日收支追蹤（供 Scene_DailySettlement 顯示，跨日由 ResetDailyIncomeExpense 清空）
    private int dailyIncome = 0;
    private int dailyExpense = 0;

    // 第三天結算結果（供 Scene_FinalSettlement 顯示）
    private int finalPersimmonIncome = 0;
    private int finalPigIncome = 0;
    private int finalBalance = 0;

    // 體力強撐／倒下事件：等 TimeManager 確認目前沒有對話在跑（isGamePaused=false）才安全觸發，
    // 避免在別的場景正在執行 <<adjust_stat>> 指令的當下，直接搶斷它去開新對話。
    private bool pendingStaminaWarning = false;
    private bool pendingCollapse = false;

    // 事件：提供給 UI 或其他系統監聽數值變化
    public event Action<string, object> OnStatChanged;

    // 事件：好感度變化
    public event Action<string, float> OnFavorabilityChanged;

    // 新增：好感度系統
    private Dictionary<string, float> npcFavorability = new Dictionary<string, float>()
    {
        { "grandfather", 50f },
        { "grandmother", 50f },
        { "husband", 50f }
    };

    // 新增：食材庫存系統（地瓜永遠無限，不在此追蹤數量；也用來存放柴薪等一般消耗品）
    private Dictionary<string, int> ingredientStock = new Dictionary<string, int>()
    {
        { "rice", 6 },
        { "oil", 6 },
        { "salt", 8 },
        { "tofu", 1 },
        { "driedFish", 1 },
        { "eggs", 1 },
        { "firewood", 10 },
        { "water", 0 },
        { "riceBran", 4 },
        { "cloth", 0 },
        { "medicine", 0 },
        { "pickledVeg", 0 },
        { "persimmon", 0 },
        { "sweetPotatoLeaves", 4 }
    };

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// 調整指定數值，會自動限制在範圍內。
    /// </summary>
    [YarnCommand("adjust_stat")]
    public static void AdjustStat(string statName, float amount)
    {
        if (Instance == null) return;

        int intAmount = Mathf.RoundToInt(amount);

        switch (statName.ToLower())
        {
            case "health":
            {
                StaminaState stateBefore = Instance.GetStaminaState();

                // 已經疲憊（含強撐／倒下）時，任何會消耗體力的動作都多扣 2 點
                int healthDelta = intAmount;
                if (intAmount < 0 && stateBefore != StaminaState.Normal)
                {
                    healthDelta -= 2;
                }

                Instance.health = Mathf.Clamp(Instance.health + healthDelta, 0, 100);
                Instance.NotifyChange("health", Instance.health);

                StaminaState stateAfter = Instance.GetStaminaState();
                if (stateAfter == StaminaState.Collapsed && !EventManager.HasFlag("collapsed_today"))
                {
                    Instance.pendingCollapse = true;
                }
                else if (stateAfter == StaminaState.Critical && !EventManager.HasFlag("stamina_warning_shown"))
                {
                    Instance.pendingStaminaWarning = true;
                }
                break;
            }
            case "mood":
                Instance.mood = Mathf.Clamp(Instance.mood + intAmount, 0, 100);
                Instance.NotifyChange("mood", Instance.mood);
                break;
            case "money":
                if (intAmount > 0) Instance.dailyIncome += intAmount;
                else if (intAmount < 0) Instance.dailyExpense += -intAmount;
                Instance.money += intAmount;
                Instance.NotifyChange("money", Instance.money);
                break;
            case "mother_in_law_favor":
                // 原型版簡單處理好感度循環
                int current = (int)Instance.motherInLawFavor;
                Instance.motherInLawFavor = (FavorLevel)Mathf.Clamp(current + intAmount, 0, 2);
                Instance.NotifyChange("mother_in_law_favor", Instance.motherInLawFavor);
                break;
            case "pigsatisfaction":
                Instance.pigSatisfaction = Mathf.Clamp(Instance.pigSatisfaction + intAmount, 0, 100);
                Instance.NotifyChange("pigSatisfaction", Instance.pigSatisfaction);
                break;
            case "chickensatisfaction":
                Instance.chickenSatisfaction = Mathf.Clamp(Instance.chickenSatisfaction + intAmount, 0, 100);
                Instance.NotifyChange("chickenSatisfaction", Instance.chickenSatisfaction);
                break;
            default:
                Debug.LogWarning($"在 AdjustStat 中找不到數值： {statName}");
                break;
        }
    }

    /// <summary>
    /// 獲取指定數值，供 Yarn 腳本使用。
    /// </summary>
    [YarnFunction("get_stat")]
    public static float GetStat(string statName)
    {
        if (Instance == null) return 0;

        switch (statName.ToLower())
        {
            case "health": return Instance.health;
            case "mood": return Instance.mood;
            case "money": return Instance.money;
            case "mother_in_law_favor": return (int)Instance.motherInLawFavor;
            case "pigsatisfaction": return Instance.pigSatisfaction;
            case "chickensatisfaction": return Instance.chickenSatisfaction;
            default:
                Debug.LogWarning($"【ResourceManager】試圖取得不存在的數值：{statName}");
                return 0f;
        }
    }

    /// <summary>
    /// 透過 Yarn Spinner 修改角色好感度
    /// </summary>
    [YarnCommand("adjust_favorability")]
    public static void AdjustFavorabilityCommand(string npcId, float amount)
    {
        if (Instance != null)
        {
            Instance.ModifyFavorability(npcId, amount);
        }
    }

    /// <summary>
    /// 直接設定指定數值。
    /// </summary>
    [YarnCommand("set_stat")]
    public static void SetStat(string statName, float value)
    {
        if (Instance == null) return;

        int intValue = Mathf.RoundToInt(value);
        switch (statName.ToLower())
        {
            case "health":
                Instance.health = Mathf.Clamp(intValue, 0, 100);
                Instance.NotifyChange("health", Instance.health);
                break;
            case "mood":
                Instance.mood = Mathf.Clamp(intValue, 0, 100);
                Instance.NotifyChange("mood", Instance.mood);
                break;
            case "money":
                Instance.money = intValue;
                Instance.NotifyChange("money", Instance.money);
                break;
            case "mother_in_law_favor":
                Instance.motherInLawFavor = (FavorLevel)Mathf.Clamp(intValue, 0, 2);
                Instance.NotifyChange("mother_in_law_favor", Instance.motherInLawFavor);
                break;
        }
    }

    /// <summary>
    /// 依目前體力判斷三階段狀態。
    /// </summary>
    public StaminaState GetStaminaState()
    {
        if (health <= 0) return StaminaState.Collapsed;
        if (health <= 10) return StaminaState.Critical;
        if (health <= 30) return StaminaState.Tired;
        return StaminaState.Normal;
    }

    /// <summary>
    /// 供 TimeManager 在確認目前沒有對話在跑（安全時機）才呼叫，消耗掉「待觸發」的倒下事件。
    /// 回傳 true 表示這次真的要去開 Scene_Collapse。
    /// </summary>
    public bool ConsumePendingCollapse()
    {
        if (!pendingCollapse) return false;
        pendingCollapse = false;
        return true;
    }

    /// <summary>
    /// 同上，消耗「待觸發」的體力強撐警告。
    /// </summary>
    public bool ConsumePendingStaminaWarning()
    {
        if (!pendingStaminaWarning) return false;
        pendingStaminaWarning = false;
        return true;
    }

    /// <summary>
    /// 跨日（睡一晚）回復體力／心情，依今天有沒有正常吃早晚飯、有沒有倒下決定回復量。
    /// 必須在 TimeManager.HandleDayRollover() 重置每日旗標「之前」呼叫，
    /// 否則 breakfast_delivered／dinner_delivered 等旗標已經被清成 false，讀不到「今天」的紀錄。
    /// </summary>
    public void ApplySleepRecovery()
    {
        bool hadBreakfast = EventManager.HasFlag("breakfast_delivered") && !EventManager.HasFlag("morning_late");
        bool hadDinner = EventManager.HasFlag("dinner_delivered") && !EventManager.HasFlag("dinner_late_triggered");
        bool collapsedToday = EventManager.HasFlag("collapsed_today");

        int healthRecovery;
        int moodRecovery;

        if (collapsedToday)
        {
            healthRecovery = 20;
            moodRecovery = 5;
        }
        else if (hadBreakfast && hadDinner)
        {
            healthRecovery = 60;
            moodRecovery = 30;
        }
        else if (hadBreakfast || hadDinner)
        {
            healthRecovery = 40;
            moodRecovery = 20;
        }
        else
        {
            healthRecovery = 25;
            moodRecovery = 10;
        }

        health = Mathf.Clamp(health + healthRecovery, 0, 100);
        mood = Mathf.Clamp(mood + moodRecovery, 0, 100);
        NotifyChange("health", health);
        NotifyChange("mood", mood);

        Debug.Log($"【體力】睡眠回復：體力 +{healthRecovery}／心情 +{moodRecovery}（早飯準時:{hadBreakfast}／晚飯準時:{hadDinner}／昨天倒下:{collapsedToday}）");
    }

    private void NotifyChange(string statName, object value)
    {
        OnStatChanged?.Invoke(statName, value);
        Debug.Log($"數值變更：{statName} = {value}");
    }

    /// <summary>
    /// 供 UI 檢查是否能進行「忍耐型」選項（心情需求）。
    /// </summary>
    public bool CanPerformPatienceAction()
    {
        return mood >= 30;
    }

    // --- 新增：好感度系統方法 ---

    /// <summary>
    /// 取得指定角色的好感度
    /// </summary>
    public float GetFavorability(string npcId)
    {
        if (npcFavorability.TryGetValue(npcId, out float favor))
        {
            return favor;
        }
        else
        {
            Debug.LogWarning($"【ResourceManager】試圖取得不存在的 NPC 好感度：{npcId}");
            return 0f;
        }
    }

    /// <summary>
    /// 修改指定角色的好感度
    /// </summary>
    public void ModifyFavorability(string npcId, float amount)
    {
        if (npcFavorability.ContainsKey(npcId))
        {
            float currentFavor = npcFavorability[npcId];
            float newFavor = Mathf.Clamp(currentFavor + amount, 0f, 100f);
            npcFavorability[npcId] = newFavor;

            Debug.Log($"【ResourceManager】{npcId} 好感度變化：{currentFavor} -> {newFavor}");
            OnFavorabilityChanged?.Invoke(npcId, newFavor);
        }
        else
        {
            Debug.LogWarning($"【ResourceManager】試圖修改不存在的 NPC 好感度：{npcId}");
        }
    }

    /// <summary>
    /// 取得所有角色的好感度資料 (回傳複本)
    /// </summary>
    public Dictionary<string, float> GetAllFavorability()
    {
        // 回傳一個新的 Dictionary 避免外部直接修改內部資料
        return new Dictionary<string, float>(npcFavorability);
    }

    // --- 新增：食材庫存系統方法 ---

    /// <summary>
    /// 取得指定食材目前的庫存數量
    /// </summary>
    public int GetIngredientCount(string key)
    {
        if (ingredientStock.TryGetValue(key, out int amount))
        {
            return amount;
        }

        Debug.LogWarning($"【ResourceManager】試圖取得不存在的食材庫存：{key}");
        return 0;
    }

    /// <summary>
    /// 消耗指定數量的食材，會自動限制在 0 以上
    /// </summary>
    public void ConsumeIngredient(string key, int amount)
    {
        if (!ingredientStock.ContainsKey(key))
        {
            Debug.LogWarning($"【ResourceManager】試圖消耗不存在的食材庫存：{key}");
            return;
        }

        ingredientStock[key] = Mathf.Max(0, ingredientStock[key] - amount);
    }

    /// <summary>
    /// 透過 Yarn Spinner 調整物品數量（可正可負），會自動限制在 0 以上。
    /// 用法：<<adjust_item "firewood" -1>>
    /// </summary>
    [YarnCommand("adjust_item")]
    public static void AdjustItem(string itemKey, int amount)
    {
        if (Instance == null) return;

        if (!Instance.ingredientStock.ContainsKey(itemKey))
        {
            Debug.LogWarning($"【ResourceManager】試圖調整不存在的食材庫存：{itemKey}");
            return;
        }

        Instance.ingredientStock[itemKey] = Mathf.Max(0, Instance.ingredientStock[itemKey] + amount);
    }

    /// <summary>
    /// 透過 Yarn Spinner 取得物品數量。
    /// 用法：<<if get_item("water") > 0>>
    /// </summary>
    [YarnFunction("get_item")]
    public static int GetItem(string itemKey)
    {
        if (Instance == null) return 0;
        return Instance.GetIngredientCount(itemKey);
    }

    private int lastEggCount = 0;

    /// <summary>
    /// 收蛋：依照 chickenSatisfaction 決定今天的產蛋數量，加進雞蛋庫存。
    /// 80 以上 2 顆，50~79 1 顆，50 以下 0 顆。
    /// </summary>
    [YarnCommand("collect_eggs")]
    public static void CollectEggs()
    {
        if (Instance == null) return;

        int count;
        if (Instance.chickenSatisfaction >= 80) count = 2;
        else if (Instance.chickenSatisfaction >= 50) count = 1;
        else count = 0;

        Instance.lastEggCount = count;

        if (count > 0)
        {
            AdjustItem("eggs", count);
        }
    }

    /// <summary>
    /// 取得最近一次 collect_eggs 收到的蛋數，供 Yarn 文字顯示。
    /// </summary>
    [YarnFunction("get_last_egg_count")]
    public static int GetLastEggCount() => Instance != null ? Instance.lastEggCount : 0;

    // --- 每日收支結算 ---

    [YarnFunction("get_daily_income")]
    public static int GetDailyIncome() => Instance != null ? Instance.dailyIncome : 0;

    [YarnFunction("get_daily_expense")]
    public static int GetDailyExpense() => Instance != null ? Instance.dailyExpense : 0;

    /// <summary>
    /// 跨日時呼叫：清空當日收支累計，供 TimeManager 在 advance_to_next_day 時呼叫。
    /// </summary>
    public void ResetDailyIncomeExpense()
    {
        dailyIncome = 0;
        dailyExpense = 0;
    }

    /// <summary>
    /// 顯示當日收支摘要（目前先以 Log 呈現，尚無對應 UI）。
    /// </summary>
    [YarnCommand("show_daily_summary")]
    public static void ShowDailySummary()
    {
        if (Instance == null) return;
        Debug.Log($"【每日結算】收入 {Instance.dailyIncome} 元／支出 {Instance.dailyExpense} 元／目前身上 {Instance.money} 元。");
    }

    // --- 每三天一次的週期結算（地租田賦、賣柿餅、賣豬）---

    private const int FinalRentAndTax = 120; // 地租 80 + 田賦 40

    /// <summary>
    /// 計算柿餅與賣豬收入，扣除地租田賦後直接套用到 money。
    /// 每三天觸發一次（見 TimeManager），柿餅賣掉後要把庫存清掉，
    /// 否則下一輪三天結算會把同一批庫存再賣一次，變成無限刷錢。
    /// </summary>
    [YarnCommand("calculate_final_settlement")]
    public static void CalculateFinalSettlement()
    {
        if (Instance == null) return;

        int persimmonCount = Instance.GetIngredientCount("persimmon");
        Instance.finalPersimmonIncome = persimmonCount * 3;
        AdjustItem("persimmon", -persimmonCount);

        if (Instance.pigSatisfaction >= 80) Instance.finalPigIncome = 200;
        else if (Instance.pigSatisfaction >= 60) Instance.finalPigIncome = 150;
        else if (Instance.pigSatisfaction >= 40) Instance.finalPigIncome = 100;
        else Instance.finalPigIncome = 60;

        int netChange = Instance.finalPersimmonIncome + Instance.finalPigIncome - FinalRentAndTax;
        Instance.finalBalance = Instance.money + netChange;

        AdjustStat("money", netChange);
    }

    [YarnFunction("get_persimmon_income")]
    public static int GetPersimmonIncome() => Instance != null ? Instance.finalPersimmonIncome : 0;

    [YarnFunction("get_pig_income")]
    public static int GetPigIncome() => Instance != null ? Instance.finalPigIncome : 0;

    [YarnFunction("get_final_balance")]
    public static int GetFinalBalance() => Instance != null ? Instance.finalBalance : 0;

    [YarnFunction("get_final_deficit")]
    public static int GetFinalDeficit() => Instance != null ? Mathf.Max(0, -Instance.finalBalance) : 0;

    /// <summary>
    /// 結算用：賣柿餅收入 + 地租田賦扣款。
    /// 原本是每三天一次的週期結算（連同賣豬，見上面的 CalculateFinalSettlement），
    /// 現在遊戲改成只玩一天，固定在 EndingUIManager 顯示結算面板時呼叫一次；
    /// 賣豬代表的是長期飼養收益，跟單日流程不搭，這裡不計算。
    /// 沿用同一套 finalPersimmonIncome／get_persimmon_income，柿餅賣掉後要清空庫存，避免重複結算。
    /// </summary>
    public static void CalculateEndingFinance()
    {
        if (Instance == null) return;

        int persimmonCount = Instance.GetIngredientCount("persimmon");
        Instance.finalPersimmonIncome = persimmonCount * 3;
        AdjustItem("persimmon", -persimmonCount);
        AdjustStat("money", Instance.finalPersimmonIncome);

        AdjustStat("money", -FinalRentAndTax);
    }

    /// <summary>
    /// 供 EndingUIManager 顯示固定的地租田賦金額（地租 80 + 田賦 40）。
    /// </summary>
    public static int GetRentAndTax() => FinalRentAndTax;

    /// <summary>
    /// 每三天一次的週期結算播完時呼叫，目前只記錄 Log（尚無額外畫面），
    /// 不會停住遊戲——遊戲會無限循環下去，每三天都會再播一次 Scene_FinalSettlement。
    /// </summary>
    [YarnCommand("trigger_ending")]
    public static void TriggerEnding()
    {
        Debug.Log($"【結算】Day{TimeManager.GetDayNumber()} 三天週期結算播完，繼續無限模式。");
    }
}
