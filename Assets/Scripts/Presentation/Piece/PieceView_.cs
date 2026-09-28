using System;
using UnityEngine;

/// <summary>
/// 게임판 위에 표시되는 '말' 하나를 담당하는 View.
///
/// [역할]
/// - 자신의 Piece ID를 가지고 있다.
/// - 플레이어가 말을 클릭하면 Controller에 알린다.
/// - Controller의 요청에 따라 말 위치를 변경한다.
/// - 선택 가능한 말인지 시각적으로 표시한다.
///
/// [중요]
/// PieceView는 GameRules를 직접 호출하지 않는다.
///
/// 입력 흐름:
///
/// Player Click
///     ↓
/// PieceView
///     ↓
/// GameFlowController
///     ↓
/// GameRules
///
/// View가 게임 규칙과 직접 연결되지 않도록 하기 위함이다.
/// </summary>
public class PieceView_ : MonoBehaviour
{
    // =========================================================
    // Piece Information
    // =========================================================

    /// <summary>
    /// Core의 Piece와 이 View를 연결하기 위한 ID.
    /// </summary>
    public int PieceId { get; private set; }


    // =========================================================
    // Click Callback
    // =========================================================

    /// <summary>
    /// 이 말이 클릭되었을 때 호출할 함수.
    ///
    /// 실제로는 GameFlowController.OnPieceClicked가 등록될 예정이다.
    /// </summary>
    private Action<int> onClicked;


    // =========================================================
    // Selection State
    // =========================================================

    /// <summary>
    /// 현재 이 말을 플레이어가 선택할 수 있는지 여부.
    ///
    /// false 상태에서는 클릭해도 아무 동작을 하지 않는다.
    /// </summary>
    private bool isSelectable;


    // =========================================================
    // Initialization
    // =========================================================

    /// <summary>
    /// PieceView를 초기화한다.
    ///
    /// 게임 시작 시 GameFlowController에서 호출한다.
    /// </summary>
    /// <param name="pieceId">
    /// 해당 말의 고유 ID
    /// </param>
    /// <param name="clickCallback">
    /// 말을 클릭했을 때 호출할 Controller 함수
    /// </param>
    public void Initialize(
        int pieceId,
        Action<int> clickCallback)
    {
        PieceId = pieceId;
        onClicked = clickCallback;
    }


    // =========================================================
    // Input
    // =========================================================

    /// <summary>
    /// Collider가 붙어 있는 오브젝트를
    /// 마우스로 클릭하면 Unity가 자동으로 호출한다.
    ///
    /// WebGL에서도 마우스 클릭 입력으로 사용할 수 있다.
    /// </summary>
    private void OnMouseDown()
    {
        // 현재 선택할 수 없는 말이라면 입력을 무시한다.
        if (!isSelectable)
            return;

        // Controller에 "이 말이 클릭되었다"고 알린다.
        onClicked?.Invoke(PieceId);
    }


    // =========================================================
    // Position
    // =========================================================

    /// <summary>
    /// 말을 지정된 위치로 즉시 이동시킨다.
    ///
    /// MVP에서는 연출 없이 즉시 이동한다.
    /// 추후 DOTween 등을 사용하여 노드 사이 이동 애니메이션을
    /// 추가할 수 있다.
    /// </summary>
    public void MoveTo(Vector3 position)
    {
        transform.position = position;
    }


    // =========================================================
    // Selection
    // =========================================================

    /// <summary>
    /// 이 말을 현재 선택 가능한 상태로 설정한다.
    ///
    /// 이동할 수 있는 말만 true로 지정하는 방식으로 사용할 수 있다.
    /// </summary>
    public void SetSelectable(bool selectable)
    {
        isSelectable = selectable;

        /*
         * TODO:
         * 선택 가능할 때 노란색 테두리 등
         * 시각적 표시를 추가한다.
         *
         * 예:
         *
         * selectionOutline.SetActive(selectable);
         */
    }


    /// <summary>
    /// 현재 선택 가능한 말인지 반환한다.
    /// </summary>
    public bool IsSelectable()
    {
        return isSelectable;
    }
}