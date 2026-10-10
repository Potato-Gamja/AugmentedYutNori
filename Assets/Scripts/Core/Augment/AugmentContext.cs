using System.Collections.Generic;

// 증강이 판단에 쓰는 이번 이동의 정보. BoardState가 이동을 계산할 때마다 만들어 모든 증강에 같은 것을 넘긴다.
public class AugmentContext
{
    public BoardState State { get; }
    public BoardGraph Graph => State.Graph;

    public Piece Piece { get; }                // 선택한 말
    public int Team => Piece.Team;             // 움직이는 팀
    public IReadOnlyList<Piece> Stack { get; } // 같이 움직이는 말 (업힌 말 포함, 판 밖 말은 자기 혼자)

    public int RolledMoveCount { get; }        // 굴린 칸 수 (증강 보정 전)
    public int MoveCount { get; internal set; } // 실제로 움직일 칸 수 (ModifyMoveCount 단계가 끝난 뒤 확정)

    public bool IsWaiting => Piece.IsWaiting;  // 판에 안 나온 말이 출발하는 이동인지
    public bool IsBackward => MoveCount < 0;   // 뒤로 가는 이동인지
    public bool IsStacked => Stack.Count >= 2; // 업힌 말이 같이 움직이는지

    public AugmentContext(BoardState state, Piece piece, IReadOnlyList<Piece> stack, int rolledMoveCount)
    {
        State = state;
        Piece = piece;
        Stack = stack;
        RolledMoveCount = rolledMoveCount;
        MoveCount = rolledMoveCount;
    }

    // 움직이는 말이 이 증강 주인의 말인지 (내 말에만 적용되는 증강에서 사용)
    public bool IsMine(IAugment augment) => augment.OwnerTeam == Team;

    // piece가 이 증강 주인의 말인지 (프렌드쉴드처럼 상대 이동 중에 내 말을 지키는 증강에서 사용)
    public static bool IsOwnedBy(IAugment augment, Piece piece) => piece != null && augment.OwnerTeam == piece.Team;
}
