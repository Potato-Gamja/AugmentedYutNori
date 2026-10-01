using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 턴 배너, 시각 타이머와 액션 버튼의 팀 색을 담당한다.
/// 게임 규칙이나 실제 턴 종료 처리는 하지 않는다.
/// </summary>
public sealed class TurnHudView : MonoBehaviour
{
    private static readonly Color32[] TeamColors =
    {
        new(214, 64, 60, 255),
        new(46, 107, 209, 255),
        new(47, 157, 106, 255),
        new(224, 137, 18, 255)
    };

    private static readonly Color32 DisabledButtonColor = new(190, 181, 163, 255);

    private TMP_Text turnText;
    private TMP_Text instructionText;
    private Button throwButton;
    private Button rethrowButton;
    private Image timerFill;
    private TMP_Text timerText;
    private float visualTurnSeconds;
    private float timerRemaining;
    private bool timerRunning;
    private string lastTurn;
    private int currentTeam;

    public void Configure(
        TMP_Text turn,
        TMP_Text instruction,
        Button throwAction,
        Button rethrowAction,
        Image timerProgress,
        TMP_Text timerLabel,
        float turnSeconds)
    {
        turnText = turn;
        instructionText = instruction;
        throwButton = throwAction;
        rethrowButton = rethrowAction;
        timerFill = timerProgress;
        timerText = timerLabel;
        visualTurnSeconds = turnSeconds;
        timerRemaining = visualTurnSeconds;
        RefreshTimer();
        ApplyTurnVisuals();
    }

    private void Update()
    {
        if (!timerRunning || timerText == null || timerRemaining <= 0f) return;
        timerRemaining = Mathf.Max(0f, timerRemaining - Time.deltaTime);
        RefreshTimer();
    }

    public void SetTurn(string value, int team)
    {
        currentTeam = NormalizeTeamIndex(team);
        if (turnText != null) turnText.text = value;

        if (lastTurn != value)
        {
            lastTurn = value;
            timerRemaining = visualTurnSeconds;
            RefreshTimer();
        }

        ApplyTurnVisuals();
    }

    public void SetInstruction(string value)
    {
        if (instructionText != null) instructionText.text = value;
    }

    public void SetThrowInteractable(bool value)
    {
        if (throwButton == null) return;
        throwButton.interactable = value;
        ApplyButtonColor(throwButton, value ? GetTeamColor(currentTeam) : DisabledButtonColor);
    }

    public void SetRethrowVisible(bool value)
    {
        if (rethrowButton == null) return;
        rethrowButton.gameObject.SetActive(value);
        ApplyButtonColor(rethrowButton, GetTeamColor(currentTeam));
    }

    public void SetTimerRunning(bool value)
    {
        timerRunning = value;
    }

    private void ApplyTurnVisuals()
    {
        Color32 teamColor = GetTeamColor(currentTeam);
        if (turnText != null) turnText.color = teamColor;

        Image border = transform.Find("TurnBanner/Border")?.GetComponent<Image>();
        if (border != null) border.color = teamColor;

        ApplyButtonColor(throwButton,
            throwButton != null && throwButton.interactable ? teamColor : DisabledButtonColor);
        ApplyButtonColor(rethrowButton, teamColor);
    }

    private void RefreshTimer()
    {
        if (timerText != null) timerText.text = Mathf.CeilToInt(timerRemaining).ToString();
        if (timerFill == null) return;

        timerFill.fillAmount = visualTurnSeconds <= 0f ? 0f : timerRemaining / visualTurnSeconds;
        timerFill.color = timerRemaining <= 5f ? new Color32(214, 64, 60, 255) :
            timerRemaining <= 10f ? new Color32(232, 133, 12, 255) : GetTeamColor(currentTeam);
    }

    private static void ApplyButtonColor(Button button, Color32 color)
    {
        if (button == null) return;
        Image image = button.targetGraphic as Image;
        if (image == null) image = button.GetComponent<Image>();
        if (image != null) image.color = color;

        Image border = button.transform.Find("P0Border")?.GetComponent<Image>();
        if (border != null) border.color = color;

        Image highlight = button.transform.Find("P0Highlight")?.GetComponent<Image>();
        if (highlight != null) highlight.color = color;
    }

    private static int NormalizeTeamIndex(int team)
    {
        int count = TeamColors.Length;
        return ((team % count) + count) % count;
    }

    public static Color32 GetTeamColor(int team)
    {
        return TeamColors[NormalizeTeamIndex(team)];
    }
}
