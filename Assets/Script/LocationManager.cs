using System;
using System.Collections.Generic;
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
        豬欄 = 5,
        外出 = 6
    }

    [Header("狀態")]
    [SerializeField] private Location currentLocation = Location.三合院;
    public Location CurrentLocation => currentLocation;

    [Header("地點熱點")]
    [Tooltip("所有地點熱點的共同父物件，啟動時會自動收集其底下的 LocationHotspot 子節點。")]
    [SerializeField] private GameObject locationsRoot;

    private readonly Dictionary<Location, LocationHotspot> hotspots = new Dictionary<Location, LocationHotspot>();

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
        BuildHotspotRegistry();
    }

    /// <summary>
    /// 掃描 locationsRoot 底下的 LocationHotspot 子節點，依 Location 類別建立索引。
    /// </summary>
    private void BuildHotspotRegistry()
    {
        hotspots.Clear();
        if (locationsRoot == null) return;

        foreach (var hotspot in locationsRoot.GetComponentsInChildren<LocationHotspot>(true))
        {
            hotspots[hotspot.location] = hotspot;
        }
    }

    /// <summary>
    /// 取得指定地點的 LocationHotspot（若尚未註冊則回傳 null）。
    /// </summary>
    public LocationHotspot GetHotspot(Location loc)
    {
        hotspots.TryGetValue(loc, out var hotspot);
        return hotspot;
    }

    /// <summary>
    /// 供其他系統（如 GameManager）切換指定地點的事件提示圓點。
    /// </summary>
    public void SetEventDot(Location loc, bool active, bool isCritical = false)
    {
        if (hotspots.TryGetValue(loc, out var hotspot))
        {
            hotspot.SetEventDot(active, isCritical);
        }
    }

    /// <summary>
    /// 切換到指定地點。
    /// </summary>
    /// <param name="locationIndex">目標地點的 Enum 或數字</param>
    [YarnCommand("go_to_location")]
    public static void GoToLocationCommand(int locationIndex)
    {
        if (Instance == null) return;

        // 檢查是否為有效的 Enum 索引
        if (!Enum.IsDefined(typeof(Location), locationIndex))
        {
            Debug.LogError($"【地點系統】無效的地點索引：{locationIndex}");
            return;
        }

        Location target = (Location)locationIndex;
        // 透過 Yarn 指令切換地點時，強制執行（無視對話正在進行的限制）
        Instance.GoToLocation(target, force: true);
    }

    public void GoToLocation(Location target, bool force = false)
    {
        // 1. 統一檢查是否可以切換地點 (除非被強制執行)
        if (!force && !CanSwitchLocation())
        {
            Debug.LogWarning("【地點系統】目前狀態（對話或小遊戲中）禁止切換地點。");
            return;
        }

        // 如果已經在該地點，則不重複觸發
        if (currentLocation == target) return;

        currentLocation = target;
        Debug.Log($"【地點系統】玩家移動至：{currentLocation}");

        // 2. 通知地點已改變
        OnLocationChanged?.Invoke(currentLocation);
    }

    /// <summary>
    /// 供 UI 按鈕檢查是否可以切換地點
    /// </summary>
    public bool CanSwitchLocation()
    {
        // 1. 若目前有 Yarn 對話在進行，不能切換
        if (dialogueRunner != null && dialogueRunner.IsDialogueRunning) return false;

        // 2. 若目前正在小遊戲模式，不能切換
        if (GameManager.Instance != null && GameManager.Instance.currentState != GameState.FreeRoam)
        {
            return false;
        }

        return true;
    }
}
