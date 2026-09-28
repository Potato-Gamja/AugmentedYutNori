// 말 상태. (팀 분담상 PieceState.cs 담당이 생기면 이 enum만 그 파일로 옮기면 된다)
public enum PieceState
{
    Waiting,  // 판에 안 나온 말 (Start 칸, 대기석)
    OnBoard,  // 판 위
    Finished, // 골인
}

// 말 데이터. 판 위 위치와 방향(직전 칸)만 들고, 상태 변경은 BoardState만 한다.
public class Piece
{
    public int Id { get; }   // 모든 기기에서 같은 말을 가리키는 고정 번호
    public int Team { get; } // 소유 팀(플레이어)
    public PieceState State { get; private set; } = PieceState.Waiting;
    public BoardNode Node { get; private set; }     // 현재 칸 (판에 안 나온 말과 골인한 말은 Start. 둘은 State로 구분)
    public BoardNode CameFrom { get; private set; } // 직전 칸 (판에 안 나온 말은 null)

    public bool IsWaiting => State == PieceState.Waiting;
    public bool IsOnBoard => State == PieceState.OnBoard;
    public bool IsFinished => State == PieceState.Finished;

    private readonly BoardNode startNode;

    public Piece(int id, int team, BoardNode startNode)
    {
        Id = id;
        Team = team;
        this.startNode = startNode;
        Node = startNode;
    }

    internal void Move(BoardPath path)
    {
        Node = path.Destination;
        CameFrom = path.CameFrom;

        if (path.IsFinished) State = PieceState.Finished;
        else if (Node == startNode) State = PieceState.Waiting;
        else State = PieceState.OnBoard;
    }

    // 잡혔을 때 판 밖(대기석)으로 되돌린다
    internal void ReturnToStart()
    {
        Node = startNode;
        CameFrom = null;
        State = PieceState.Waiting;
    }

    public override string ToString() => $"Piece{Id}(Team{Team}, {State}, {Node})";
}
