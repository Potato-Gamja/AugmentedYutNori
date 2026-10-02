using System;
using System.Collections.Generic;

// augments.json 정의와 생성 함수를 맞춰 보고, 조회와 Pool 등록을 제공하는 카탈로그
public class AugmentCatalog
{
    private readonly Dictionary<string, AugmentDefinition> definitionsById = new();
    private readonly IReadOnlyDictionary<string, Func<int, IAugment>> factories;

    public int Version { get; }
    public IReadOnlyList<AugmentDefinition> Definitions { get; }    // json 배열 순서 그대로

    public AugmentCatalog(AugmentDefinitionFile file,
                          IReadOnlyDictionary<string, Func<int, IAugment>> factories)
    {
        if (file == null) throw new ArgumentNullException(nameof(file));
        if (factories == null) throw new ArgumentNullException(nameof(factories));
        if (file.augments == null)
            throw new ArgumentException("augments.json에 'augments' 배열이 없습니다. 최상위 키 이름을 확인하세요.");

        foreach (AugmentDefinition definition in file.augments)
        {
            if (definition == null || string.IsNullOrEmpty(definition.id))
                throw new ArgumentException("augments.json에 id가 비어 있는 증강이 있습니다. 키 이름(id)을 확인하세요.");

            if (definitionsById.ContainsKey(definition.id))
                throw new ArgumentException($"augments.json에 '{definition.id}'가 두 번 있습니다.");

            if (!factories.ContainsKey(definition.id))
                throw new ArgumentException($"augments.json의 '{definition.id}'에 해당하는 생성 함수가 없습니다. AugmentFactories를 확인하세요.");

            definitionsById.Add(definition.id, definition);
        }

        foreach (string id in factories.Keys)
        {
            if (!definitionsById.ContainsKey(id))
                throw new ArgumentException($"생성 함수 '{id}'가 augments.json에 없습니다.");
        }

        this.factories = factories;
        Version = file.version;
        Definitions = new List<AugmentDefinition>(file.augments);
    }

    public AugmentDefinition Find(string id)        // UI용. 없으면 예외
    {
        if (definitionsById.TryGetValue(id, out AugmentDefinition definition)) 
            return definition;
        else 
            throw new KeyNotFoundException($"'{id}'에 해당하는 증강 정의를 찾을 수 없습니다.");
    }

    public void RegisterTo(AugmentPool pool)        // Definitions 순서대로 pool.Register
    {
        if (pool == null) throw new ArgumentNullException(nameof(pool));
        foreach (AugmentDefinition definition in Definitions)
        {
            pool.Register(definition.id, factories[definition.id]);
        }
    }
}