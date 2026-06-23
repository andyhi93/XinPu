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

    [Header("天數 (Day)")]
    public TMP_Text dayText;

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

        // 2. 監聽時間變化（時辰切換時順便更新天數，目前沒有獨立的「跨日」事件，
        //    但時辰每兩小時一定會切換一次，借這個時機檢查天數就夠即時了）
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
        UpdateDayUI();
    }

    private void UpdateDayUI()
    {
        if (dayText == null) return;
        int day = Mathf.RoundToInt(TimeManager.GetDayNumber());
        dayText.text = $"第{ToChineseNumber(day)}天";
    }

    private void UpdateLocationUI(LocationManager.Location location)
    {
        if (locationText != null) locationText.text = location.ToString();
    }

    private static readonly string[] ChineseDigits = { "零", "一", "二", "三", "四", "五", "六", "七", "八", "九" };
    private static readonly string[] ChineseUnits = { "", "十", "百", "千" };

    /// <summary>
    /// 把 0~9999 的整數轉成國字數字（例如 12 -> 十二，101 -> 一百零一）。
    /// 天數理論上不會跑到萬位數，超出範圍就直接回傳阿拉伯數字當保底，不會噴錯。
    /// </summary>
    private static string ToChineseNumber(int number)
    {
        if (number <= 0) return ChineseDigits[0];
        if (number >= 10000) return number.ToString();

        string digits = number.ToString();
        int len = digits.Length;
        var sb = new System.Text.StringBuilder();
        bool pendingZero = false;

        for (int i = 0; i < len; i++)
        {
            int digit = digits[i] - '0';
            int unitIndex = len - i - 1; // 0=個位、1=十位、2=百位、3=千位

            if (digit == 0)
            {
                pendingZero = true;
                continue;
            }

            if (pendingZero)
            {
                sb.Append(ChineseDigits[0]);
                pendingZero = false;
            }

            // 開頭剛好是「十位」且數字是 1 時省略「一」：10 -> 十，12 -> 十二，而不是一十、一十二
            bool omitLeadingOne = digit == 1 && unitIndex == 1 && i == 0;
            if (!omitLeadingOne)
            {
                sb.Append(ChineseDigits[digit]);
            }
            sb.Append(ChineseUnits[unitIndex]);
        }

        return sb.ToString();
    }
}
