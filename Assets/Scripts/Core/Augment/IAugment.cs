using System.Collections.Generic;

// 증강 효과. BoardState가 말 이동을 계산/적용하는 단계마다 등록된 증강을 Priority 순서로 호출한다.
//
// 말 하나가 움직일 때 호출 순서
//   1. ModifyMoveCount : 굴린 칸 수 보정           (예: 빽도 전환, 잔걸음 강화, 빠른 출발)
//   2. ModifyPaths     : 계산된 경로 추가/제거      (예: 통행 금지 칸, 추가 지름길)
//   3. ModifyCaptures  : 잡힐 말 목록 보정          (예: 튼튼한 업기)
//   4. OnMoved         : 이동이 끝난 뒤 효과        (예: 도착 칸 효과)
//
// 증강은 모든 팀의 이동에 호출된다. 내 말에만 적용하려면 context.IsMine(this)로 확인할 것.
// 멀티플레이: 모든 기기에서 같은 결과가 나오도록 결정적으로 구현한다 (UnityEngine.Random, Time 등 사용 금지).
// 호출 도중 context.State.RemoveAugment(this)로 자기 자신을 제거해도 안전하다.
//
// 새 증강은 아래 Augment 클래스를 상속해 필요한 단계만 override 한다.
public interface IAugment
{
    string Id { get; }      // 증강 종류 식별자. 네트워크/저장에서 어떤 증강인지 주고받을 때 사용 (예: "Rethrow")
    int OwnerTeam { get; }  // 이 증강을 가진 팀
    int Priority { get; }   // 호출 순서. 낮을수록 먼저, 같으면 등록 순서

    void OnAttached(BoardState state);
    void OnDetached(BoardState state);

    int ModifyMoveCount(AugmentContext context, int moveCount);
    void ModifyPaths(AugmentContext context, List<BoardPath> paths);
    void ModifyCaptures(AugmentContext context, List<Piece> captured);
    void OnMoved(AugmentContext context, MoveResult result);
}

// 증강 구현용 기본 클래스. 모든 단계가 "효과 없음"으로 구현돼 있어 필요한 것만 override 하면 된다.
// (인터페이스 기본 구현 대신 이 클래스를 쓰는 이유: 중간 부모 클래스를 두면 자식의 메서드가 인터페이스와
//  연결되지 않고 조용히 무시되는 C# 함정이 있다. override는 이름/인자가 틀리면 컴파일 에러로 알려준다)
public abstract class Augment : IAugment
{
    public abstract string Id { get; }
    public int OwnerTeam { get; }
    public virtual int Priority => 0;

    protected Augment(int ownerTeam)
    {
        OwnerTeam = ownerTeam;
    }

    // BoardState에 등록/해제될 때
    public virtual void OnAttached(BoardState state) { }
    public virtual void OnDetached(BoardState state) { }

    // 1. 실제로 움직일 칸 수. moveCount는 앞선 증강까지 보정된 값
    public virtual int ModifyMoveCount(AugmentContext context, int moveCount) => moveCount;

    // 2. 갈 수 있는 경로 목록을 직접 고친다
    public virtual void ModifyPaths(AugmentContext context, List<BoardPath> paths) { }

    // 3. 이번 이동으로 잡힐 상대 말 목록을 직접 고친다. 목록에서 빼면 안 잡히고, 넣으면 추가로 잡힌다
    public virtual void ModifyCaptures(AugmentContext context, List<Piece> captured) { }

    // 4. 이동이 적용된 뒤
    public virtual void OnMoved(AugmentContext context, MoveResult result) { }

    public override string ToString() => $"{Id}(Team{OwnerTeam})";
}
