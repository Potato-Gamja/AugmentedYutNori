using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

// 판 하나 = 에셋 하나. 판 구성(BoardDefinition)과 칸 위치를 함께 저장한다.
// 만들기: 씬에서 판을 배치한 뒤 BoardView 우클릭 메뉴 '현재 판을 레이아웃으로 저장'
// 쓰기: BoardView의 Layout에 지정하면 실행할 때 이 배치대로 칸을 생성한다
[CreateAssetMenu(fileName = "BoardLayout", menuName = "YutNori/Board Layout")]
public class BoardLayoutSO : ScriptableObject
{
    [SerializeField] private BoardDefinition definition = new BoardDefinition();

    [Tooltip("칸 위치 (BoardView의 칸 부모 기준 로컬 좌표)")]
    [SerializeField] private List<NodeLayout> layouts = new List<NodeLayout>();

    public BoardDefinition Definition => definition;
    public IReadOnlyList<NodeLayout> Layouts => layouts;

    public bool TryGetPosition(int nodeId, out Vector3 localPosition)
    {
        int index = layouts.FindIndex(layout => layout.nodeId == nodeId);
        localPosition = index >= 0 ? layouts[index].localPosition : Vector3.zero;
        return index >= 0;
    }

    public void Set(BoardDefinition newDefinition, List<NodeLayout> newLayouts)
    {
        definition = newDefinition;
        layouts = newLayouts;
    }

    // 서버 판 id. 에셋 이름에서 "BoardLayout_"을 떼고 케밥 표기로 바꾼다
    //   BoardLayout_Default → default, BoardLayout_LongDiagonal → long-diagonal
    public string ServerBoardId => ToServerBoardId(name);

    public static string ToServerBoardId(string assetName)
    {
        const string prefix = "BoardLayout_";
        string baseName = assetName.StartsWith(prefix, StringComparison.Ordinal) ? assetName.Substring(prefix.Length) : assetName;

        var builder = new StringBuilder();
        for (int i = 0; i < baseName.Length; i++)
        {
            char c = baseName[i];
            if (char.IsUpper(c) && i > 0 && baseName[i - 1] != '-' && baseName[i - 1] != '_') builder.Append('-');
            builder.Append(c == '_' ? '-' : char.ToLowerInvariant(c));
        }
        return builder.ToString();
    }

    // 서버 resources/boards/{ServerBoardId}.json과 같은 형식의 json (위치는 빼고 칸 번호·종류·연결만).
    // 서버도 이 판으로 이동을 계산하므로 칸 번호와 연결이 Unity와 같아야 한다. 파일 쓰기는 Editor/BoardLayoutExporter
    //   - 칸은 번호 순, 칸 하나당 한 줄, 종류는 서버 enum 이름(NORMAL, START, END, CORNER, CENTER)
    //   - 줄바꿈 LF, 마지막 줄바꿈 포함 (서버 저장소 파일과 글자까지 같게)
    public string ToServerJson()
    {
        var nodes = new List<NodeDefinition>(definition.nodes);
        nodes.Sort((a, b) => a.id.CompareTo(b.id));

        var builder = new StringBuilder();
        builder.Append("{\n");
        builder.Append("  \"startNodeId\": ").Append(definition.startNodeId).Append(",\n");
        builder.Append("  \"nodes\": [\n");
        for (int i = 0; i < nodes.Count; i++)
        {
            NodeDefinition node = nodes[i];
            builder.Append("    {\"id\": ").Append(node.id)
                .Append(", \"type\": \"").Append(node.type.ToString().ToUpperInvariant()).Append('"')
                .Append(", \"next\": ").Append(ToJsonArray(node.next))
                .Append(", \"prev\": ").Append(ToJsonArray(node.prev))
                .Append(", \"shortcut\": ").Append(ToJsonArray(node.shortcut))
                .Append(i < nodes.Count - 1 ? "},\n" : "}\n");
        }
        builder.Append("  ]\n");
        builder.Append("}\n");
        return builder.ToString();
    }

    private static string ToJsonArray(List<int> values)
    {
        return values == null || values.Count == 0 ? "[]" : "[" + string.Join(", ", values) + "]";
    }
}

[Serializable]
public struct NodeLayout
{
    public int nodeId;
    public Vector3 localPosition;

    public NodeLayout(int nodeId, Vector3 localPosition)
    {
        this.nodeId = nodeId;
        this.localPosition = localPosition;
    }
}
