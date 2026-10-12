using System;
using System.Collections.Generic;

// profile_icons.json 정의를 읽어 조회를 제공하는 카탈로그. 서버(profile-icons.json)와 id를 맞춘다
public class ProfileIconCatalog
{
    public const string SpriteFolder = "ProfileIcons";      // Resources 기준 폴더

    private readonly Dictionary<string, ProfileIconDefinition> definitionsById = new();

    public int Version { get; }
    public IReadOnlyList<ProfileIconDefinition> Definitions { get; }    // json 배열 순서 그대로

    public ProfileIconCatalog(ProfileIconDefinitionFile file)
    {
        if (file == null) throw new ArgumentNullException(nameof(file));
        if (file.icons == null)
            throw new ArgumentException("profile_icons.json에 'icons' 배열이 없습니다. 최상위 키 이름을 확인하세요.");

        foreach (ProfileIconDefinition definition in file.icons)
        {
            if (definition == null || string.IsNullOrEmpty(definition.id))
                throw new ArgumentException("profile_icons.json에 id가 비어 있는 아이콘이 있습니다. 키 이름(id)을 확인하세요.");

            if (definitionsById.ContainsKey(definition.id))
                throw new ArgumentException($"profile_icons.json에 '{definition.id}'가 두 번 있습니다.");

            definitionsById.Add(definition.id, definition);
        }

        Version = file.version;
        Definitions = new List<ProfileIconDefinition>(file.icons);
    }

    public bool Contains(string id) => id != null && definitionsById.ContainsKey(id);

    public ProfileIconDefinition Find(string id)        // UI용. 없으면 예외
    {
        if (id != null && definitionsById.TryGetValue(id, out ProfileIconDefinition definition))
            return definition;
        else
            throw new KeyNotFoundException($"'{id}'에 해당하는 프로필 아이콘 정의를 찾을 수 없습니다.");
    }

    public static string SpritePath(string id) => $"{SpriteFolder}/{id}";     // Resources.Load<Sprite>(SpritePath(id))
}
