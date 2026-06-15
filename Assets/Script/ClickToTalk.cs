using UnityEngine;
using Yarn.Unity;

/// <summary>
/// 點擊物件啟動對話。
/// 現在透過 GameManager 統一管理對話啟動。
/// </summary>
public class ClickToTalk : MonoBehaviour
{
    [Header("想觸發的 Yarn 劇情節點名稱")]
    public string targetNode = "Start";

    void OnMouseDown()
    {
        // 如果遊戲已暫停（例如在選單中），則不觸發
        if (TimeManager.Instance != null && TimeManager.Instance.isGamePaused) return;

        // 透過 GameManager 啟動對話
        if (GameManager.Instance != null)
        {
            GameManager.Instance.StartDialogue(targetNode);
        }
        else
        {
            Debug.LogWarning("【點擊對話】場景中找不到 GameManager，嘗試直接尋找 DialogueRunner。");
            var runner = Object.FindFirstObjectByType<DialogueRunner>();
            if (runner != null && !runner.IsDialogueRunning)
            {
                runner.StartDialogue(targetNode);
            }
        }
    }
}
