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
    
    [Header("狀態")]
    public bool isGamePaused = false;

    // 當前遊戲時間（秒，範圍 0 到 86400）
    private float currentTimeInSeconds;
    public float CurrentTimeInSeconds => currentTimeInSeconds;
    private int currentBranchIndex = -1;
    private int lastHealthDeductionIndex = -1; // 用來紀錄上一次扣過體力的時辰索引
    private bool hasTriggeredMorningLate = false; // 是否已觸發過「遲到的早餐」事件

    // 十二地支映射
    private readonly string[] earthlyBranches = {
        "子", "丑", "寅", "卯", "辰", "巳", "午", "未", "申", "酉", "戌", "亥"
    };

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
            hasTriggeredMorningLate = false; // 每日重置觸發狀態
        }

        CheckForTimeEvents();
        UpdateBranchIndex();
    }

    private void CheckForTimeEvents()
    {
        if (!hasTriggeredMorningLate && IsTimeAfter(8, 0))
        {
            // 檢查飯是否煮好
            bool isFoodDone = KitchenMiniGame.IsFoodDone();
            
            if (!isFoodDone)
            {
                hasTriggeredMorningLate = true;
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.ForceStartDialogue("Scene_MorningLate");
                }
            }
            else
            {
                // 如果已經煮好了，今天就不再檢查（直到隔天重置）
                hasTriggeredMorningLate = true;
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
            OnTimePeriodChanged?.Invoke(GetCurrentTime());
            Debug.Log($"時段已切換：{GetCurrentTime()}時");
        }
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
        // 手動推進一個小時（3600 秒）
        currentTimeInSeconds += 3600f;
        if (currentTimeInSeconds >= 24f * 3600f)
        {
            currentTimeInSeconds -= 24f * 3600f;
        }
        UpdateBranchIndex();
        Debug.Log("【TimeManager】已手動推進 1 小時");
    }

    [YarnCommand("advance_time")]
    public void AdvanceTime()
    {
        // 手動推進一個時段（2 小時）
        currentTimeInSeconds += 2f * 3600f;
        if (currentTimeInSeconds >= 24f * 3600f)
        {
            currentTimeInSeconds -= 24f * 3600f;
        }
        UpdateBranchIndex();
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
        }
        Instance.UpdateBranchIndex();
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
                // 子(0) -> 23, 丑(1) -> 1, 寅(2) -> 3, 卯(3) -> 5
                int startHour = (branchIndex * 2 - 1 + 24) % 24;
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
}
