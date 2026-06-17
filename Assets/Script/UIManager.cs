using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("體力 (Health)")]
    public Transform healthSlotsParent; // 拖入 Health_Row 容器
    private List<GameObject> healthSlots = new List<GameObject>();

    [Header("心情 (Mood)")]
    public Image moodFill;
    public TMP_Text moodText;
    public Color moodHighColor = Color.white;
    public Color moodMidColor = Color.yellow;
    public Color moodLowColor = Color.red;

    [Header("金錢 (Money)")]
    public TMP_Text moneyText;

    [Header("時間 (Time)")]
    public TMP_Text timeText;

    [Header("地點 (Location)")]
    public TMP_Text locationText;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        // 0. 自動抓取體力格子
        if (healthSlotsParent != null)
        {
            foreach (Transform child in healthSlotsParent)
            {
                healthSlots.Add(child.gameObject);
            }
        }

        // 1. 監聽資源變化
        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.OnStatChanged += HandleStatChanged;
            // 初始化顯示
            UpdateHealth((int)ResourceManager.GetStat("health"));
            UpdateMood((int)ResourceManager.GetStat("mood"));
            UpdateMoney((int)ResourceManager.GetStat("money"));
        }

        // 2. 監聽時間變化
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnTimePeriodChanged += UpdateTimeUI;
            UpdateTimeUI(TimeManager.Instance.GetCurrentTime());
        }

        // 3. 監聽地點變化
        if (LocationManager.Instance != null)
        {
            LocationManager.Instance.OnLocationChanged += UpdateLocationUI;
            UpdateLocationUI(LocationManager.Instance.CurrentLocation);
        }
    }

    private void OnDestroy()
    {
        if (ResourceManager.Instance != null)
            ResourceManager.Instance.OnStatChanged -= HandleStatChanged;

        if (TimeManager.Instance != null)
            TimeManager.Instance.OnTimePeriodChanged -= UpdateTimeUI;

        if (LocationManager.Instance != null)
            LocationManager.Instance.OnLocationChanged -= UpdateLocationUI;
    }

    // --- 處理事件 ---

    private void HandleStatChanged(string statName, object value)
    {
        switch (statName.ToLower())
        {
            case "health":
                UpdateHealth((int)value);
                break;
            case "mood":
                UpdateMood((int)value);
                break;
            case "money":
                UpdateMoney((int)value);
                break;
        }
    }

    private void UpdateHealth(int currentHealth)
    {
        if (healthSlots == null || healthSlots.Count == 0) return;

        for (int i = 0; i < healthSlots.Count; i++)
        {
            // 如果當前索引小於體力值，則顯示，否則隱藏
            healthSlots[i].SetActive(i < currentHealth);
        }
    }

    private void UpdateMood(int currentMood)
    {
        if (moodText != null) moodText.text = $"{currentMood}%";
        if (moodFill != null)
        {
            moodFill.fillAmount = currentMood / 100f;
            
            // 顏色邏輯： > 60 正常，30-60 黃色，< 30 紅色
            if (currentMood > 60)
                moodFill.color = moodHighColor;
            else if (currentMood >= 30)
                moodFill.color = moodMidColor;
            else
                moodFill.color = moodLowColor;
        }
    }

    private void UpdateMoney(int currentMoney)
    {
        if (moneyText != null) moneyText.text = $"{currentMoney}";
    }

    private void UpdateTimeUI(string branchName)
    {
        if (timeText != null) timeText.text = $"{branchName}";
    }

    private void UpdateLocationUI(LocationManager.Location location)
    {
        if (locationText != null) locationText.text = location.ToString();
    }
}
