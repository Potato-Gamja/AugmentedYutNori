using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// GameFlowController가 사용하는 HUD 창구다.
/// 기존 프리팹 참조와 공개 API를 유지하고 실제 표시는 전용 View 컴포넌트에 위임한다.
/// </summary>
public class GameHudPanel : UIBase
{
    [SerializeField] private TMP_Text turnText;
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private TMP_Text instructionText;
    [SerializeField] private Button throwButton;
    [SerializeField] private Button rethrowButton;

    [Header("P0 HUD")]
    [SerializeField] private RectTransform storedResultRoot;
    [SerializeField] private GameObject storedResultChipTemplate;
    [SerializeField] private Sprite chipNormal;
    [SerializeField] private Sprite chipBonus;
    [SerializeField] private Sprite chipConsumeRed;
    [SerializeField] private Sprite chipConsumeBlue;
    [SerializeField] private Sprite chipBack;
    [SerializeField] private Sprite resultRevealBackground;
    [SerializeField] private Sprite resultRevealGlow;
    [SerializeField] private Image timerFill;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private float visualTurnSeconds = 30f;

    private TurnHudView turnView;
    private PlayerStatusView playerView;
    private StoredResultView storedResultView;
    private YutResultRevealView resultRevealView;
    private int currentVisualTeam;
    private bool throwRequested;
    private bool rethrowRequested;
    private bool isLocalTurn = true;
    private bool isCommandPending;
    private bool isConnected = true;

    public event Action ThrowClicked;
    public event Action RethrowClicked;
    public event Action<int, int> WaitingPieceClicked;

    public override void Init()
    {
        turnView = GetOrAdd<TurnHudView>();
        playerView = GetOrAdd<PlayerStatusView>();
        storedResultView = GetOrAdd<StoredResultView>();
        resultRevealView = GetOrAdd<YutResultRevealView>();

        turnView.Configure(
            turnText,
            instructionText,
            throwButton,
            rethrowButton,
            timerFill,
            timerText,
            visualTurnSeconds);
        playerView.BindWaitingPieceClicks((team, slot) => WaitingPieceClicked?.Invoke(team, slot));
        storedResultView.Configure(
            resultText,
            storedResultRoot,
            storedResultChipTemplate,
            chipNormal,
            chipBonus,
            chipConsumeRed,
            chipConsumeBlue,
            chipBack);
        resultRevealView.Configure(resultText, resultRevealBackground, resultRevealGlow);

        if (throwButton != null)
        {
            throwButton.onClick.RemoveListener(RaiseThrowClicked);
            throwButton.onClick.AddListener(RaiseThrowClicked);
        }
        if (rethrowButton != null)
        {
            rethrowButton.onClick.RemoveListener(RaiseRethrowClicked);
            rethrowButton.onClick.AddListener(RaiseRethrowClicked);
        }
    }

    private void RaiseThrowClicked()
    {
        if (CanSendCommand()) ThrowClicked?.Invoke();
    }

    private void RaiseRethrowClicked()
    {
        if (CanSendCommand()) RethrowClicked?.Invoke();
    }

    public void SetTurn(string value) => SetTurn(value, currentVisualTeam);

    public void SetTurn(string value, int team)
    {
        currentVisualTeam = team;
        turnView?.SetTurn(value, team);
        playerView?.SetActiveTeam(team);
    }

    public void SetResults(string value) => storedResultView?.SetResults(value);
    public void SetInstruction(string value) => turnView?.SetInstruction(value);
    public void SetThrowInteractable(bool value)
    {
        throwRequested = value;
        RefreshActionAvailability();
    }

    public void SetRethrowVisible(bool value)
    {
        rethrowRequested = value;
        RefreshActionAvailability();
    }

    /// <summary>
    /// 로컬 플레이어의 차례와 서버 응답 대기 상태에 맞춰 액션 입력을 잠근다.
    /// </summary>
    public void SetInteractionState(bool localTurn, bool commandPending)
    {
        isLocalTurn = localTurn;
        isCommandPending = commandPending;
        RefreshActionAvailability();
    }

    /// <summary>
    /// 연결이 끊긴 동안 중복 요청이 나가지 않도록 모든 액션 입력을 잠근다.
    /// </summary>
    public void SetConnectionAvailable(bool connected)
    {
        isConnected = connected;
        RefreshActionAvailability();
    }
    public void SetTimerRunning(bool value) => turnView?.SetTimerRunning(value);

    public void SetPlayerState(
        int team,
        int waiting,
        int onBoard,
        int finished,
        IReadOnlyList<string> augmentIds)
    {
        playerView?.SetState(team, waiting, onBoard, finished, augmentIds);
    }

    public void SetSelectedWaitingPiece(int team, int slotIndex)
    {
        playerView?.SetSelectedWaitingPiece(team, slotIndex);
    }

    public void MarkResultPending(int resultIndex, int team)
    {
        storedResultView?.MarkPending(resultIndex, team);
    }

    public void ShowMoveOptionChoices(
        IReadOnlyList<string> optionLabels,
        int team,
        Action<int> onSelected)
    {
        storedResultView?.ShowChoices(optionLabels, team, onSelected);
    }

    public void HideMoveOptionChoices() => storedResultView?.HideChoices();

    public void ShowThrowResult(YutResult result, int remainingThrows, int team)
    {
        resultRevealView?.Show(result, remainingThrows, team);
    }

    public static Color32 GetTeamColor(int team) => TurnHudView.GetTeamColor(team);

    private bool CanSendCommand() => isConnected && isLocalTurn && !isCommandPending;

    private void RefreshActionAvailability()
    {
        bool canSend = CanSendCommand();
        turnView?.SetThrowInteractable(throwRequested && canSend);
        turnView?.SetRethrowVisible(rethrowRequested);
        if (rethrowButton != null) rethrowButton.interactable = rethrowRequested && canSend;

        // 규격 확인 필요: IGameSession.IsCommandPending과 로컬 플레이어 판정값을
        // GameFlowController에서 SetInteractionState에 전달한다.
    }

    private T GetOrAdd<T>() where T : Component
    {
        T component = GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
    }
}
