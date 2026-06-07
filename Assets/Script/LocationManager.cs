using System;
using UnityEngine;
using Yarn.Unity;

public class LocationManager : MonoBehaviour
{
    public static LocationManager Instance { get; private set; }

    // 使用 Enum 定義地點，方便在 Inspector 或程式碼中輸入數字
    public enum Location
    {
        三合院 = 0,    // 選擇地點的中心點
        禾埕 = 1,      // 主工作區
        廊道 = 2,      // 竹篩風乾區
        灶房 = 3,
        廳堂 = 4,
        豬欄 = 5
    }

    [Header("狀態")]
    [SerializeField] private Location currentLocation = Location.三合院;
    public Location CurrentLocation => currentLocation;

    [Header("設定")]
    private DialogueRunner dialogueRunner;

    // 事件：地點切換時通知其他系統（例如更換背景圖或生成 NPC）
    public event Action<Location> OnLocationChanged;

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
        dialogueRunner = FindFirstObjectByType<DialogueRunner>();
    }

    /// <summary>
    /// 切換到指定地點。
    /// </summary>
    /// <param name="newLocation">目標地點的 Enum 或數字</param>
    [YarnCommand("go_to_location")]
    public void GoToLocation(int locationIndex)
    {
        // 檢查是否為有效的 Enum 索引
        if (!Enum.IsDefined(typeof(Location), locationIndex))
        {
            Debug.LogError($"【地點系統】無效的地點索引：{locationIndex}");
            return;
        }

        Location target = (Location)locationIndex;
        GoToLocation(target);
    }

    public void GoToLocation(Location target)
    {
        // 1. 若目前有 Yarn 對話在進行，不能切換
        if (dialogueRunner != null && dialogueRunner.IsDialogueRunning)
        {
            Debug.LogWarning("【地點系統】對話進行中，禁止切換地點。");
            return;
        }

        // 如果已經在該地點，則不重複觸發
        if (currentLocation == target) return;

        currentLocation = target;
        Debug.Log($"【地點系統】玩家移動至：{currentLocation}");

        // 2. 通知地點已改變
        OnLocationChanged?.Invoke(currentLocation);

        // 3. 這裡可以擴充觸發「抵達事件」的邏輯
        // EventManager.Instance.CheckForArrivalEvents(currentLocation);
    }

    /// <summary>
    /// 供 UI 按鈕檢查是否可以切換地點
    /// </summary>
    public bool CanSwitchLocation()
    {
        if (dialogueRunner == null) return true;
        return !dialogueRunner.IsDialogueRunning;
    }
}
