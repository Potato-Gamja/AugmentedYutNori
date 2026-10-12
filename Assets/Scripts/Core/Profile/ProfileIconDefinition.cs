using System;

// profile_icons.json의 아이콘 한 개 정의 (id, 이름). 그림은 Resources/ProfileIcons/{id}.png
[Serializable]
public class ProfileIconDefinition
{
    public string id;
    public string name;
}
