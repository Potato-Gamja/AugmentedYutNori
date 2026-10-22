using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>게임판 선택에 사용할 첫 터치 또는 마우스 입력을 읽는다.</summary>
public static class GamePointerInput
{
    private static readonly List<RaycastResult> uiHits = new();

    public static bool TryGetPress(out Vector2 position)
    {
        position = default;
        var touch = Touchscreen.current;
        if (touch != null)
        {
            var primary = touch.primaryTouch;
            if (primary.press.wasPressedThisFrame)
            {
                position = primary.position.ReadValue();
                return true;
            }
            // 같은 터치가 합성 마우스 클릭으로 다시 처리되지 않게 한다.
            if (primary.press.isPressed || primary.press.wasReleasedThisFrame) return false;
        }
        var mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return false;
        position = mouse.position.ReadValue();
        return true;
    }

    public static bool IsOverUI(Vector2 position)
    {
        var eventSystem = EventSystem.current;
        if (eventSystem == null) return false;
        uiHits.Clear();
        eventSystem.RaycastAll(new PointerEventData(eventSystem) { position = position }, uiHits);
        foreach (var hit in uiHits)
            if (hit.module is GraphicRaycaster) return true;
        return false;
    }
}
