using System.Collections.Generic;



// S→C AUGMENT_CHOICES  서버가 뽑은 후보 → GameRules.SetAugmentChoices
public class AugmentChoicesMessage
{
    public int team;
    public List<string> augmentIds = new List<string>();   // 개수 = 서버 augment-choice-count
    public int timeLimitSec;                                // 넘기면 서버가 자동 선택
}

// C→S SELECT_AUGMENT
public class SelectAugmentRequest
{
    public string augmentId;    // 받은 후보 안의 ID여야 한다
}

// S→C AUGMENT_SELECTED  선택 확정 (자동 선택 포함). 상대 화면 표시에도 사용
public class AugmentSelectedMessage
{
    public int team;
    public string augmentId;
    public bool autoSelected;   // 시간 초과로 서버가 고른 경우 true
}
