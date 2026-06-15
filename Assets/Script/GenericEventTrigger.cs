using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

/// <summary>
/// 通用的事件觸發器，可用於非 Button 組件（如 Image）來接收點擊等事件
/// </summary>
public class GenericEventTrigger : MonoBehaviour, IPointerClickHandler
{
    [Header("點擊事件")]
    public UnityEvent OnClick;

    public void OnPointerClick(PointerEventData eventData)
    {
        OnClick?.Invoke();
    }
}
