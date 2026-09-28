using System;
using System.Collections.Generic;

// 판 위 모든 말의 상태와 규칙(업기/잡기/증강). 게임 상태는 여기서만 바뀐다.
// 멀티플레이: 이동은 MoveCommand(번호만)로 주고받고, 받은 쪽은 TryApply로 경로를 다시 계산해 검증한 뒤 적용한다.
public class BoardState
{
    private readonly Dictionary<int, Piece> pieces = new Dictionary<int, Piece>();
    private readonly List<IAugment> augments = new List<IAugment>();

    public BoardGraph Graph { get; }
    public IReadOnlyCollection<Piece> Pieces => pieces.Values;
    public IReadOnlyList<IAugment> Augments => augments;

    // 이동이 적용될 때마다 (로컬 입력이든 네트워크로 받은 명령이든) 호출. 화면 갱신/전송은 여기에 연결
    public event Action<MoveResult> Moved;

    public BoardState(BoardGraph graph)
    {
        Graph = graph;
    }

    // ---------- 말 ----------

    public Piece AddPiece(int id, int team)
    {
        var piece = new Piece(id, team, Graph.StartNode);
        pieces.Add(id, piece);
        return piece;
    }

    public Piece GetPiece(int id) => pieces.TryGetValue(id, out Piece piece) ? piece : null;

    // 판 위 해당 칸에 있는 말들 (대기/골인 말 제외)
    public List<Piece> GetPiecesAt(BoardNode node)
    {
        var result = new List<Piece>();
        foreach (Piece piece in pieces.Values)
        {
            if (piece.IsOnBoard && piece.Node == node) result.Add(piece);
        }
        return result;
    }

    // 함께 움직이는 말 묶음 = 같은 칸의 같은 팀 말 (업힌 말). 판에 안 나온 말은 자기 혼자
    public List<Piece> GetStack(Piece piece)
    {
        if (!piece.IsOnBoard) return new List<Piece> { piece };

        List<Piece> stack = GetPiecesAt(piece.Node);
        stack.RemoveAll(other => other.Team != piece.Team);
        return stack;
    }

    // ---------- 증강 ----------

    // Priority 순으로 끼워 넣는다 (같으면 등록 순서). 모든 기기에서 호출 순서가 같아야 결과가 같다
    public void AddAugment(IAugment augment)
    {
        if (augment == null || augments.Contains(augment)) return;

        int index = augments.FindIndex(other => other.Priority > augment.Priority);
        augments.Insert(index < 0 ? augments.Count : index, augment);
        augment.OnAttached(this);
    }

    public bool RemoveAugment(IAugment augment)
    {
        if (!augments.Remove(augment)) return false;

        augment.OnDetached(this);
        return true;
    }

    public List<IAugment> GetAugments(int team) => augments.FindAll(augment => augment.OwnerTeam == team);

    // 호출 도중 증강이 추가/제거돼도 안전하도록 복사본으로 돈다
    private IAugment[] SnapshotAugments() => augments.ToArray();

    // ---------- 이동 ----------

    // 말이 moveCount만큼 갈 수 있는 경로들 (증강 보정 적용)
    public List<BoardPath> GetMovePaths(Piece piece, int moveCount)
    {
        if (piece == null || piece.IsFinished) return new List<BoardPath>();

        AugmentContext context = CreateContext(piece, moveCount);
        return GetMovePaths(context);
    }

    private List<BoardPath> GetMovePaths(AugmentContext context)
    {
        List<BoardPath> paths = Graph.GetMovePaths(context.Piece.Node, context.Piece.CameFrom, context.MoveCount);
        foreach (IAugment augment in SnapshotAugments()) augment.ModifyPaths(context, paths);
        return paths;
    }

    // 이번 이동의 컨텍스트. ModifyMoveCount 단계까지 적용해 MoveCount를 확정한다
    private AugmentContext CreateContext(Piece piece, int rolledMoveCount)
    {
        var context = new AugmentContext(this, piece, GetStack(piece), rolledMoveCount);

        int count = rolledMoveCount;
        foreach (IAugment augment in SnapshotAugments()) count = augment.ModifyMoveCount(context, count);
        context.MoveCount = count;
        return context;
    }

    // destination으로 가는 경로가 규칙상 가능할 때만 이동. 로컬 입력과 네트워크 명령 모두 이걸 쓴다
    public bool TryMove(Piece piece, int moveCount, BoardNode destination, out MoveResult result)
    {
        result = null;
        if (piece == null || piece.IsFinished || destination == null) return false;

        AugmentContext context = CreateContext(piece, moveCount);
        BoardPath path = GetMovePaths(context).Find(p => p.Destination == destination);
        if (path == null) return false;

        result = Apply(context, path);
        return true;
    }

    public bool TryApply(MoveCommand command, out MoveResult result) =>
        TryMove(GetPiece(command.PieceId), command.MoveCount, Graph.GetNode(command.DestinationNodeId), out result);

    private MoveResult Apply(AugmentContext context, BoardPath path)
    {
        var moved = new List<Piece>(context.Stack);
        foreach (Piece member in moved) member.Move(path);

        // 도착 칸의 상대 팀 말이 기본 잡기 대상 (골인은 판 밖이라 제외). 증강이 목록을 고친 뒤 대기석으로
        var captured = new List<Piece>();
        if (!path.IsFinished)
        {
            foreach (Piece other in GetPiecesAt(path.Destination))
            {
                if (other.Team != context.Team) captured.Add(other);
            }
            foreach (IAugment augment in SnapshotAugments()) augment.ModifyCaptures(context, captured);

            var unique = new HashSet<Piece>();
            captured.RemoveAll(other => other == null || !other.IsOnBoard || moved.Contains(other) || !unique.Add(other));
            foreach (Piece other in captured) other.ReturnToStart();
        }

        var result = new MoveResult(context.Piece, context.RolledMoveCount, path, moved, captured);
        foreach (IAugment augment in SnapshotAugments()) augment.OnMoved(context, result);
        Moved?.Invoke(result);
        return result;
    }
}

// 한 번의 이동 결과
public class MoveResult
{
    public Piece Piece { get; }                          // 선택한 말
    public int MoveCount { get; }                        // 굴린 칸 수 (증강 보정 전)
    public BoardPath Path { get; }
    public IReadOnlyList<Piece> MovedPieces { get; }     // 같이 움직인 말 (업힌 말 포함)
    public IReadOnlyList<Piece> CapturedPieces { get; }  // 잡혀서 대기석으로 간 상대 말

    public MoveResult(Piece piece, int moveCount, BoardPath path, List<Piece> movedPieces, List<Piece> capturedPieces)
    {
        Piece = piece;
        MoveCount = moveCount;
        Path = path;
        MovedPieces = movedPieces;
        CapturedPieces = capturedPieces;
    }

    // 다른 기기에 보낼 명령
    public MoveCommand ToCommand() => new MoveCommand(Piece.Id, MoveCount, Path.Destination.Id);
}

// 네트워크/저장용 이동 명령. 참조 없이 번호만 담는다
[Serializable]
public struct MoveCommand
{
    public int PieceId;
    public int MoveCount;
    public int DestinationNodeId;

    public MoveCommand(int pieceId, int moveCount, int destinationNodeId)
    {
        PieceId = pieceId;
        MoveCount = moveCount;
        DestinationNodeId = destinationNodeId;
    }

    public override string ToString() => $"Move(piece {PieceId}, {MoveCount}칸, → node {DestinationNodeId})";
}
