using System;
using System.Collections.Generic;

// 어떤 저장된 윷 결과를 사용해서, 어디까지 갈 수 있는지
public readonly struct MoveOption
{
    public int ResultIndex { get; }
    public YutResult Result { get; }
    public bool IsFinished { get; }
    public int ModifiedMoveCount { get; }
    public int DestinationNodeId { get; }

    public MoveOption(
        int resultIndex,
        YutResult result,
        int destinationNodeId,
        int modifiedMoveCount,
        bool isFinished)
    {
        ResultIndex = resultIndex;
        Result = result;
        DestinationNodeId = destinationNodeId;
        ModifiedMoveCount = modifiedMoveCount;
        IsFinished = isFinished;
    }
}

public class GameRules
{
    private readonly GameState state;
    private readonly TurnManager turnManager;
    private readonly GameEvents gameEvents;

    private readonly YutThrower yutThrower;
    private readonly IGetResult getResult;
    private readonly AugmentPool augmentPool;

    public GameRules(
        GameState state,
        TurnManager turnManager,
        GameEvents gameEvents,
        YutThrower yutThrower,
        IGetResult getResult)
    {
        this.state = state;
        this.turnManager = turnManager;
        this.gameEvents = gameEvents;
        this.yutThrower = yutThrower;
        this.getResult = getResult;

        augmentPool = CreateAugmentPool();
    }

    // 윷 던지기
    public YutThrowOutcome ThrowYut()
    {
        if (state.WinnerTeam.HasValue)
        {
            throw new InvalidOperationException("이미 게임이 종료되었습니다.");
        }
        
        if (!turnManager.HasRemainingThrow())
        {
            throw new InvalidOperationException("남은 윷 던지기 횟수가 없습니다.");
        }

        // StandardGetResult.StickCount는 기본 4개
        YutThrowOutcome outcome = yutThrower.Throw(
            StandardGetResult.StickCount,
            getResult
        );

        // StoredResults = [Yut, Gae] 이런식으로 나중에 이동에 사용할 수 있도록 결과 저장
        state.StoredResults.Add(outcome.Result);
        // 던지기 횟수 소비 + 윷/모 추가 던지기 처리
        turnManager.ConsumeThrow(outcome.Result);
        // 윷을 던졌다는 사실 외부에 알리기 (애니메이션이나, 결과 텍스트로 보여줄 수 있음)
        gameEvents.RaiseThrown(outcome);
        
        // 더 던질 수 있는지, 움직일 수 있는 말이 있는지 확인
        TryEndTurnIfNoAction();
        return outcome;
    }

    // 이동 가능한 선택지 조회
    public List<MoveOption> GetMoveOptions(int pieceId)
    {
        var options = new List<MoveOption>();

        if (state.WinnerTeam.HasValue)
            return options;

        // 윷/모 등으로 추가 던지기가 남아 있다면 먼저 전부 던져야 한다.
        if (turnManager.HasRemainingThrow())
            return options;

        Piece piece = state.Board.GetPiece(pieceId);

        if (piece == null)
            return options;

        // 현재 팀의 말만 이동 가능
        if (piece.Team != turnManager.CurrentTeam)
            return options;

        if (piece.IsFinished)
            return options;

        // 저장된 모든 윷 결과에 대해 이동 가능한 경로 확인
        for (int resultIndex = 0;
             resultIndex < state.StoredResults.Count;
             resultIndex++)
        {
            YutResult yutResult = state.StoredResults[resultIndex];

            int moveCount = yutResult.Steps();

            // 말이 해당 칸 수만큼 이동할 수 있는 모든 경로를 받음
            List<BoardPath> paths = state.Board.GetMovePaths(piece, moveCount);

            foreach (BoardPath path in paths)
            {
                options.Add(
                    new MoveOption(
                    resultIndex,
                    yutResult,
                    path.Destination.Id,
                    state.Board.GetModifiedMoveCount(piece, moveCount),
                    path.IsFinished
                    )
                );
            }
        }
        return options;
    }

    // 말 이동 (어떤 말, 어떤 윷 결과, 어느 목적지)
    public bool MovePiece(
        int pieceId,
        int resultIndex,
        int destinationNodeId)
    {
        if (state.WinnerTeam.HasValue)
            return false;

        // 아직 추가 던지기가 남아 있다면
        // 이동보다 던지기를 먼저 해야 함
        if (turnManager.HasRemainingThrow())
            return false;

        // 잘못된 결과 index 방지
        if (resultIndex < 0 || resultIndex >= state.StoredResults.Count)
        {
            return false;
        }

        Piece piece = state.Board.GetPiece(pieceId);

        if (piece == null)
            return false;

        // 상대 팀 말 이동 방지
        if (piece.Team != turnManager.CurrentTeam)
            return false;

        if (piece.IsFinished)
            return false;


        BoardNode destination = state.Board.Graph.GetNode(destinationNodeId);

        if (destination == null)
            return false;

        // 사용할 저장 결과를 가져와서 이동 칸 수로 변환
        YutResult yutResult = state.StoredResults[resultIndex];
        int moveCount = yutResult.Steps();

        // 실제 이동, 경로 검증, 잡기, 증강 처리는 BoardState에 맡긴다.
        bool moved = state.Board.TryMove(
            piece,
            moveCount,
            destination,
            out MoveResult result
        );

        if (!moved)
            return false;

        // 이동에 성공한 경우에만 사용한 윷 결과를 제거
        state.StoredResults.RemoveAt(resultIndex);
        state.HasMovedThisTurn = true;
        // 이동 이벤트
        gameEvents.RaisePieceMoved(result);

        // 잡힌 말 이벤트 (MoveResult에 잡힌 말 목록 이미 존재)
        foreach (Piece capturedPiece in result.CapturedPieces)
        {
            gameEvents.RaisePieceCaptured(capturedPiece);
        }

        // 상대 말을 잡으면 추가 던지기 1회
        if (result.CapturedPieces.Count > 0)
        {
            turnManager.AddExtraThrow();
        }

        // 같이 이동한 말 확인 (업힌 말이 있으면 여러개일 수 있어서)
        foreach (Piece movedPiece in result.MovedPieces)
        {
            if (movedPiece.IsFinished)
            {
                gameEvents.RaisePieceFinished(movedPiece);
            }
        }

        // 모든 말이 완주했다면 게임 종료
        if (TeamFinished(turnManager.CurrentTeam))
        {
            state.WinnerTeam = turnManager.CurrentTeam;
            gameEvents.RaiseGameEnded(turnManager.CurrentTeam);
            return true;
        }

        TryEndTurnIfNoAction();
        return true;
    }

    // 현재 저장된 윷 결과 중 하나라도 실제로 사용 가능한지 검사
    public bool HasAnyStoredMove()
    {
        foreach (Piece piece in state.Board.Pieces)
        {
            if (piece.Team != turnManager.CurrentTeam)
                continue;

            if (piece.IsFinished)
                continue;

            foreach (YutResult result in state.StoredResults)
            {
                int moveCount = result.Steps();

                List<BoardPath> paths =
                    state.Board.GetMovePaths(
                        piece,
                        moveCount
                    );

                if (paths.Count > 0)
                    return true;
            }
        }
        return false;
    }

    // 팀 완주 여부
    public bool TeamFinished(int team)
    {
        // 아예 해당 팀 말이 하나도 없는 경우의 변수
        bool hasPiece = false;

        foreach (Piece piece in state.Board.Pieces)
        {
            if (piece.Team != team)
                continue;

            // 최소 한개는 있다는 표시
            hasPiece = true;

            if (!piece.IsFinished)
                return false;
        }
        return hasPiece;
    }

    // 증강 선택
    // 현재 팀의 증강 후보를 조회할 때 사용하는 기본 API.
    public List<string> GetAugmentChoices(int count)
    {
        return GetAugmentChoices(turnManager.CurrentTeam, count);
    }

    // 게임 시작 단계에서 플레이어별 증강 후보를 조회한다.
    public List<string> GetAugmentChoices(int team, int count)
    {
        return augmentPool.GetRandomIds(team, count);
    }

    public IAugment SelectAugment(string id)
    {
        return SelectAugment(turnManager.CurrentTeam, id);
    }

    // MVP에서 두 플레이어가 순서대로 증강을 선택할 때 사용한다.
    public IAugment SelectAugment(int team, string id)
    {
        IAugment augment = augmentPool.Pick(team, id);
        state.Board.AddAugment(augment);
        gameEvents.RaiseAugmentSelected(augment);
        return augment;
    }

    // 재던지기 증강
    public bool TryRethrow(
        RethrowAugment augment,
        out YutThrowOutcome outcome)
    {
        outcome = default;

        if (augment == null)
            return false;

        // 이번 턴에 말을 한 번이라도 움직였다면 재던지기 사용 불가
        if (state.HasMovedThisTurn)
            return false;

        YutResult? latestResult = state.LatestYutResult;

        if (!latestResult.HasValue)
            return false;

        // 증강 자체의 사용 조건 검사 + 1회용 증강 소모
        // TryUse() 안에서 팀, 윷/모 여부, 실제 보유 여부 확인
        if (!augment.TryUse(
                state.Board,
                turnManager.CurrentTeam,
                latestResult.Value))
        {
            return false;
        }

        YutResult oldResult = latestResult.Value;

        // 기존 결과 기억
        outcome = yutThrower.Throw(
            StandardGetResult.StickCount,
            getResult
        );

        // 최근 결과를 새 결과로 교체
        int lastIndex = state.StoredResults.Count - 1;
        state.StoredResults[lastIndex] = outcome.Result;

        // 윷/모 추가 기회 여부 다시 계산
        turnManager.ReplaceThrowResult(
            oldResult,
            outcome.Result
        );

        gameEvents.RaiseThrown(outcome);
        gameEvents.RaiseThrown(outcome);

        // 교체된 결과도 이동할 수 없고 추가 던지기가 없다면 턴을 종료한다.
        TryEndTurnIfNoAction();

        return true;
    }

    // 턴 종료 처리
    private void TryEndTurnIfNoAction()
    {
        // 아직 던질 수 있으면 턴 유지
        if (turnManager.HasRemainingThrow())
            return;

        // 사용할 수 있는 저장 결과가 있으면 턴 유지
        if (HasAnyStoredMove())
            return;

        // 더 이상 사용할 수 없는 결과 제거
        state.StoredResults.Clear();
        EndTurn();
    }

    // 턴 종료
    private void EndTurn()
    {
        turnManager.NextTurn();
        gameEvents.RaiseTurnChanged(turnManager.CurrentTeam);
    }

    // CreateAugment 대체
    private static AugmentPool CreateAugmentPool()
    {
        var pool = new AugmentPool();

        pool.Register(
            "BackDoSwitch",
            team =>
                new BackDoSwitchAugment(team)
        );

        pool.Register(
            "QuickStart",
            team =>
                new QuickStartAugment(team)
        );

        pool.Register(
            "WeakBoost",
            team =>
                new WeakBoostAugment(team)
        );

        pool.Register(
            "Shield",
            team =>
                new ShieldAugment(team)
        );

        pool.Register(
            "SturdyCarry",
            team =>
                new SturdyCarryAugment(team)
        );

        pool.Register(
            "Rethrow",
            team =>
                new RethrowAugment(team)
        );

        return pool;
    }
    // Controller가 구체적인 RethrowAugment를 찾지 않도록
    // 현재 팀이 가진 재던지기 증강을 내부에서 조회한다.
    public bool TryRethrow(out YutThrowOutcome outcome)
    {
    RethrowAugment rethrow = null;

    foreach (IAugment augment in state.Board.GetAugments(turnManager.CurrentTeam))
    {
        if (augment is RethrowAugment found)
        {
            rethrow = found;
            break;
        }
    }

    return TryRethrow(rethrow, out outcome);
}
}