// 증강 풀(AugmentPool): 후보가 중복 없이 팀별로 나오는지, 랜덤 주입으로 결과가 정해지는지, 제시된 후보 안에서만 선택되는지 검증하는 테스트
using System;
using NUnit.Framework;

public class AugmentPoolTests
{
    // 등록 순서. 랜덤 값 0.0이면 이 순서 그대로 후보가 나온다
    private static readonly string[] AllIds =
        { "WeakBoost", "Shield", "SturdyCarry", "QuickStart", "Rethrow", "BackDoSwitch" };

    private AugmentPool pool;

    // 랜덤을 쓰면 테스트가 실패하도록 만든 가짜. 온라인 경로(SetOffered)가 랜덤을 안 쓰는지 확인할 때 사용
    private class ThrowingRandomProvider : IRandomProvider
    {
        public double NextDouble() => throw new InvalidOperationException("랜덤을 사용하면 안 되는 경로입니다.");
    }

    // AllIds 순서대로 6종을 등록한 풀을 만든다
    private static AugmentPool MakePool(IRandomProvider random)
    {
        var augmentPool = new AugmentPool(random);
        augmentPool.Register("WeakBoost", team => new WeakBoostAugment(team));
        augmentPool.Register("Shield", team => new ShieldAugment(team));
        augmentPool.Register("SturdyCarry", team => new SturdyCarryAugment(team));
        augmentPool.Register("QuickStart", team => new QuickStartAugment(team));
        augmentPool.Register("Rethrow", team => new RethrowAugment(team));
        augmentPool.Register("BackDoSwitch", team => new BackDoSwitchAugment(team));
        return augmentPool;
    }

    // 각 테스트 직전에 새 풀을 만든다 (테스트끼리 뽑은 기록이 섞이지 않게)
    [SetUp]
    public void SetUp()
    {
        pool = MakePool(new SystemRandomProvider());
    }

    // ───── 후보 뽑기 (기존) ─────

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
        pool.SetOffered(0, AllIds);
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
        pool.SetOffered(0, AllIds);
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
        pool.SetOffered(1, AllIds);
        IAugment augment = pool.Pick(1, "Shield");

        // 검증
        Assert.IsInstanceOf<ShieldAugment>(augment);
        Assert.AreEqual("Shield", augment.Id);
        Assert.AreEqual(1, augment.OwnerTeam);
    }

    [Test]
    public void AllPicked_ReturnsEmpty()
    {
        // 준비: 6종 전부 뽑기 (Pick 후 후보가 비워지므로 매번 다시 제시)
        foreach (string id in AllIds)
        {
            pool.SetOffered(0, new[] { id });
            pool.Pick(0, id);
        }

        // 실행
        var ids = pool.GetRandomIds(0, 3);

        // 검증
        Assert.IsEmpty(ids);
    }

    // ───── 랜덤 주입 ─────

    [Test]
    public void Constructor_NullRandom_Throws()
    {
        // 실행 + 검증
        Assert.Throws<ArgumentNullException>(() => new AugmentPool(null));
    }

    [Test]
    public void GetRandomIds_ZeroRandom_KeepsRegisterOrder()
    {
        // 준비: 0.0이면 매번 자기 자리와 바꾸므로 섞이지 않는다
        var zeroPool = MakePool(new SequenceRandomProvider(0.0));

        // 실행
        var ids = zeroPool.GetRandomIds(0, 3);

        // 검증: 등록 순서 앞에서 3개, 순서까지 같다
        CollectionAssert.AreEqual(new[] { "WeakBoost", "Shield", "SturdyCarry" }, ids);
    }

    [Test]
    public void GetRandomIds_HighRandom_PicksLast()
    {
        // 준비: 0 + (int)(0.99 * 6) = 5 → 마지막 원소가 맨 앞으로 온다
        var highPool = MakePool(new SequenceRandomProvider(0.99));

        // 실행
        var ids = highPool.GetRandomIds(0, 1);

        // 검증: 범위를 벗어나지 않고 마지막 증강이 나온다
        CollectionAssert.AreEqual(new[] { "BackDoSwitch" }, ids);
    }

    [Test]
    public void GetRandomIds_SameSequence_SameResult()
    {
        // 준비: 같은 랜덤 값을 넣은 풀 두 개
        var poolA = MakePool(new SequenceRandomProvider(0.3, 0.7, 0.1));
        var poolB = MakePool(new SequenceRandomProvider(0.3, 0.7, 0.1));

        // 실행
        var idsA = poolA.GetRandomIds(0, 3);
        var idsB = poolB.GetRandomIds(0, 3);

        // 검증: 순서까지 같다 (등록 순서가 고정돼 있으므로)
        CollectionAssert.AreEqual(idsA, idsB);
    }

    // ───── 로컬 후보(GetRandomIds) → Pick ─────

    [Test]
    public void Pick_FromRandomChoices_Succeeds()
    {
        // 준비
        var ids = pool.GetRandomIds(0, 3);

        // 실행
        IAugment augment = pool.Pick(0, ids[0]);

        // 검증
        Assert.AreEqual(ids[0], augment.Id);
    }

    [Test]
    public void Pick_WithoutChoices_Throws()
    {
        // 실행 + 검증: 후보를 제시하지 않고 고르면 실패
        Assert.Throws<InvalidOperationException>(() => pool.Pick(0, "Shield"));
    }

    [Test]
    public void Pick_NotInChoices_Throws()
    {
        // 준비
        pool.SetOffered(0, new[] { "Shield", "Rethrow" });

        // 실행 + 검증: 등록은 돼 있지만 제시되지 않은 증강
        Assert.Throws<ArgumentException>(() => pool.Pick(0, "WeakBoost"));
    }

    [Test]
    public void Pick_Twice_Throws()
    {
        // 준비: 한 번 고르면 후보가 비워진다
        pool.SetOffered(0, new[] { "Shield", "Rethrow" });
        pool.Pick(0, "Shield");

        // 실행 + 검증: 같은 후보로 한 번 더 고를 수 없다
        Assert.Throws<InvalidOperationException>(() => pool.Pick(0, "Rethrow"));
    }

    [Test]
    public void Pick_OtherTeamChoices_Throws()
    {
        // 준비: 팀 0에게만 후보 제시
        pool.SetOffered(0, new[] { "Shield" });

        // 실행 + 검증: 팀 1은 후보가 없다
        Assert.Throws<InvalidOperationException>(() => pool.Pick(1, "Shield"));
    }

    [Test]
    public void GetRandomIds_ReturnedListChanged_ChoicesKept()
    {
        // 준비: 반환된 목록을 바깥에서 비운다
        var ids = pool.GetRandomIds(0, 3);
        string first = ids[0];
        ids.Clear();

        // 실행 + 검증: 저장된 후보는 복사본이라 영향이 없다
        Assert.DoesNotThrow(() => pool.Pick(0, first));
    }

    // ───── 서버 후보(SetOffered) ─────

    [Test]
    public void SetOffered_ThenPick_Succeeds()
    {
        // 준비
        pool.SetOffered(0, new[] { "Shield", "Rethrow", "QuickStart" });

        // 실행
        IAugment augment = pool.Pick(0, "Rethrow");

        // 검증
        Assert.IsInstanceOf<RethrowAugment>(augment);
        Assert.AreEqual(0, augment.OwnerTeam);
    }

    [Test]
    public void SetOffered_Null_Throws()
    {
        // 실행 + 검증
        Assert.Throws<ArgumentNullException>(() => pool.SetOffered(0, null));
    }

    [Test]
    public void SetOffered_UnregisteredId_Throws()
    {
        // 실행 + 검증: augments.json에 없는 id
        Assert.Throws<ArgumentException>(() => pool.SetOffered(0, new[] { "Teleport" }));
    }

    [Test]
    public void SetOffered_AlreadyPickedId_Throws()
    {
        // 준비: 팀 0이 방패를 이미 고름
        pool.SetOffered(0, new[] { "Shield" });
        pool.Pick(0, "Shield");

        // 실행 + 검증
        Assert.Throws<ArgumentException>(() => pool.SetOffered(0, new[] { "Shield" }));
    }

    [Test]
    public void SetOffered_DuplicateId_Throws()
    {
        // 실행 + 검증
        Assert.Throws<ArgumentException>(() => pool.SetOffered(0, new[] { "Shield", "Shield" }));
    }

    [Test]
    public void SetOffered_Invalid_KeepsPreviousChoices()
    {
        // 준비: 정상 후보를 먼저 저장
        pool.SetOffered(0, new[] { "Shield" });

        // 실행: 잘못된 후보는 예외로 거부된다
        Assert.Throws<ArgumentException>(() => pool.SetOffered(0, new[] { "Teleport" }));

        // 검증: 이전 후보가 그대로 남아 있다
        Assert.DoesNotThrow(() => pool.Pick(0, "Shield"));
    }

    [Test]
    public void SetOffered_DoesNotUseRandom()
    {
        // 준비: 랜덤을 쓰면 예외가 나는 풀
        var noRandomPool = MakePool(new ThrowingRandomProvider());

        // 실행 + 검증: 서버 후보 저장과 선택은 랜덤 없이 끝난다
        Assert.DoesNotThrow(() =>
        {
            noRandomPool.SetOffered(0, new[] { "Shield" });
            noRandomPool.Pick(0, "Shield");
        });
    }
}