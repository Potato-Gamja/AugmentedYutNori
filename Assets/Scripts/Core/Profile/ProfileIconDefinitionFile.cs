using System;

// profile_icons.json 파일 전체 구조 (버전과 아이콘 정의 목록)
[Serializable]
public class ProfileIconDefinitionFile
{
    public int version;
    public ProfileIconDefinition[] icons;
}
