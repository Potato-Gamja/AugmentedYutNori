using System;
using UnityEngine;
using UnityEngine.UI;

public class YutThrowView : MonoBehaviour
{
    [SerializeField] private Button throwButton;
    [SerializeField] private Image[] stickImages;        // 윷가락 4개

    [SerializeField] private Sprite flatSprite;          // 평평한 면
    [SerializeField] private Sprite roundSprite;         // 둥근 면
    [SerializeField] private Sprite markedFlatSprite;    // 표시된 윷가락의 평평한 면 (빽도 표시)

    private const int MarkedStick = 0;

    public event Action ThrowClicked; // 버튼 클릭 액션에 일어날 작용들 담기

    private void Awake()
    {
        throwButton.onClick.AddListener(() => ThrowClicked?.Invoke());
    }

    public void ShowSticks(bool[] sticks)
    {
        for (int i = 0; i < stickImages.Length; i++)
        {
            bool isFlat = sticks[i];

            if (!isFlat)
                stickImages[i].sprite = roundSprite;
            else if (i == MarkedStick)
                stickImages[i].sprite = markedFlatSprite;
            else
                stickImages[i].sprite = flatSprite;
        }
    }

    public void SetInteractable(bool on) => throwButton.interactable = on;
}