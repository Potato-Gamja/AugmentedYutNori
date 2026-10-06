using System;
using System.Collections.Generic;

// 로컬 플레이용 세션. 요청을 받으면 GameRules를 로컬 난수로 바로 실행하고,
// GameEvents로 나온 결과를 IGameSession 이벤트로 그대로 넘긴다 (지금 동작과 같음).
public class LocalGameSession : IGameSession, IDisposable
{
    private readonly GameRules rules;
    private readonly GameEvents events;

    // 증강 선택 진행 (팀 0부터 차례대로)
    private int augmentTeamCount;
    private int augmentChoiceCount;
    private int augmentSelectionTeam = -1;

    public event Action<YutThrowOutcome> Thrown;
    public event Action<MoveResult> Moved;
    public event Action<int> TurnChanged;
    public event Action<int, IReadOnlyList<string>> AugmentChoices;
    public event Action<int, IAugment> AugmentSelected;
    public event Action<int> GameEnded;
    public event Action<string> RequestRejected;

    // 로컬은 요청을 그 자리에서 처리하므로 기다리는 요청이 없다
    public bool IsCommandPending => false;

    // 증강 선택 중이면 지금 고르는 팀, 아니면 -1
    public int AugmentSelectionTeam => augmentSelectionTeam;

    public LocalGameSession(GameRules rules, GameEvents events)
    {
        this.rules = rules ?? throw new ArgumentNullException(nameof(rules));
        this.events = events ?? throw new ArgumentNullException(nameof(events));

        events.Thrown += OnThrown;
        events.PieceMoved += OnPieceMoved;
        events.TurnChanged += OnTurnChanged;
        events.GameEnded += OnGameEnded;
    }

    public void Dispose()
    {
        events.Thrown -= OnThrown;
        events.PieceMoved -= OnPieceMoved;
        events.TurnChanged -= OnTurnChanged;
        events.GameEnded -= OnGameEnded;
    }

    // 게임 시작 시 증강 선택을 시작한다. 팀 0의 후보부터 AugmentChoices로 알린다
    public void BeginAugmentSelection(int teamCount, int choiceCount = 3)
    {
        augmentTeamCount = teamCount;
        augmentChoiceCount = choiceCount;
        augmentSelectionTeam = 0;
        OfferAugments();
    }

    public void RequestThrow()
    {
        try
        {
            rules.ThrowYut(); // 결과는 GameEvents.Thrown → OnThrown
        }
        catch (InvalidOperationException exception)
        {
            RequestRejected?.Invoke(exception.Message);
        }
    }

    public void RequestMove(int pieceId, int resultIndex, int destinationNodeId)
    {
        if (!rules.MovePiece(pieceId, resultIndex, destinationNodeId))
            RequestRejected?.Invoke($"말 {pieceId}을(를) {destinationNodeId}번 칸으로 옮길 수 없습니다.");
    }

    public void RequestRethrow()
    {
        if (!rules.TryRethrow(out _))
            RequestRejected?.Invoke("지금은 다시 던질 수 없습니다.");
    }

    public void RequestAugment(string augmentId)
    {
        if (augmentSelectionTeam < 0)
        {
            RequestRejected?.Invoke("증강을 고를 차례가 아닙니다.");
            return;
        }

        int team = augmentSelectionTeam;
        IAugment augment;
        try
        {
            augment = rules.SelectAugment(team, augmentId); // 제시된 후보가 아니면 예외
        }
        catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
        {
            RequestRejected?.Invoke(exception.Message);
            return;
        }

        augmentSelectionTeam++;
        if (augmentSelectionTeam >= augmentTeamCount) augmentSelectionTeam = -1;

        AugmentSelected?.Invoke(team, augment);
        if (augmentSelectionTeam >= 0) OfferAugments();
    }

    private void OfferAugments()
    {
        List<string> choices = rules.GetAugmentChoices(augmentSelectionTeam, augmentChoiceCount);
        AugmentChoices?.Invoke(augmentSelectionTeam, choices);
    }

    private void OnThrown(YutThrowOutcome outcome) => Thrown?.Invoke(outcome);
    private void OnPieceMoved(MoveResult result) => Moved?.Invoke(result);
    private void OnTurnChanged(int team) => TurnChanged?.Invoke(team);
    private void OnGameEnded(int winnerTeam) => GameEnded?.Invoke(winnerTeam);
}
