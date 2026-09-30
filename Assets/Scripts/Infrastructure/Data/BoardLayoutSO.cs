using System;
using System.Collections.Generic;
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
