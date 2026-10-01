// 다시 던지기(Rethrow): 사용 조건과 1회 소모를 검증하는 테스트
using NUnit.Framework;
using System.Linq;

public class RethrowAugmentTests
{
    private const int MyTeam = 0;
    private const int OtherTeam = 1;

    [Test]
    public void MyTurn_Do_CanUse()
    {
        // 준비: 이동 규칙과 상관없는 증강이라 말이 필요 없다
        var board = new TestBoard();
        var rethrow = new RethrowAugment(MyTeam);
        board.State.AddAugment(rethrow);

        // 실행 + 검증
        Assert.IsTrue(rethrow.CanUse(board.State, MyTeam, YutResult.Do));
    }

    [Test]
    public void OtherTurn_Do_CannotUse()
    {
        // 준비
        var board = new TestBoard();
        var rethrow = new RethrowAugment(MyTeam);
        board.State.AddAugment(rethrow);

        // 실행 + 검증: 상대 턴에는 쓸 수 없다
        Assert.IsFalse(rethrow.CanUse(board.State, OtherTeam, YutResult.Do));
    }

    [Test]
    public void MyTurn_YutOrMo_CannotUse()
    {
        // 준비
        var board = new TestBoard();
        var rethrow = new RethrowAugment(MyTeam);
        board.State.AddAugment(rethrow);

        // 실행 + 검증: 윷·모는 이미 한 번 더 던지므로 쓸 수 없다
        Assert.IsFalse(rethrow.CanUse(board.State, MyTeam, YutResult.Yut));
        Assert.IsFalse(rethrow.CanUse(board.State, MyTeam, YutResult.Mo));
    }

    [Test]
    public void TryUse_ConsumedOnce()
    {
        // 준비
        var board = new TestBoard();
        var rethrow = new RethrowAugment(MyTeam);
        board.State.AddAugment(rethrow);

        // 실행
        bool first = rethrow.TryUse(board.State, MyTeam, YutResult.Do);
        bool second = rethrow.TryUse(board.State, MyTeam, YutResult.Do);

        // 검증: 한 번 쓰면 사라지고, 두 번째는 실패한다
        Assert.IsTrue(first);
        Assert.IsFalse(second);
        Assert.IsFalse(board.State.Augments.Contains(rethrow));
    }

    [Test]
    public void TryUse_Yut_NotConsumed()
    {
        // 준비
        var board = new TestBoard();
        var rethrow = new RethrowAugment(MyTeam);
        board.State.AddAugment(rethrow);

        // 실행
        bool used = rethrow.TryUse(board.State, MyTeam, YutResult.Yut);

        // 검증: 조건이 안 맞으면 증강이 헛되이 사라지지 않는다
        Assert.IsFalse(used);
        Assert.IsTrue(board.State.Augments.Contains(rethrow));
    }
}