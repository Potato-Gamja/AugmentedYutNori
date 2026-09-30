// 증강 풀(AugmentPool): 무작위 선택지가 중복 없이, 팀별로 이미 뽑은 증강을 빼고 나오는지 검증하는 테스트
using NUnit.Framework;

public class AugmentPoolTests
{
    private static readonly string[] AllIds =
        { "WeakBoost", "Shield", "SturdyCarry", "QuickStart", "Rethrow", "BackDoSwitch" };

    private AugmentPool pool;

    // 각 테스트 직전에 새 풀을 만들어 6종을 등록한다 (테스트끼리 뽑은 기록이 섞이지 않게)
    [SetUp]
    public void SetUp()
    {
        pool = new AugmentPool();
        pool.Register("WeakBoost", team => new WeakBoostAugment(team));
        pool.Register("Shield", team => new ShieldAugment(team));
        pool.Register("SturdyCarry", team => new SturdyCarryAugment(team));
        pool.Register("QuickStart", team => new QuickStartAugment(team));
        pool.Register("Rethrow", team => new RethrowAugment(team));
        pool.Register("BackDoSwitch", team => new BackDoSwitchAugment(team));
    }

    [Test]
    public void GetRandomIds_ReturnsRequestedCount_NoDuplicates()
    {
        // 실행
        var ids = pool.GetRandomIds(0, 3);

        // 검증
        Assert.AreEqual(3, ids.Count);
        CollectionAssert.AllItemsAreUnique(ids);
    }

    [Test]
    public void GetRandomIds_MoreThanAvailable_ReturnsAll()
    {
        // 실행: 등록된 수(6)보다 많이 요청
        var ids = pool.GetRandomIds(0, 10);

        // 검증: 순서와 상관없이 6종 전부
        CollectionAssert.AreEquivalent(AllIds, ids);
    }

    [Test]
    public void AfterPick_PickedIdExcluded()
    {
        // 준비
        pool.Pick(0, "Shield");

        // 실행: 남은 것 전부 요청 (무작위여도 결과가 항상 같다)
        var ids = pool.GetRandomIds(0, 6);

        // 검증
        Assert.AreEqual(5, ids.Count);
        CollectionAssert.DoesNotContain(ids, "Shield");
    }

    [Test]
    public void Pick_DoesNotAffectOtherTeam()
    {
        // 준비: 팀 0이 방패를 뽑음
        pool.Pick(0, "Shield");

        // 실행: 팀 1이 요청
        var ids = pool.GetRandomIds(1, 6);

        // 검증: 뽑은 기록은 팀별로 따로라 팀 1은 방패를 받을 수 있다
        Assert.AreEqual(6, ids.Count);
        CollectionAssert.Contains(ids, "Shield");
    }

    [Test]
    public void Pick_CreatesAugmentForTeam()
    {
        // 실행
        IAugment augment = pool.Pick(1, "Shield");

        // 검증
        Assert.IsInstanceOf<ShieldAugment>(augment);
        Assert.AreEqual("Shield", augment.Id);
        Assert.AreEqual(1, augment.OwnerTeam);
    }

    [Test]
    public void AllPicked_ReturnsEmpty()
    {
        // 준비: 6종 전부 뽑기
        foreach (string id in AllIds) pool.Pick(0, id);

        // 실행
        var ids = pool.GetRandomIds(0, 3);

        // 검증
        Assert.IsEmpty(ids);
    }
}