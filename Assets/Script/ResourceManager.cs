using System;
using System.Collections.Generic;
using UnityEngine;
using Yarn.Unity;

public class ResourceManager : MonoBehaviour
{
    public static ResourceManager        Instance { get; private set; }

    public enum FavorLevel { 差, 普通, 好 }

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

    [Header("Yarn 整合設定")]
    [SerializeField] private string healthExhaustedNode = "HealthExhausted";
    private DialogueRunner dialogueRunner;

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

    private void Start()
    {
        // Unity 6 建議使用 FindFirstObjectByType
        dialogueRunner = FindFirstObjectByType<DialogueRunner>();
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
                Instance.health = Mathf.Clamp(Instance.health + intAmount, 0, 100);
                Instance.NotifyChange("health", Instance.health);
                if (Instance.health <= 0) Instance.HandleHealthExhausted();
                break;
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

    private void HandleHealthExhausted()
    {
        Debug.Log("體力歸零！觸發強制休息。");
        if (dialogueRunner != null && !dialogueRunner.IsDialogueRunning)
        {
            dialogueRunner.StartDialogue(healthExhaustedNode);
        }

        // 邏輯副作用：自動跳過一個時段（2 小時）
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.AdvanceTime();
        }
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

    // --- 第三天結算 ---

    private const int FinalRentAndTax = 120; // 地租 80 + 田賦 40

    /// <summary>
    /// 計算柿餅與賣豬收入，扣除地租田賦後直接套用到 money。
    /// </summary>
    [YarnCommand("calculate_final_settlement")]
    public static void CalculateFinalSettlement()
    {
        if (Instance == null) return;

        Instance.finalPersimmonIncome = Instance.GetIngredientCount("persimmon") * 3;

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
    /// Demo 結局：目前尚無正式結局畫面，先暫停遊戲時間並記錄 Log。
    /// </summary>
    [YarnCommand("trigger_ending")]
    public static void TriggerEnding()
    {
        Debug.Log("【結算】Demo 三天結束，等待後續補上正式的結局畫面。");
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.isGamePaused = true;
        }
    }
}
