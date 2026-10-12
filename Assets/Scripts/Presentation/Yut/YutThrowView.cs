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

    public event Action ThrowClicked; // 버튼 클릭 액션에 일어날 작용들 담기

    private void Awake()
    {
        throwButton.onClick.AddListener(() =>
        {
            SetInteractable(false); //더블 클릭 방지
            ThrowClicked?.Invoke();
        });
    }

    // 윷가락 결과를 보여준다. 이 연출 시점에 던지기 효과음을 재생한다
    public void ShowSticks(bool[] sticks)
    {
        SoundManager.Play(SoundId.YutThrow);

        for (int i = 0; i < stickImages.Length; i++)
        {
            bool hasStick = i < sticks.Length; //윷 개수 변경되면 유동적으로 체크하도록 변경
            stickImages[i].gameObject.SetActive(hasStick);
            if (!hasStick) continue;

            if (!sticks[i])
                stickImages[i].sprite = roundSprite;
            else if (i == StandardGetResult.MarkedStick)
                stickImages[i].sprite = markedFlatSprite;
            else
                stickImages[i].sprite = flatSprite;
        }
    }

    public void SetInteractable(bool on) => throwButton.interactable = on;
}