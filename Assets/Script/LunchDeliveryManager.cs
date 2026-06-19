using UnityEngine;
using UnityEngine.UI;

public class LunchDeliveryManager : MonoBehaviour
{
    public static LunchDeliveryManager Instance { get; private set; }

    [Header("設定")]
    [SerializeField] private LocationManager.Location outsideLocation = LocationManager.Location.外出;

    [Header("時間點 (秒)")]
    private const float TIME_1200 = 12f * 3600f;
    private const float TIME_1300 = 13f * 3600f;

    [Header("顏色設定")]
    [SerializeField] private Color warningColor = Color.yellow;
    [SerializeField] private Color criticalColor = Color.red;

    private LocationHotspot outsideHotspot;
    private Image eventDotImage;
    private float lastPenaltyTime = 0f;

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
        // 尋找對應的外出熱點
        LocationHotspot[] hotspots = FindObjectsByType<LocationHotspot>(FindObjectsSortMode.None);
        foreach (var h in hotspots)
        {
            if (h.location == outsideLocation)
            {
                outsideHotspot = h;
                if (h.eventDot != null)
                {
                    eventDotImage = h.eventDot.GetComponent<Image>();
                }
                break;
            }
        }
    }

    private void Update()
    {
        if (TimeManager.Instance == null || outsideHotspot == null) return;

        float currentTime = TimeManager.Instance.CurrentTimeInSeconds;
        bool isFoodDone = KitchenMiniGame.IsFoodDone();

        // 只有在 12:00 之後、飯煮好了，且今天還沒送過便當才顯示 EventDot
        if (currentTime >= TIME_1200 && isFoodDone && !EventManager.HasFlag("lunch_delivered"))
        {
            outsideHotspot.eventDot.SetActive(true);
            UpdateEventDotVisual(currentTime);
            HandleFavorabilityPenalty(currentTime);
        }
        else
        {
            if (outsideHotspot.eventDot != null)
                outsideHotspot.eventDot.SetActive(false);
        }
    }

    private void UpdateEventDotVisual(float currentTime)
    {
        if (eventDotImage == null) return;

        eventDotImage.color = currentTime >= TIME_1300 ? criticalColor : warningColor;
    }

    private void HandleFavorabilityPenalty(float currentTime)
    {
        // 13:00 之後開始倒扣好感度，每 5 分鐘扣一次
        if (currentTime >= TIME_1300)
        {
            if (currentTime - lastPenaltyTime >= 300f) // 300秒 = 5分鐘
            {
                if (ResourceManager.Instance != null)
                {
                    ResourceManager.Instance.ModifyFavorability("husband", -2);
                }
                lastPenaltyTime = currentTime;
                Debug.Log("【午餐事件】太晚送飯，丈夫好感度 -2");
            }
        }
    }
}
