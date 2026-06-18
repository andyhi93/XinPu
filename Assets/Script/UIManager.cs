using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("體力 (Health)")]
    public Slider healthSlider;
    public Image healthFill;
    public TMP_Text healthText;
    public Color healthHighColor = Color.white;
    public Color healthMidColor = Color.yellow;
    public Color healthLowColor = Color.red;

    [Header("心情 (Mood)")]
    public Slider moodSlider;
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
        float ratio = currentHealth / 100f;
        if (healthSlider != null) healthSlider.value = ratio;
        if (healthText != null) healthText.text = $"{currentHealth}%";
        if (healthFill != null)
        {
            healthFill.fillAmount = ratio;
            
            // 顏色邏輯： > 60% 正常, 30-60% 黃色, < 30% 紅色
            if (currentHealth > 60)
                healthFill.color = healthHighColor;
            else if (currentHealth >= 30)
                healthFill.color = healthMidColor;
            else
                healthFill.color = healthLowColor;
        }
    }

    private void UpdateMood(int currentMood)
    {
        float ratio = currentMood / 100f;
        if (moodSlider != null) moodSlider.value = ratio;
        if (moodText != null) moodText.text = $"{currentMood}%";
        if (moodFill != null)
        {
            moodFill.fillAmount = ratio;
            
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
