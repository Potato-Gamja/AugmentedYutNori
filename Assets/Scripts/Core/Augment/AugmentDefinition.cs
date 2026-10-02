using System;

// augments.json의 증강 한 개 정의 (id, 이름, 설명, 1회용 표시 여부)
[Serializable]
public class AugmentDefinition
{
    public string id;
    public string name;
    public string description;
    public bool consumable;
}