using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 結算/結局 UI 面板：遊戲只玩一天，Scene_DailySettlement 結尾透過 GameManager 呼叫，
/// 顯示今天做過的事情摘要，並提供「重新遊玩」／「結束遊戲」。
/// 不是 singleton，跟其他小遊戲一樣由 GameManager 在 Inspector 直接拖引用。
/// 面板結構：Content（ScrollRect 的內容容器，每一行文字是底下一個子物件）、Button（重新遊玩）、Exit（Image + GenericEventTrigger）。
/// </summary>
public class EndingUIManager : MonoBehaviour
{
    [Header("Window")]
    [SerializeField] private GameObject menuWindow; // 結算視窗本體，用來開關介面，預設關閉

    [Header("UI 元件")]
    [Tooltip("ScrollRect 底下的 Content 容器，每一行摘要文字會各自是一個子物件，這樣 ScrollRect 才能正確算出可滾動範圍")]
    [SerializeField] private Transform contentParent;
    [Tooltip("單行文字用的 Prefab，上面要有一個 TextMeshProUGUI 元件")]
    [SerializeField] private GameObject linePrefab;
    [SerializeField] private Button restartButton;
    [SerializeField] private GenericEventTrigger exitTrigger; // Image 上掛 GenericEventTrigger 來接收點擊

    private void Awake()
    {
        if (menuWindow != null) menuWindow.SetActive(false);
        if (restartButton != null) restartButton.onClick.AddListener(OnClickRestart);
        if (exitTrigger != null) exitTrigger.OnClick.AddListener(OnClickExit);
    }

    /// <summary>
    /// 供 GameManager 呼叫（對應 Yarn 的 <<show_ending_ui>>），顯示今天的行動摘要並結束本局。
    /// </summary>
    public void ShowEndingPanel()
    {
        // 結算面板本身就是這一局的終點：把狀態切到 Minigame，避免 GameManager
        // 「對話結束忘記 jump 就自動補 Scene_FreeRoam」的防呆機制把畫面蓋回去；
        // 時間也直接暫停，不讓背景還在跑、又跳出其他強制事件蓋在結算面板上面。
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetGameState(GameState.Minigame);
        }
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.isGamePaused = true;
        }

        // 結算當天賣柿餅的收入、扣地租田賦，要在 DisplaySummaryLines 讀數值之前算好。
        ResourceManager.CalculateEndingFinance();

        if (menuWindow != null) menuWindow.SetActive(true);
        DisplaySummaryLines();
    }

    /// <summary>
    /// 把 Content 底下清空，每一行摘要文字各自 Instantiate 一個 linePrefab，
    /// 而不是把整段文字塞進同一個 TextMeshProUGUI——否則只算一個物件，ScrollRect 量不出實際內容高度，滾動會失效。
    /// </summary>
    private void DisplaySummaryLines()
    {
        if (contentParent == null || linePrefab == null) return;

        for (int i = contentParent.childCount - 1; i >= 0; i--)
        {
            Destroy(contentParent.GetChild(i).gameObject);
        }

        foreach (string line in BuildSummaryLines())
        {
            GameObject lineObj = Instantiate(linePrefab, contentParent);
            TextMeshProUGUI lineText = lineObj.GetComponent<TextMeshProUGUI>();
            if (lineText != null) lineText.text = line;
        }
    }

    /// <summary>
    /// 依照今天實際觸發過的旗標，組成行動紀錄列表；什麼都沒做過就顯示對應的提示句。
    /// </summary>
    private List<string> BuildSummaryLines()
    {
        List<string> actionLines = new List<string>();

        if (EventManager.HasFlag("breakfast_delivered")) actionLines.Add("煮了早飯，端給大家吃。");
        if (EventManager.HasFlag("pig_fed")) actionLines.Add("煮了豬菜，餵了豬。");
        if (EventManager.HasFlag("chicken_fed")) actionLines.Add("放雞出來，灑了穀物。");
        if (EventManager.HasFlag("eggs_collected")) actionLines.Add("去雞巢收了蛋。");
        if (EventManager.HasFlag("lunch_delivered")) actionLines.Add("走到田邊，把便當送過去。");
        if (EventManager.HasFlag("laundry_done")) actionLines.Add("去洗衫窟洗了衣服。");
        if (EventManager.HasFlag("town_looked_around")) actionLines.Add("在街上走了一圈，聽了些消息。");
        if (EventManager.HasFlag("water_boiled_today")) actionLines.Add("燒了熱水給家裡的人用。");
        if (EventManager.HasFlag("firewood_picked_up_today")) actionLines.Add("順路撿了柴回來。");
        if (EventManager.HasFlag("fire_extinguished")) actionLines.Add("灶房起火，她把它壓住了。");
        if (EventManager.HasFlag("morning_late")) actionLines.Add("今天灶火點晚了。");

        List<string> lines = new List<string>
        {
            "今天你做了這些"
        };

        if (actionLines.Count == 0)
        {
            lines.Add("今天她什麼都沒能做到。");
        }
        else
        {
            foreach (string line in actionLines)
            {
                lines.Add($"・{line}");
            }
        }

        lines.Add($"體力剩下 {(int)ResourceManager.GetStat("health")} ／ 心情 {(int)ResourceManager.GetStat("mood")}");

        int persimmonIncome = ResourceManager.GetPersimmonIncome();
        if (persimmonIncome > 0) lines.Add($"・柿餅賣了，得 {persimmonIncome} 元。");
        lines.Add($"・今天要繳地租田賦，共 {ResourceManager.GetRentAndTax()} 元。");

        lines.Add($"今天的帳：收入 {ResourceManager.GetDailyIncome()} 元，支出 {ResourceManager.GetDailyExpense()} 元");
        lines.Add($"身上剩下 {(int)ResourceManager.GetStat("money")} 元");

        return lines;
    }

    private void OnClickRestart()
    {
        GameManager.RestartGame();
    }

    private void OnClickExit()
    {
        Debug.Log("【結算】玩家選擇結束遊戲，呼叫 Application.Quit()（Editor 中測試時無效是正常現象）。");
        Application.Quit();
    }
}
