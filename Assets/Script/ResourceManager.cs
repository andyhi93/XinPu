using System;
using System.Collections.Generic;
using UnityEngine;
using Yarn.Unity;

public class ResourceManager : MonoBehaviour
{
    public static ResourceManager Instance { get; private set; }

    public enum FavorLevel { 差, 普通, 好 }

    [Header("基礎數值")]
    [SerializeField] private int health = 10;
    [SerializeField] private int mood = 100;
    [SerializeField] private int money = 100;
    [SerializeField] private FavorLevel motherInLawFavor = FavorLevel.普通;

    [Header("預留數值 (原型開發用)")]
    public float persimmon_progress = 0f;
    public int livestock_satisfaction = 100;

    [Header("Yarn 整合設定")]
    [SerializeField] private string healthExhaustedNode = "HealthExhausted";
    private DialogueRunner dialogueRunner;

    // 事件：提供給 UI 或其他系統監聽數值變化
    public event Action<string, object> OnStatChanged;

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
    public void AdjustStat(string statName, float amount)
    {
        int intAmount = Mathf.RoundToInt(amount);
        
        switch (statName.ToLower())
        {
            case "health":
                health = Mathf.Clamp(health + intAmount, 0, 10);
                NotifyChange("health", health);
                if (health <= 0) HandleHealthExhausted();
                break;
            case "mood":
                mood = Mathf.Clamp(mood + intAmount, 0, 100);
                NotifyChange("mood", mood);
                break;
            case "money":
                money += intAmount;
                NotifyChange("money", money);
                break;
            case "mother_in_law_favor":
                // 原型版簡單處理好感度循環
                int current = (int)motherInLawFavor;
                motherInLawFavor = (FavorLevel)Mathf.Clamp(current + intAmount, 0, 2);
                NotifyChange("mother_in_law_favor", motherInLawFavor);
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

        return statName.ToLower() switch
        {
            "health" => Instance.health,
            "mood" => Instance.mood,
            "money" => Instance.money,
            "mother_in_law_favor" => (int)Instance.motherInLawFavor,
            _ => 0
        };
    }

    /// <summary>
    /// 直接設定指定數值。
    /// </summary>
    [YarnCommand("set_stat")]
    public void SetStat(string statName, float value)
    {
        int intValue = Mathf.RoundToInt(value);
        switch (statName.ToLower())
        {
            case "health":
                health = Mathf.Clamp(intValue, 0, 10);
                NotifyChange("health", health);
                break;
            case "mood":
                mood = Mathf.Clamp(intValue, 0, 100);
                NotifyChange("mood", mood);
                break;
            case "money":
                money = intValue;
                NotifyChange("money", money);
                break;
            case "mother_in_law_favor":
                motherInLawFavor = (FavorLevel)Mathf.Clamp(intValue, 0, 2);
                NotifyChange("mother_in_law_favor", motherInLawFavor);
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
}
