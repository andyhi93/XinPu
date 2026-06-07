using UnityEngine;
using Yarn.Unity;

public class ClickToTalk : MonoBehaviour
{
    [Header("想觸發的 Yarn 劇情節點名稱")]
    public string targetNode = "Start";

    private DialogueRunner dialogueRunner;

    void Start()
    {
        dialogueRunner = Object.FindFirstObjectByType<DialogueRunner>();
        if (dialogueRunner == null)
        {
            Debug.LogError("【對話測試】場景中找不到 DialogueRunner！請檢查是否有拉入 Dialogue System。");
        }
    }

    void OnMouseDown()
    {
        // Check if game is paused
        if (TimeManager.Instance != null && TimeManager.Instance.isGamePaused)
        {
            Debug.Log("【對話測試】遊戲已暫停，忽略點擊。");
            return;
        }

        // 旗子 1：確認滑鼠確實有成功點擊到這個物件的 Collider
        Debug.Log($"【對話測試】滑鼠成功點擊到了物件：{gameObject.name}");

        if (dialogueRunner == null) 
        {
            Debug.LogWarning("【對話測試】因為 dialogueRunner 是空的，中斷執行。");
            return;
        }

        // 旗子 2：檢查是不是因為系統判定「對話正在執行中」而被攔截
        if (dialogueRunner.IsDialogueRunning) 
        {
            Debug.LogWarning("【對話測試】系統偵測到對話正在執行中（IsDialogueRunning = true），拒絕重複觸發。");
            return;
        }

        Debug.Log($"【對話測試】準備嘗試啟動節點：{targetNode}");
        dialogueRunner.StartDialogue(targetNode);
    }
}