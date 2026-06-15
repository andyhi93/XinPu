using UnityEngine;
using Yarn.Unity;

/// <summary>
/// 遊戲總管理器：負責管理遊戲模式切換、全域狀態與對話啟動。
/// </summary>
public enum GameState { Dialogue, FreeRoam, Minigame }

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Yarn Spinner 設定")]
    public DialogueRunner dialogueRunner;
    [Tooltip("一開始啟動遊戲時的對話節點名稱")]
    public string startingNode = "Start";
    [Tooltip("是否在遊戲啟動時自動執行 startingNode")]
    public bool autoStartDialogue = true;

    [Header("目前狀態")]
    public GameState currentState = GameState.FreeRoam;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        if (dialogueRunner == null)
        {
            dialogueRunner = FindFirstObjectByType<DialogueRunner>();
        }

        // 初始自動啟動
        if (autoStartDialogue && dialogueRunner != null && !string.IsNullOrEmpty(startingNode))
        {
            StartDialogue(startingNode);
        }
    }

    /// <summary>
    /// 全域統一啟動對話的入口
    /// </summary>
    /// <param name="nodeName">Yarn 節點名稱</param>
    public void StartDialogue(string nodeName)
    {
        if (dialogueRunner == null)
        {
            Debug.LogError("【GameManager】找不到 DialogueRunner，無法啟動對話。");
            return;
        }

        if (dialogueRunner.IsDialogueRunning)
        {
            Debug.LogWarning($"【GameManager】對話系統忙碌中，忽略啟動要求：{nodeName}");
            return;
        }

        Debug.Log($"【GameManager】啟動劇情節點：{nodeName}");
        SetGameState(GameState.Dialogue);
        dialogueRunner.StartDialogue(nodeName);

        // 監聽結束事件
        dialogueRunner.onDialogueComplete.AddListener(OnDialogueComplete);
    }

    private void OnDialogueComplete()
    {
        SetGameState(GameState.FreeRoam);
        dialogueRunner.onDialogueComplete.RemoveListener(OnDialogueComplete);
    }

    /// <summary>
    /// 切換遊戲模式，並處理模式切換的連帶影響
    /// </summary>
    public void SetGameState(GameState newState)
    {
        currentState = newState;
        
        switch (newState)
        {
            case GameState.Dialogue:
                // 對話中暫停遊戲時間
                if (TimeManager.Instance != null) TimeManager.Instance.isGamePaused = true;
                break;
            case GameState.FreeRoam:
                // 自由行動恢復時間
                if (TimeManager.Instance != null) TimeManager.Instance.isGamePaused = false;
                break;
            case GameState.Minigame:
                // 進入小遊戲暫停時間
                if (TimeManager.Instance != null) TimeManager.Instance.isGamePaused = true;
                break;
        }
        
        Debug.Log($"【GameManager】模式切換至：{newState}");
    }

    /// <summary>
    /// 供 Yarn 指令使用的模式切換：<<game_mode "Minigame">>
    /// </summary>
    [YarnCommand("game_mode")]
    public static void SetModeCommand(string modeName)
    {
        if (Instance == null) return;
        if (System.Enum.TryParse(modeName, true, out GameState newState))
        {
            Instance.SetGameState(newState);
        }
    }
}
