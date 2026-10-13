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

    // 판이 바뀌어 직전 칸이 없어졌을 때 (방향 정보만 버리고 위치는 그대로)
    internal void ForgetCameFrom() => CameFrom = null;

    // 잡혔을 때 판 밖(대기석)으로 되돌린다
    internal void ReturnToStart()
    {
        Node = startNode;
        CameFrom = null;
        State = PieceState.Waiting;
    }

    // 서버 스냅샷 상태로 그대로 맞춘다 (재접속·재동기화용). 이동 규칙을 거치지 않는다
    // node가 없으면 출발 칸
    internal void Restore(PieceState state, BoardNode node, BoardNode cameFrom)
    {
        State = state;
        Node = node ?? startNode;
        CameFrom = cameFrom;
    }

    public override string ToString() => $"Piece{Id}(Team{Team}, {State}, {Node})";
}
