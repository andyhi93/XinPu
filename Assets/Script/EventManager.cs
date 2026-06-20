using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Yarn.Unity;

public class EventManager : MonoBehaviour
{
    public static EventManager Instance { get; private set; }

    [Serializable]
    public enum EventTier { Tier1_Yellow = 1, Tier2_Orange = 2, Tier3_Red = 3 }

    [Serializable]
    public class StatChange
    {
        public string statName;
        public float amount;
    }

    [Serializable]
    public class GameEvent
    {
        public string id;
        public string location;          // "HEYUAN", "LANGDAO", "ANY"
        public EventTier tier;
        public float warningDelay;       // 幾秒後發出警告
        public float resolveWindow;      // 警告後多久結算
        public string yarnNode;          // 玩家在場時觸發的 Yarn 節點
        public bool triggerOnce;         // 是否只觸發一次
        public List<StatChange> ignoredEffects = new List<StatChange>();
        
        [HideInInspector] public bool hasTriggered = false;
        [HideInInspector] public bool isProcessing = false;
    }

    [Header("事件資料庫")]
    [SerializeField] private string jsonPath = "Data/events";
    private List<GameEvent> eventDatabase = new List<GameEvent>();

    [Header("狀態 (開發檢視用)")]
    [SerializeField] private List<string> pendingIgnoredEventIDs = new List<string>();

    [Serializable]
    public class FlagEntry
    {
        public string key;
        public bool value;
    }

    [Header("旗標 (開發檢視用)")]
    [SerializeField] private List<FlagEntry> flagsDebugView = new List<FlagEntry>();

    // 固有旗標：每天都會用到的旗標，在這裡列出來才能一眼看出有哪些。
    // 其他臨時/一次性的旗標可以直接用 <<set_flag>> 動態新增，不用列在這裡。
    private static readonly string[] FixedFlags = new string[]
    {
        "breakfast_delivered",
        "lunch_delivered",
    };

    private Dictionary<string, bool> flags = new Dictionary<string, bool>();

    private DialogueRunner dialogueRunner;

    [Serializable]
    private class EventDataWrapper
    {
        public List<GameEvent> events;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        ResetFixedFlags();
        LoadEventsFromJson();
    }

    /// <summary>
    /// 將固有旗標 (FixedFlags) 重置為 false。供其他系統呼叫，例如新的一天開始時。
    /// </summary>
    public static void ResetFixedFlags()
    {
        if (Instance == null) return;
        foreach (string key in FixedFlags)
        {
            Instance.flags[key] = false;
        }
        Instance.SyncFlagsDebugView();
    }

    private void LoadEventsFromJson()
    {
        TextAsset jsonFile = Resources.Load<TextAsset>(jsonPath);
        if (jsonFile == null)
        {
            Debug.LogError($"【事件系統】找不到 JSON 檔案：Resources/{jsonPath}");
            return;
        }

        try
        {
            EventDataWrapper wrapper = JsonUtility.FromJson<EventDataWrapper>(jsonFile.text);
            if (wrapper != null && wrapper.events != null)
            {
                foreach (var ev in wrapper.events)
                {
                    if (ValidateEvent(ev))
                    {
                        eventDatabase.Add(ev);
                    }
                }
                Debug.Log($"【事件系統】成功讀取 {eventDatabase.Count} 個有效事件。");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"【事件系統】解析 JSON 失敗：{e.Message}");
        }
    }

    private bool ValidateEvent(GameEvent ev)
    {
        bool isValid = true;

        if (string.IsNullOrEmpty(ev.id))
        {
            Debug.LogWarning("【事件系統驗證】事件 ID 為空！");
            isValid = false;
        }
        
        if (string.IsNullOrEmpty(ev.location))
        {
            Debug.LogWarning($"【事件系統驗證】事件 {ev.id} 的地點 (location) 為空！");
            isValid = false;
        }

        if (ev.warningDelay < 0)
        {
            Debug.LogWarning($"【事件系統驗證】事件 {ev.id} 的 warningDelay ({ev.warningDelay}) 不能小於 0！");
            isValid = false;
        }

        if (ev.resolveWindow <= 0)
        {
            Debug.LogWarning($"【事件系統驗證】事件 {ev.id} 的 resolveWindow ({ev.resolveWindow}) 必須大於 0！");
            isValid = false;
        }

        if ((int)ev.tier < 1 || (int)ev.tier > 3)
        {
            Debug.LogWarning($"【事件系統驗證】事件 {ev.id} 的 tier ({(int)ev.tier}) 應該在 1 到 3 之間！");
        }

        if (string.IsNullOrEmpty(ev.yarnNode))
        {
            Debug.LogWarning($"【事件系統驗證】事件 {ev.id} 的 yarnNode 為空！");
            isValid = false;
        }

        if (ev.ignoredEffects == null || ev.ignoredEffects.Count == 0)
        {
            Debug.LogWarning($"【事件系統驗證】事件 {ev.id} 沒有定義任何 ignoredEffects！");
            // 這可能不算是致命錯誤，視需求而定
        }

        return isValid;
    }

    private void Start()
    {
        dialogueRunner = FindFirstObjectByType<DialogueRunner>();
    }

    /// <summary>
    /// 將事件加入佇列並開始計時。
    /// </summary>
    public void TriggerEvent(string eventID)
    {
        GameEvent ev = eventDatabase.Find(e => e.id == eventID);
        if (ev == null || (ev.triggerOnce && ev.hasTriggered) || ev.isProcessing) return;

        StartCoroutine(EventProcessRoutine(ev));
    }

    private IEnumerator EventProcessRoutine(GameEvent ev)
    {
        ev.isProcessing = true;
        
        // 1. 等待 Warning Delay
        yield return new WaitForSeconds(ev.warningDelay);

        // 2. 發出警告信號 (視覺/聲音)
        TriggerWarningSignal(ev);

        // 3. 等待 Resolve Window
        yield return new WaitForSeconds(ev.resolveWindow);

        // 4. 判定玩家位置與結算
        ResolveEvent(ev);
        
        ev.isProcessing = false;
        if (ev.triggerOnce) ev.hasTriggered = true;
    }

    private void TriggerWarningSignal(GameEvent ev)
    {
        Debug.Log($"【事件警告】{ev.id} (Tier {(int)ev.tier}) 在 {ev.location} 發生！");
        // 通知熱點更新狀態 (例如開始閃爍)
        RefreshHotspots();
    }

    private void ResolveEvent(GameEvent ev)
    {
        bool isPlayerAtLocation = CheckPlayerLocation(ev.location);

        if (isPlayerAtLocation && GameManager.Instance != null)
        {
            // 玩家在場：透過 GameManager 觸發 Yarn 對話
            Debug.Log($"【事件結算】玩家在場，請求啟動對話：{ev.yarnNode}");
            GameManager.Instance.StartDialogue(ev.yarnNode);
        }
        else
        {
            // 玩家不在場：將事件加入「未處理」清單
            Debug.Log($"【事件結算】玩家不在場 ({ev.location})，事件「{ev.id}」進入待處理清單。");
            if (!pendingIgnoredEventIDs.Contains(ev.id))
            {
                pendingIgnoredEventIDs.Add(ev.id);
            }
            RefreshHotspots();
        }
    }

    private bool CheckPlayerLocation(string requiredLocation)
    {
        if (requiredLocation == "ANY") return true;
        if (LocationManager.Instance == null) return false;

        // 這裡需要根據你的 LocationManager Enum 名稱進行映射
        // 假設 Enum 成員是 禾埕, 廊道 等
        LocationManager.Location currentLoc = LocationManager.Instance.CurrentLocation;
        
        if (requiredLocation == "HEYUAN" && currentLoc == LocationManager.Location.禾埕) return true;
        if (requiredLocation == "LANGDAO" && currentLoc == LocationManager.Location.廊道) return true;

        return false;
    }

    // --- 供 Yarn 腳本調用的命令與函式 ---

    /// <summary>
    /// Yarn 函式：檢查某個事件是否被忽略且待處理。
    /// <<if has_ignored_event("rain")>>
    /// </summary>
    [YarnFunction("has_ignored_event")]
    public static bool HasIgnoredEvent(string eventID)
    {
        if (Instance == null) return false;
        return Instance.pendingIgnoredEventIDs.Contains(eventID);
    }

    /// <summary>
    /// Yarn 指令：正式執行並處理某個被忽略事件的效果。
    /// 用法：<<resolve_ignored_event "rain">>
    /// </summary>
    [YarnCommand("resolve_ignored_event")]
    public void ResolveIgnoredEvent(string eventID)
    {
        if (pendingIgnoredEventIDs.Contains(eventID))
        {
            GameEvent ev = eventDatabase.Find(e => e.id == eventID);
            if (ev != null)
            {
                ApplyEffects(ev.ignoredEffects);
                Debug.Log($"【事件系統】已處理忽略事件「{eventID}」的後果。");
            }
            pendingIgnoredEventIDs.Remove(eventID);
            RefreshHotspots();
        }
    }

    /// <summary>
    /// Yarn 指令：一次處理所有待處理的忽略事件效果。
    /// 用法：<<apply_all_ignored_effects>>
    /// </summary>
    [YarnCommand("apply_all_ignored_effects")]
    public void ApplyAllIgnoredEffects()
    {
        foreach (string id in pendingIgnoredEventIDs)
        {
            GameEvent ev = eventDatabase.Find(e => e.id == id);
            if (ev != null)
            {
                ApplyEffects(ev.ignoredEffects);
            }
        }
        pendingIgnoredEventIDs.Clear();
        RefreshHotspots();
        Debug.Log("【事件系統】已處理所有待處理的忽略事件後果。");
    }

    private void ApplyEffects(List<StatChange> effects)
    {
        foreach (var effect in effects)
        {
            ResourceManager.AdjustStat(effect.statName, effect.amount);
        }
    }

    private void RefreshHotspots()
    {
        LocationHotspot[] hotspots = FindObjectsByType<LocationHotspot>(FindObjectsSortMode.None);
        foreach (var h in hotspots)
        {
            h.UpdateEventState();
        }
    }

    /// <summary>
    /// 供 LocationHotspot 查詢該地點是否有正在進行或待處理的事件。
    /// </summary>
    public bool DoesLocationNeedAttention(LocationManager.Location loc)
    {
        // 邏輯可自訂：例如只要有 pendingIgnoredEventIDs 就亮圓點
        // 或者檢查是否有正在 isProcessing 的事件
        return pendingIgnoredEventIDs.Count > 0;
    }

    // --- 通用旗標系統：給每日任務狀態 (早飯、送飯…) 等 bool 用 ---

    /// <summary>
    /// Yarn 指令：設定旗標。用法：<<set_flag "breakfast_delivered">>
    /// </summary>
    [YarnCommand("set_flag")]
    public static void SetFlag(string key, bool value = true)
    {
        if (Instance == null) return;
        Instance.flags[key] = value;
        Instance.SyncFlagsDebugView();
    }

    private void SyncFlagsDebugView()
    {
        flagsDebugView.Clear();
        foreach (var kv in flags)
        {
            flagsDebugView.Add(new FlagEntry { key = kv.Key, value = kv.Value });
        }
    }

    /// <summary>
    /// Yarn 函式：查詢旗標。用法：<<if has_flag("breakfast_delivered")>>
    /// </summary>
    [YarnFunction("has_flag")]
    public static bool HasFlag(string key)
    {
        return Instance != null && Instance.flags.TryGetValue(key, out bool value) && value;
    }

    // --- 供 KitchenMenu 呼叫：記錄煮飯的食材選擇與好感度結果 ---

    // 丈夫在田裡吃便當，早飯對他的好感度影響只有一半
    private const float HusbandFavorabilityMultiplier = 0.5f;

    /// <summary>
    /// 記錄一次煮飯的加菜選擇與計算出的好感度效果，並套用到公公、婆婆、丈夫的好感度。
    /// 由 KitchenMenu 的煮飯按鈕呼叫。
    /// </summary>
    public void RecordKitchenMealChoice(string addItemKey, int favorabilityResult)
    {
        string addItemLabel = string.IsNullOrEmpty(addItemKey) ? "無加菜" : addItemKey;
        Debug.Log($"【事件系統】煮飯選擇：{addItemLabel}，好感度效果：{favorabilityResult}");

        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.ModifyFavorability("grandfather", favorabilityResult);
            ResourceManager.Instance.ModifyFavorability("grandmother", favorabilityResult);
            ResourceManager.Instance.ModifyFavorability("husband", favorabilityResult * HusbandFavorabilityMultiplier);
        }
    }
}
