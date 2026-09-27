using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class NodeView : MonoBehaviour
{
    [Header("노드 정보")]
    [SerializeField] private int nodeId;

    [Header("크기")]
    [SerializeField] private float normalScale = 1f;
    [SerializeField] private float bigScale = 1.5f;

    [Header("색상")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color highlightColor = Color.yellow;

    private SpriteRenderer _renderer;
    private BoardNode _node;

    public int NodeId => nodeId;

    public Vector3 Position => transform.position;

    private void Awake()
    {
        _renderer = GetComponent<SpriteRenderer>();
    }

    public void Init(BoardNode node)
    {
        if(node == null || node.Id != nodeId)
        {
            Debug.LogError($"[NodeView] id 불일치. view: {nodeId}, node: {node.Id}");
            return;
        }

        _node = node;

        bool isBig = node.IsCorner || node.IsCenter || node.IsStart;
        transform.localScale = Vector3.one * (isBig ? bigScale : normalScale);

        SetHighlight(false);
    }

    public void SetHighlight(bool on)
    {
        if (_renderer == null) return;
        _renderer.color = on ? highlightColor : normalColor;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        var style = new GUIStyle
        {
            fontSize = 14
        };
        style.normal.textColor = Color.black;
        UnityEditor.Handles.Label(transform.position + Vector3.up * .3f, nodeId.ToString(), style); ;
    }
#endif
}
