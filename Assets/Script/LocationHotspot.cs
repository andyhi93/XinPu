using UnityEngine.EventSystems;
using UnityEngine;
using UnityEngine.UI;

public class LocationHotspot : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("地點設定")]
    public LocationManager.Location location;

    [Header("UI 引用")]
    public GameObject border;      // 選中外框
    public GameObject label;       // 地點名稱
    public GameObject eventDot;    // 事件提示點 (可選)

    [Header("事件提示顏色")]
    [SerializeField] private Color eventDotWarningColor = Color.yellow;
    [SerializeField] private Color eventDotCriticalColor = Color.red;

    private Image eventDotImage;

    private void Start()
    {
        // 初始化：預設不顯示外框與地點名稱
        if (border != null) border.SetActive(false);
        if (label != null) label.SetActive(false);

        if (eventDot != null) eventDotImage = eventDot.GetComponent<Image>();
    }

    public void OnPointerEnter(PointerEventData e)
    {
        // 只有在可以切換地點時才顯示提示（避免在小遊戲或對話中產生誤導）
        if (LocationManager.Instance != null && !LocationManager.Instance.CanSwitchLocation()) return;

        if (border != null) border.SetActive(true);
        if (label != null) label.SetActive(true);
    }

    public void OnPointerExit(PointerEventData e)
    {
        // 離開時一律隱藏提示
        if (border != null) border.SetActive(false);
        if (label != null) label.SetActive(false);
    }

    public void OnPointerClick(PointerEventData e)
    {
        if (LocationManager.Instance != null)
        {
            // GoToLocation 內部已經有檢查，但為了回饋明確，這裡也可以加
            LocationManager.Instance.GoToLocation(location);
        }
        else
        {
            Debug.LogError("【地點系統】找不到 LocationManager 實例！");
        }
    }

    /// <summary>
    /// 根據是否有未處理事件來更新 UI 圓點。
    /// </summary>
    public void UpdateEventState()
    {
        if (eventDot != null && EventManager.Instance != null)
        {
            // 這裡可以根據 location 來過濾是否顯示圓點
            // 目前先簡單設定：只要有未處理事件就顯示
            eventDot.SetActive(EventManager.Instance.DoesLocationNeedAttention(location));
        }
    }

    /// <summary>
    /// 顯示或隱藏事件提示圓點，並依緊急程度切換顏色（黃/紅）。
    /// </summary>
    public void SetEventDot(bool active, bool isCritical = false)
    {
        if (eventDot == null) return;

        eventDot.SetActive(active);
        if (active && eventDotImage != null)
        {
            eventDotImage.color = isCritical ? eventDotCriticalColor : eventDotWarningColor;
        }
    }
}
