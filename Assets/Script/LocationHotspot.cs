using UnityEngine.EventSystems;
using UnityEngine;

public class LocationHotspot : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("地點設定")]
    public LocationManager.Location location;

    [Header("UI 引用")]
    public GameObject border;      // 選中外框
    public GameObject label;       // 地點名稱
    public GameObject eventDot;    // 事件提示點 (可選)

    public void OnPointerEnter(PointerEventData e)
    {
        if (border != null) border.SetActive(true);
        if (label != null) label.SetActive(true);
    }

    public void OnPointerExit(PointerEventData e)
    {
        if (border != null) border.SetActive(false);
        if (label != null) label.SetActive(false);
    }

    public void OnPointerClick(PointerEventData e)
    {
        if (LocationManager.Instance != null)
        {
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
}
