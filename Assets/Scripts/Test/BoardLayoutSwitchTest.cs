using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 테스트용: BoardLayoutSwitchTestCanvas의 버튼으로 실행 중 판을 바꾼다. 실제 처리는 BoardView.SwitchLayout
public class BoardLayoutSwitchTest : MonoBehaviour
{
    [SerializeField] private BoardView boardView;
    [SerializeField] private List<LayoutButton> buttons = new List<LayoutButton>();

    [Header("Button Color")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color currentColor = new Color(1f, 0.85f, 0.3f); // 지금 쓰는 판

    [Serializable]
    private struct LayoutButton
    {
        public BoardLayoutSO layout;
        public Button button;
    }

    private void Start()
    {
        if (boardView == null) boardView = FindFirstObjectByType<BoardView>();

        foreach (LayoutButton entry in buttons)
        {
            if (entry.button == null || entry.layout == null) continue;

            BoardLayoutSO layout = entry.layout;
            entry.button.onClick.AddListener(() => OnClicked(layout));

            // TMP_Text label = entry.button.GetComponentInChildren<TMP_Text>();
            // if (label != null) label.text = layout.name;
        }
        RefreshColors();
    }

    private void OnClicked(BoardLayoutSO layout)
    {
        if (boardView != null && boardView.SwitchLayout(layout)) RefreshColors();
    }

    // 지금 쓰는 판의 버튼만 강조
    private void RefreshColors()
    {
        foreach (LayoutButton entry in buttons)
        {
            if (entry.button == null || entry.button.image == null) continue;
            bool current = boardView != null && boardView.Layout == entry.layout;
            entry.button.image.color = current ? currentColor : normalColor;
        }
    }
}
