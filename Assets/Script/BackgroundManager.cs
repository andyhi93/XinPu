using UnityEngine;
using UnityEngine.UI;
using Yarn.Unity;
using System.Collections.Generic;

/// <summary>
/// 背景管理器：負責切換場景背景圖。
/// </summary>
public class BackgroundManager : MonoBehaviour
{
    public static BackgroundManager Instance { get; private set; }

    [Header("UI 元件")]
    public Image bgImage;

    [System.Serializable]
    public struct BackgroundEntry
    {
        public string name;
        public Sprite sprite;
    }

    [Header("背景圖庫")]
    [Tooltip("手動輸入背景名稱與圖片")]
    public List<BackgroundEntry> backgrounds = new List<BackgroundEntry>();

    [Header("House 限定圖層")]
    [Tooltip("只有在背景套用名為 \"house\" 的圖片時顯示，切到其他背景時自動關閉")]
    public GameObject house;

    [System.Serializable]
    public class LocationBackgroundEntry
    {
        public LocationManager.Location location;
        [Tooltip("對應 backgrounds 清單裡的 name，留空代表這個地點還沒有指定背景")]
        public string backgroundName;
    }

    [Header("地點對應背景")]
    [Tooltip("切換 Location（地點）時，自動套用對應的背景名稱")]
    public List<LocationBackgroundEntry> locationBackgrounds = new List<LocationBackgroundEntry>
    {
        new LocationBackgroundEntry { location = LocationManager.Location.三合院, backgroundName = "house" },
        new LocationBackgroundEntry { location = LocationManager.Location.禾埕, backgroundName = "DryingYard" },
        new LocationBackgroundEntry { location = LocationManager.Location.廊道, backgroundName = "" },
        new LocationBackgroundEntry { location = LocationManager.Location.灶房, backgroundName = "kitchen" },
        new LocationBackgroundEntry { location = LocationManager.Location.廳堂, backgroundName = "hall" },
        // 豬欄目前沒有專屬的背景圖，先沿用三合院的 house 背景與 House 圖層
        new LocationBackgroundEntry { location = LocationManager.Location.豬欄, backgroundName = "house" },
        // 外出＝走到門口，還沒真的上路，所以先維持 house 背景；
        // 真正切到 road 是靠 Scene_Road_ToTown/Scene_Road_Home 裡的 <<bg road>> 指令
        new LocationBackgroundEntry { location = LocationManager.Location.外出, backgroundName = "house" },
    };

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (LocationManager.Instance != null)
        {
            LocationManager.Instance.OnLocationChanged += HandleLocationChanged;
            // 啟動時手動套用一次目前地點，因為 OnLocationChanged 只有在「切換」時才會觸發
            HandleLocationChanged(LocationManager.Instance.CurrentLocation);
        }
    }

    private void OnDestroy()
    {
        if (LocationManager.Instance != null)
        {
            LocationManager.Instance.OnLocationChanged -= HandleLocationChanged;
        }
    }

    /// <summary>
    /// 地點切換時：自動套用對應背景（House 圖層的開關交給 ChangeBackground 統一處理）。
    /// </summary>
    private void HandleLocationChanged(LocationManager.Location location)
    {
        LocationBackgroundEntry entry = locationBackgrounds.Find(e => e.location == location);
        if (entry != null && !string.IsNullOrEmpty(entry.backgroundName))
        {
            ChangeBackground(entry.backgroundName);
        }
    }

    /// <summary>
    /// 切換背景：<<bg 背景註冊名稱>>
    /// 設為 static 可避免 Yarn Spinner 去尋找同名的 GameObject，解決報錯問題。
    /// House 圖層只在背景就是 "house" 時顯示，不管這次切換是地點自動觸發還是劇本直接下指令
    /// （例如出門走到大路時劇本會下 <<bg road>>，House 就會自動跟著關掉）。
    /// </summary>
    [YarnCommand("bg")]
    public static void ChangeBackground(string bgName)
    {
        if (Instance == null)
        {
            Debug.LogError("【背景管理】場景中找不到 BackgroundManager 實例！");
            return;
        }

        if (Instance.bgImage == null) return;

        BackgroundEntry entry = Instance.backgrounds.Find(e => e.name == bgName);
        if (entry.sprite != null)
        {
            Instance.bgImage.sprite = entry.sprite;
            if (Instance.house != null)
            {
                Instance.house.SetActive(bgName == "house");
            }
        }
        else
        {
            // 找不到圖片時只發出警告，不中斷遊戲
            Debug.LogWarning($"【背景管理】找不到名為 '{bgName}' 的背景註冊。請檢查 Inspector 設定。");
        }
    }
}
