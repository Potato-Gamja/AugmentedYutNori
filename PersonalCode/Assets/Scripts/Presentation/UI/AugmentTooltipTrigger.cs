using System;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class AugmentTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private Action onEnter;
    private Action onExit;

    public void Configure(Action enter, Action exit)
    {
        onEnter = enter;
        onExit = exit;
    }

    public void OnPointerEnter(PointerEventData eventData) => onEnter?.Invoke();
    public void OnPointerExit(PointerEventData eventData) => onExit?.Invoke();

    private void OnDisable() => onExit?.Invoke();
}
