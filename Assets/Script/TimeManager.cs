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
    private int currentBranchIndex = -1;

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
        }

        UpdateBranchIndex();
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
    /// 回傳當前一天的進度 (0.0 到 1.0)
    /// </summary>
    public float GetDayProgress()
    {
        return currentTimeInSeconds / (24f * 3600f);
    }
}
