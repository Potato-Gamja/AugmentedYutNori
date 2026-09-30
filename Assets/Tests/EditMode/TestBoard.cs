// 증강 테스트용 일자형 미니 판(지름길 없음)을 BoardDefinition으로 만드는 헬퍼
// 판 모양 (length = 10일 때):
//   Start(0) → 1 → 2 → ... → 9 → End(10) → Start(0) 들어가면 골인
// 말을 원하는 칸에 놓을 때는 State.TryMove로 실제 이동시킨다 (Piece.Move는 internal이라 테스트에서 못 씀).
// 증강 효과가 끼어들지 않도록 말을 먼저 놓고 증강은 나중에 붙일 것.
public class TestBoard
{
    public BoardGraph Graph { get; }
    public BoardState State { get; }

    // length: 판 위 칸 수이자 End 칸 번호
    public TestBoard(int length = 10)
    {
        var definition = new BoardDefinition { startNodeId = 0 };
        for (int i = 0; i <= length; i++)
        {
            NodeDefinition nodeDefinition = new NodeDefinition { id = i };
            if (i == 0)
            {
                nodeDefinition.type = BoardNodeType.Start;
                nodeDefinition.next.Add(1);
            }
            else if (i == length)
            {
                // End를 지나 Start로 들어오면 골인 (BoardGraph.MoveForward)
                nodeDefinition.type = BoardNodeType.End;
                nodeDefinition.next.Add(0);
                nodeDefinition.prev.Add(i - 1);
            }
            else
            {
                nodeDefinition.type = BoardNodeType.Normal;
                nodeDefinition.next.Add(i + 1);
                if (i == 1)
                {
                    // 첫 칸에서 빽도하면 대기석(Start)이 아니라 참먹이(End)로 가야 하므로 Start가 아닌 End를 이전 칸으로 둔다
                    nodeDefinition.prev.Add(length);
                }
                else
                {
                    nodeDefinition.prev.Add(i - 1);
                }
            }
            definition.nodes.Add(nodeDefinition);
        }
        Graph = definition.CreateGraph();
        State = new BoardState(Graph);
    }

    public BoardNode Node(int id) => Graph.GetNode(id);
}