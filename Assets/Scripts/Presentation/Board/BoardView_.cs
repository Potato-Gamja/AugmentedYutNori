using UnityEngine;

/// <summary>
/// 윷놀이 판을 화면에 표현하는 View.
///
/// [역할]
/// - 각 BoardNode가 화면의 어느 위치에 있는지 관리한다.
/// - nodeId를 받아 해당 노드의 실제 Transform 위치를 반환한다.
/// - 추후 NodeView 생성 및 연결선 표현도 담당할 수 있다.
///
/// [주의]
/// - 이동 경로 계산은 하지 않는다.
/// - 지름길 판정도 하지 않는다.
/// - 빽도 처리도 하지 않는다.
///
/// 위와 같은 게임 규칙은 Core/Board/BoardGraph가 담당한다.
/// </summary>
public class BoardView_ : MonoBehaviour
{
    /// <summary>
    /// 각 노드의 화면 위치.
    ///
    /// 배열 인덱스와 BoardNode의 ID를 동일하게 맞추는 것을 권장한다.
    ///
    /// 예:
    /// nodePositions[0] = 출발점
    /// nodePositions[1] = 1번 노드
    /// nodePositions[2] = 2번 노드
    /// ...
    /// </summary>
    [SerializeField]
    private Transform[] nodePositions;


    /// <summary>
    /// 특정 nodeId에 해당하는 월드 좌표를 반환한다.
    ///
    /// PieceView가 말의 화면 위치를 변경할 때 사용한다.
    /// </summary>
    /// <param name="nodeId">
    /// BoardGraph에서 사용하는 노드 ID
    /// </param>
    /// <returns>
    /// 해당 노드의 Unity 월드 좌표
    /// </returns>
    public Vector3 GetNodePosition(int nodeId)
    {
        // 잘못된 ID가 들어왔을 경우
        // IndexOutOfRangeException이 발생하지 않도록 방어한다.
        if (nodeId < 0 || nodeId >= nodePositions.Length)
        {
            Debug.LogError(
                $"[BoardView] 존재하지 않는 Node ID입니다. nodeId = {nodeId}"
            );

            return Vector3.zero;
        }

        return nodePositions[nodeId].position;
    }


    /// <summary>
    /// 현재 등록된 노드의 개수를 반환한다.
    ///
    /// 디버깅이나 초기화 확인 시 사용할 수 있다.
    /// </summary>
    public int GetNodeCount()
    {
        return nodePositions.Length;
    }


#if UNITY_EDITOR

    /// <summary>
    /// Unity Editor의 Scene 화면에서
    /// 각 노드 위치를 쉽게 확인하기 위한 Gizmo.
    ///
    /// 실제 게임에는 영향을 주지 않는다.
    /// </summary>
    private void OnDrawGizmos()
    {
        if (nodePositions == null)
            return;

        foreach (Transform node in nodePositions)
        {
            if (node == null)
                continue;

            Gizmos.DrawWireSphere(node.position, 0.15f);
        }
    }

#endif
}