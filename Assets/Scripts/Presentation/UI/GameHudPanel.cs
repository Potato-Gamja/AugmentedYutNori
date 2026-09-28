using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// LSU_Game의 상시 HUD.
/// UIManager의 Panel 레이어에서 생성되며, 게임 규칙은 처리하지 않고
/// 버튼 입력을 GameFlowController에 전달하고 화면 문구만 갱신한다.
/// </summary>
public class GameHudPanel : UIBase
{
    [SerializeField] private TMP_Text turnText;
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private TMP_Text instructionText;
    [SerializeField] private Button throwButton;

    public event Action ThrowClicked;

    // UIManager가 프리팹을 처음 생성했을 때 한 번 호출한다.
    public override void Init()
    {
        if (throwButton != null) throwButton.onClick.AddListener(() => ThrowClicked?.Invoke());
    }

    // Controller가 상태를 바꿀 때 호출하는 최소 표시 API.
    public void SetTurn(string value) { if (turnText != null) turnText.text = value; }
    public void SetResults(string value) { if (resultText != null) resultText.text = value; }
    public void SetInstruction(string value) { if (instructionText != null) instructionText.text = value; }
    public void SetThrowInteractable(bool value) { if (throwButton != null) throwButton.interactable = value; }
}
