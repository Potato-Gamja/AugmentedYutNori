using System;
using System.Collections.Generic;

// 판 구성 데이터 (칸 번호/종류/연결만, 위치 없음). 직렬화 가능한 순수 C#이라
// ScriptableObject, JSON, 서버/네트워크 어디서든 같은 판을 만들 수 있다.
// 연결 규칙은 NodeView와 같다: prev[i]에서 들어온 말은 next[i]로 나간다.
[Serializable]
public class BoardDefinition
{
    public int startNodeId;
    public List<NodeDefinition> nodes = new List<NodeDefinition>();

    public NodeDefinition Find(int id) => nodes.Find(node => node.id == id);

    // 번호 중복, 없는 칸으로의 연결, 시작 칸(Start 종류, 판에 하나)을 검사한다
    public bool Validate(out string error)
    {
        var ids = new HashSet<int>();
        int startCount = 0;
        foreach (NodeDefinition node in nodes)
        {
            if (node.id < 0) { error = $"칸 번호가 음수입니다: {node.id}"; return false; }
            if (!ids.Add(node.id)) { error = $"칸 번호가 겹칩니다: {node.id}"; return false; }
            if (node.type == BoardNodeType.Start) startCount++;
        }

        if (startCount != 1) { error = $"종류가 Start인 칸이 {startCount}개입니다. 판에 하나만 있어야 합니다."; return false; }

        NodeDefinition start = Find(startNodeId);
        if (start == null) { error = $"시작 칸({startNodeId})이 없습니다."; return false; }
        if (start.type != BoardNodeType.Start) { error = $"시작 칸({startNodeId})의 종류가 Start가 아니라 {start.type}입니다."; return false; }

        foreach (NodeDefinition node in nodes)
        {
            foreach (List<int> links in new[] { node.next, node.prev, node.shortcut })
            {
                foreach (int id in links)
                {
                    if (!ids.Contains(id)) { error = $"{node.id}번 칸이 없는 칸({id})과 연결돼 있습니다."; return false; }
                }
            }
        }

        error = null;
        return true;
    }

    // 이 정의로 BoardGraph를 만든다 (화면 없이도 사용 가능: 서버 판정, 테스트). 잘못된 정의면 예외
    public BoardGraph CreateGraph()
    {
        var graph = new BoardGraph();
        graph.Rebuild(this);
        return graph;
    }
}

[Serializable]
public class NodeDefinition
{
    public int id;
    public BoardNodeType type;
    public List<int> next = new List<int>();
    public List<int> prev = new List<int>();
    public List<int> shortcut = new List<int>();
}
