using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

// 씬에서 판 모양을 만드는 칸. 연결은 인스펙터에서 직접 지정하고, 실행 시 BoardView가 BoardNode 데이터로 변환한다.
public class NodeView : MonoBehaviour
{
    [Tooltip("칸 고정 번호 (저장/네트워크/증강 효과용). BoardView 우클릭 메뉴 '칸 번호 다시 매기기'로 자동 지정")]
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

    private SpriteRenderer outline;

    public int Id => id;
    public BoardNodeType Type => type;
    public IReadOnlyList<NodeView> NextNodes => nextNodes;
    public IReadOnlyList<NodeView> PrevNodes => prevNodes;
    public IReadOnlyList<NodeView> ShortcutNodes => shortcutNodes;

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

    public void ClearHighlight()
    {
        if (outline != null) outline.enabled = false;
    }

    // 칸 스프라이트를 복제해 조금 크게 뒤에 깔아서 테두리처럼 보이게 한다
    private SpriteRenderer CreateOutline()
    {
        SpriteRenderer source = GetComponentInChildren<SpriteRenderer>();
        if (source == null) return null;

        var go = new GameObject("Outline");
        go.transform.SetParent(source.transform, false);
        go.transform.localScale = Vector3.one * outlineScale;

        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = source.sprite;
        renderer.sortingLayerID = source.sortingLayerID;
        renderer.sortingOrder = source.sortingOrder - 1;
        return renderer;
    }
}
