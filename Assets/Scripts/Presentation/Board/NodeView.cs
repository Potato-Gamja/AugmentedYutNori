using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public enum DestinationBadgeType
{
    None,
    Capture,
    Carry,
    Finish
}

// 씬에서 판 모양을 만드는 칸. 연결은 인스펙터에서 직접 지정하고, 실행 시 BoardView가 BoardNode 데이터로 변환한다.
public class NodeView : MonoBehaviour
{
    [Tooltip("칸 고정 번호 (저장/네트워크/증강 효과용). 같은 번호 = 판 위의 같은 자리이므로, 판끼리 같은 자리의 칸은 같은 번호를 쓴다. 새 칸은 쓰지 않는 번호를 직접 지정")]
    [SerializeField] private int id = -1;
    [SerializeField] private BoardNodeType type = BoardNodeType.Normal;

    [Tooltip("Prev[i]에서 들어온 말은 Next[i]로 나간다. 짝이 없으면 Next[0]")]
    [SerializeField] private List<NodeView> nextNodes = new List<NodeView>();
    [SerializeField] private List<NodeView> prevNodes = new List<NodeView>();

    [Tooltip("이 칸에 멈춘 상태에서 출발할 때만 가는 칸 (지름길)")]
    [FormerlySerializedAs("shortcutNodes")]
    [SerializeField] private List<NodeView> shortcutNodes = new List<NodeView>();

    [Header("Highlight")]
    [Tooltip("칸 스프라이트 대비 아웃라인 크기 (클수록 테두리가 두꺼움)")]
    [SerializeField] private float outlineScale = 1.35f;
    [Tooltip("지정하면 복제 원 대신 P0 목적지 링을 사용한다.")]
    
    [Header("P0 Destination Badges")]
    [SerializeField] private Sprite captureBadgeSprite;
    [SerializeField] private Sprite carryRedBadgeSprite;
    [SerializeField] private Sprite carryBlueBadgeSprite;
    [SerializeField] private Sprite carryGreenBadgeSprite;
    [SerializeField] private Sprite carryYellowBadgeSprite;
    [SerializeField] private Sprite finishBadgeSprite;
    [SerializeField] private Sprite destinationHighlightSprite;
    [SerializeField] private List<Sprite> teamDestinationHighlightSprites = new();
    [SerializeField] private List<Sprite> teamResultBadgeSprites = new();

    
    private SpriteRenderer destinationBadge;
private SpriteRenderer outline;

    public int Id => id;
    public BoardNodeType Type => type;
    public IReadOnlyList<NodeView> NextNodes => nextNodes;
    public IReadOnlyList<NodeView> PrevNodes => prevNodes;
    public IReadOnlyList<NodeView> ShortcutNodes => shortcutNodes;

    // BoardView가 레이아웃(BoardLayoutSO)에서 칸을 만들 때 번호/종류/연결을 채운다
    internal void Configure(int nodeId, BoardNodeType nodeType, List<NodeView> next, List<NodeView> prev, List<NodeView> shortcut)
    {
        id = nodeId;
        type = nodeType;
        nextNodes = next;
        prevNodes = prev;
        shortcutNodes = shortcut;
    }

    // 화면에 보이는 칸의 중심 (자식 스프라이트만 옮겨도 따라가도록 스프라이트 기준). 화살표/말 이동/클릭 판정에 사용
    public Vector3 Position
    {
        get
        {
            SpriteRenderer sprite = GetComponentInChildren<SpriteRenderer>();
            return sprite != null ? sprite.transform.position : transform.position;
        }
    }

    // BoardView가 그래프를 만든 뒤 연결해 주는 데이터
    public BoardNode Node { get; private set; }

    public void Bind(BoardNode node) => Node = node;

    // 칸 테두리를 지정한 색으로 표시
    public void SetHighlight(Color color)
    {
        if (outline == null) outline = CreateOutline();
        if (outline == null) return;

        outline.color = color;
        outline.enabled = true;
    }

    public void SetHighlight(Color color, int team)
    {
        SetHighlight(color);
        if (outline == null || teamDestinationHighlightSprites == null ||
            team < 0 || team >= teamDestinationHighlightSprites.Count ||
            teamDestinationHighlightSprites[team] == null) return;

        outline.sprite = teamDestinationHighlightSprites[team];
        outline.transform.localScale = Vector3.one;
        outline.color = Color.white;
    }

    public Sprite GetResultBadgeSprite(int team)
    {
        return teamResultBadgeSprites != null && team >= 0 && team < teamResultBadgeSprites.Count
            ? teamResultBadgeSprites[team]
            : null;
    }

public void ClearHighlight()
    {
        if (outline != null) outline.enabled = false;
        if (destinationBadge != null) destinationBadge.enabled = false;
    }

    // 칸 스프라이트를 복제해 조금 크게 뒤에 깔아서 테두리처럼 보이게 한다
private SpriteRenderer CreateOutline()
    {
        SpriteRenderer source = GetComponentInChildren<SpriteRenderer>();
        if (source == null) return null;

        var go = new GameObject("Outline");
        go.transform.SetParent(source.transform, false);
        go.transform.localScale = destinationHighlightSprite != null ? Vector3.one : Vector3.one * outlineScale;

        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = destinationHighlightSprite != null ? destinationHighlightSprite : source.sprite;
        renderer.sortingLayerID = source.sortingLayerID;
        renderer.sortingOrder = destinationHighlightSprite != null ? source.sortingOrder + 2 : source.sortingOrder - 1;
        return renderer;
    }


/// <summary>
    /// 이동 후보의 잡기/업기/완주 의미를 목적지 위에 표시한다.
    /// 실제 판정은 GameRules와 BoardState가 담당하며 이 메서드는 표시만 변경한다.
    /// </summary>
    public void SetDestinationBadge(DestinationBadgeType badgeType, int movingTeam)
    {
        if (badgeType == DestinationBadgeType.None)
        {
            if (destinationBadge != null) destinationBadge.enabled = false;
            return;
        }

        if (destinationBadge == null)
        {
            SpriteRenderer source = GetComponentInChildren<SpriteRenderer>();
            if (source == null) return;
            var go = new GameObject("DestinationBadge");
            go.transform.SetParent(source.transform, false);
            go.transform.localPosition = new Vector3(0f, 0.72f, 0f);
            destinationBadge = go.AddComponent<SpriteRenderer>();
            destinationBadge.sortingLayerID = source.sortingLayerID;
            destinationBadge.sortingOrder = source.sortingOrder + 5;
        }

        destinationBadge.sprite = badgeType == DestinationBadgeType.Capture
            ? captureBadgeSprite
            : badgeType == DestinationBadgeType.Carry
                ? movingTeam switch
                {
                    0 => carryRedBadgeSprite,
                    1 => carryBlueBadgeSprite,
                    2 => carryGreenBadgeSprite,
                    _ => carryYellowBadgeSprite
                }
                : finishBadgeSprite;
        destinationBadge.enabled = destinationBadge.sprite != null;
    }
}
