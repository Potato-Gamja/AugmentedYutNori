using System;

// augments.json 파일 전체 구조 (버전과 증강 정의 목록)
[Serializable]
public class AugmentDefinitionFile
{
    public int version;
    public AugmentDefinition[] augments;
}