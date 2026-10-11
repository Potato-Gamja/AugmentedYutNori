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


// 서버가 이동 이후 판단에 사용할 정보
public readonly struct MoveReport
{
    public int CapturedCount { get; }
    public bool IsFinished { get; }
    public bool HasExtraThrow { get; }
    public bool HasRemainingAction { get; }
    public string StateHash { get; }

    public MoveReport(
        int capturedCount,
        bool isFinished,
        bool hasExtraThrow,
        bool hasRemainingAction,
        string stateHash)
    {
        CapturedCount = capturedCount;
        IsFinished = isFinished;
        HasExtraThrow = hasExtraThrow;
        HasRemainingAction = hasRemainingAction;
        StateHash = stateHash;
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
        IGetResult getResult,
        AugmentPool augmentPool)
    {
        this.state = state;
        this.turnManager = turnManager;
        this.gameEvents = gameEvents;
        this.yutThrower = yutThrower;
        this.getResult = getResult;
        this.augmentPool = augmentPool;
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

        return ApplyThrow(outcome.Sticks, outcome.Result);
    }


    // 서버가 전달한 윷 결과 적용
    public YutThrowOutcome ApplyThrow(bool[] sticks, YutResult serverResult)
    {
        if (state.WinnerTeam.HasValue)
        {
            throw new InvalidOperationException("이미 게임이 종료되었습니다.");
        }

        if (!turnManager.HasRemainingThrow())
        {
            throw new InvalidOperationException("남은 윷 던지기 횟수가 없습니다.");
        }

        if (sticks == null)
        {
            throw new ArgumentNullException(nameof(sticks));
        }

        // 서버에서 받은 윷가락 결과를 클라이언트에서도 다시 판정
        YutResult result = getResult.GetResult(sticks);

        // 서버 결과와 클라이언트 판정 결과가 다르면 예외
        if (result != serverResult)
        {
            throw new InvalidOperationException(
                $"서버 윷 결과가 Core 판정과 일치하지 않습니다. " +
                $"Core={result}, Server={serverResult}");
        }

        // 도박사면 도↔모를 바꾼다 (윷가락 모양은 원래 결과 그대로)
        YutResult stored = HasGambler(turnManager.CurrentTeam) ? GamblerAugment.Swap(serverResult) : serverResult;
        bool swapped = stored != serverResult;

        YutThrowOutcome outcome = new YutThrowOutcome(sticks, stored, serverResult);

        // StoredResults = [Yut, Gae] 이런식으로 나중에 이동에 사용할 수 있도록 결과 저장
        state.StoredResults.Add(outcome.Result);

        // 던지기 횟수 소비 + 윷/모 추가 던지기 처리
        turnManager.ConsumeThrow(!swapped && GrantsExtraThrow(outcome.Result));

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

        // 상대 말을 잡으면 추가 던지기 (사냥꾼은 2번)
        if (result.CapturedPieces.Count > 0)
        {
            int bonus = HasHunter(turnManager.CurrentTeam) ? HunterAugment.CaptureBonusThrows : 1;
            turnManager.AddExtraThrow(bonus);
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


    // 이동 결과를 서버 판단용 정보로 변환
    public MoveReport BuildMoveReport(MoveResult result)
    {
        if (result == null)
        {
            throw new ArgumentNullException(nameof(result));
        }

        bool isFinished = false;

        // 같이 움직인 말 중 하나라도 완주했는지 확인
        foreach (Piece movedPiece in result.MovedPieces)
        {
            if (movedPiece.IsFinished)
            {
                isFinished = true;
                break;
            }
        }

        // MovePiece는 이동 전 RemainingThrows가 0인 상태에서 실행됨
        // 이동 뒤 RemainingThrows > 0이면 잡기로 추가 던지기가 생긴 것
        bool hasExtraThrow = turnManager.HasRemainingThrow();

        // 추가 던지기 또는 사용할 수 있는 윷 결과가 남아있는지 확인
        bool hasRemainingAction =
            turnManager.HasRemainingThrow() ||
            HasAnyStoredMove();

        return new MoveReport(
            result.CapturedPieces.Count,
            isFinished,
            hasExtraThrow,
            hasRemainingAction,
            StateHasher.HashHex(state, turnManager)
        );
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


    // 서버가 전달한 증강 후보 저장
    public void SetAugmentChoices(
        int team,
        IReadOnlyList<string> ids)
    {
        augmentPool.SetOffered(team, ids);
    }


    public IAugment SelectAugment(string id)
    {
        return SelectAugment(turnManager.CurrentTeam, id);
    }


    // MVP에서 두 플레이어가 순서대로 증강을 선택할 때 사용한다.
    public IAugment SelectAugment(int team, string id)
    {
        // Pick에서 서버 또는 로컬이 제시한 후보 안의 증강인지 확인
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

        // 증강 자체의 사용 조건 확인
        // CanUse 안에서 야바위꾼 사용 조건 5개 확인
        if (!augment.CanUse(
                state,
                turnManager.CurrentTeam))
        {
            return false;
        }

        // 기존 결과 기억
        outcome = yutThrower.Throw(
            StandardGetResult.StickCount,
            getResult
        );

        return ApplyRethrow(
            augment,
            outcome.Sticks,
            outcome.Result,
            out outcome
        );
    }

    // 이 팀이 사냥꾼을 가졌는지
    private bool HasHunter(int team) =>
        state.Board.GetAugments(team).Exists(augment => augment is HunterAugment);

    // 이번 결과로 한 번 더 던지는지 (사냥꾼을 가졌다면 윷·모여도 추가 던지기 없음)
    private bool GrantsExtraThrow(YutResult result) =>
        result.GrantsExtraThrow() && !HasHunter(turnManager.CurrentTeam);

    // 이 팀이 도박사를 가졌는지
    private bool HasGambler(int team) =>
        state.Board.GetAugments(team).Exists(augment => augment is GamblerAugment);

    // 서버에서 받은 재던지기 결과 적용
    public bool ApplyRethrow(
        bool[] sticks,
        YutResult result)
    {
        RethrowAugment rethrow = null;

        // 현재 팀이 가진 재던지기 증강 찾기
        foreach (IAugment augment in state.Board.GetAugments(turnManager.CurrentTeam))
        {
            if (augment is RethrowAugment found)
            {
                rethrow = found;
                break;
            }
        }

        if (rethrow == null)
            return false;

        return ApplyRethrow(
            rethrow,
            sticks,
            result,
            out _
        );
    }


    // 로컬과 서버 재던지기 결과를 실제 게임 상태에 적용
    private bool ApplyRethrow(
        RethrowAugment augment,
        bool[] sticks,
        YutResult result,
        out YutThrowOutcome outcome)
    {
        outcome = default;

        if (augment == null)
            return false;

        YutResult? latestResult = state.LatestYutResult;

        if (!latestResult.HasValue)
            return false;

        if (sticks == null)
        {
            throw new ArgumentNullException(nameof(sticks));
        }

        // 서버에서 받은 윷가락 결과를 클라이언트에서도 다시 판정
        YutResult checkedResult = getResult.GetResult(sticks);

        // 서버 결과와 클라이언트 판정 결과가 다르면 예외
        if (checkedResult != result)
        {
            throw new InvalidOperationException(
                $"서버 윷 결과가 Core 판정과 일치하지 않습니다. " +
                $"Core={checkedResult}, Server={result}");
        }

        // 증강 자체의 사용 조건 검사
        // CanUse() 안에서 야바위꾼 사용 조건 5개 확인
        if (!augment.CanUse(
                state,
                turnManager.CurrentTeam))
        {
            return false;
        }

        YutResult oldResult = latestResult.Value;

        outcome = new YutThrowOutcome(sticks, result);

        // 최근 결과를 새 결과로 교체
        int lastIndex = state.StoredResults.Count - 1;
        state.StoredResults[lastIndex] = outcome.Result;
        state.HasRethrownThisTurn = true;

        // 윷/모 추가 기회 여부 다시 계산
        turnManager.ReplaceThrowResult(
            GrantsExtraThrow(oldResult),
            GrantsExtraThrow(outcome.Result)
        );

        // 재던지기 결과 이벤트
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