// 증강 카탈로그(AugmentCatalog): json 정의와 생성 함수가 맞지 않을 때 예외를 던지는지 검증하는 테스트
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

public class AugmentCatalogTests
{
    private const string RealJsonPath = "Assets/Data/augments.json";   // EditMode 테스트는 프로젝트 루트 기준

    private static AugmentDefinitionFile MakeFile(params string[] ids)
    {
        AugmentDefinition[] augmentDefinitions = new AugmentDefinition[ids.Length];
        for (int i = 0; i < ids.Length; i++)
        {
            augmentDefinitions[i] = new AugmentDefinition { id = ids[i] };
        }

        return new AugmentDefinitionFile { version = 1, augments = augmentDefinitions };
    }

    // 카탈로그 검증만 보는 테스트라 어떤 증강을 만들지는 상관없다
    private static Dictionary<string, Func<int, IAugment>> MakeFactories(params string[] ids)
    {
        var augments = new Dictionary<string, Func<int, IAugment>>();

        foreach (var id in ids)
        {
            augments.Add(id, team => new BackDoSwitchAugment(team));
        }

        return augments;
    }

    private static AugmentDefinitionFile LoadRealFile()
    {
        string json = File.ReadAllText(RealJsonPath);
        return JsonUtility.FromJson<AugmentDefinitionFile>(json);
    }

    // ───── 생성자 검증 ─────

    [Test]
    public void Constructor_Valid_DoesNotThrow()
    {
        // 준비
        var file = MakeFile("A", "B");
        var factories = MakeFactories("A", "B");

        // 실행 + 검증
        Assert.DoesNotThrow(() => new AugmentCatalog(file, factories));
    }

    [Test]
    public void Constructor_NullAugments_Throws()
    {
        // 준비: 최상위 키 오타로 JsonUtility가 배열을 못 채운 상황
        var file = new AugmentDefinitionFile { version = 1, augments = null };

        // 실행 + 검증
        Assert.Throws<ArgumentException>(() => new AugmentCatalog(file, MakeFactories()));
    }

    [Test]
    public void Constructor_EmptyId_Throws()
    {
        // 준비: 키 오타(Id 등)로 id가 비어 들어온 상황
        var file = MakeFile("");

        // 실행 + 검증
        Assert.Throws<ArgumentException>(() => new AugmentCatalog(file, MakeFactories()));
    }

    [Test]
    public void Constructor_DuplicateId_Throws()
    {
        // 준비
        var file = MakeFile("A", "A");

        // 실행 + 검증
        Assert.Throws<ArgumentException>(() => new AugmentCatalog(file, MakeFactories("A")));
    }

    [Test]
    public void Constructor_MissingFactory_Throws()
    {
        // 준비: json에는 B가 있는데 생성 함수가 없다
        var file = MakeFile("A", "B");

        // 실행 + 검증
        Assert.Throws<ArgumentException>(() => new AugmentCatalog(file, MakeFactories("A")));
    }

    [Test]
    public void Constructor_MissingDefinition_Throws()
    {
        // 준비: 생성 함수에는 B가 있는데 json에 없다
        var file = MakeFile("A");

        // 실행 + 검증
        Assert.Throws<ArgumentException>(() => new AugmentCatalog(file, MakeFactories("A", "B")));
    }

    // ───── 조회와 등록 ─────

    [Test]
    public void Definitions_KeepJsonOrder()
    {
        // 준비: 생성 함수와 json의 순서를 일부러 다르게
        var file = MakeFile("B", "A");
        var factories = MakeFactories("A", "B");

        // 실행
        var catalog = new AugmentCatalog(file, factories);

        // 검증: json 순서를 따른다
        Assert.AreEqual("B", catalog.Definitions[0].id);
        Assert.AreEqual("A", catalog.Definitions[1].id);
    }

    [Test]
    public void Find_ReturnsDefinition()
    {
        // 준비
        var file = MakeFile("A");
        file.augments[0].name = "이름";
        file.augments[0].description = "설명";
        var catalog = new AugmentCatalog(file, MakeFactories("A"));

        // 실행
        AugmentDefinition definition = catalog.Find("A");

        // 검증
        Assert.AreEqual("이름", definition.name);
        Assert.AreEqual("설명", definition.description);
    }

    [Test]
    public void Find_UnknownId_Throws()
    {
        // 준비
        var catalog = new AugmentCatalog(MakeFile("A"), MakeFactories("A"));

        // 실행 + 검증
        Assert.Throws<KeyNotFoundException>(() => catalog.Find("Z"));
    }

    [Test]
    public void RegisterTo_RegistersAll()
    {
        // 준비
        var catalog = new AugmentCatalog(MakeFile("A", "B", "C"), MakeFactories("A", "B", "C"));
        var pool = new AugmentPool();

        // 실행
        catalog.RegisterTo(pool);

        // 검증: 등록된 수보다 많이 요청하면 전부 나온다 (무작위여도 결과가 항상 같다)
        CollectionAssert.AreEquivalent(new[] { "A", "B", "C" }, pool.GetRandomIds(0, 10));
    }

    // ───── 실제 augments.json ─────

    [Test]
    public void RealJson_MatchesFactories()
    {
        // 준비
        AugmentDefinitionFile file = LoadRealFile();

        // 실행 + 검증: json과 AugmentFactories가 서로 빠짐없이 맞는다
        Assert.DoesNotThrow(() => new AugmentCatalog(file, AugmentFactories.Create()));
    }

    [Test]
    public void RealJson_CreatedAugmentIdMatches()
    {
        // 준비
        AugmentDefinitionFile file = LoadRealFile();
        var factories = AugmentFactories.Create();

        // 실행 + 검증: 증강 클래스 안의 Id 오타(예: "Sheild")를 잡는다
        foreach (AugmentDefinition definition in file.augments)
        {
            IAugment augment = factories[definition.id](0);
            Assert.AreEqual(definition.id, augment.Id, $"{definition.id}의 생성 결과 Id가 다릅니다.");
        }
    }

    [Test]
    public void RealJson_AllHaveNameAndDescription()
    {
        // 준비
        AugmentDefinitionFile file = LoadRealFile();

        // 검증: 카드에 빈 이름/설명이 뜨지 않게
        foreach (AugmentDefinition definition in file.augments)
        {
            Assert.IsFalse(string.IsNullOrEmpty(definition.name), $"{definition.id}의 name이 비어 있습니다.");
            Assert.IsFalse(string.IsNullOrEmpty(definition.description), $"{definition.id}의 description이 비어 있습니다.");
        }
    }
}