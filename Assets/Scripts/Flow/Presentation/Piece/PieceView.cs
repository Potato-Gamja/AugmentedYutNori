using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 말 표시. 판에 안 나온 동안은 처음 놓인 자리(대기석)에 있고, 이동은 받은 좌표를 따라 한 칸씩 움직인다.
public class PieceView : MonoBehaviour
{
    [SerializeField] private int team;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private float stepDuration = 0.15f;
    [SerializeField] private Color selectedColor = new Color(1f, 0.85f, 0.3f);
    [SerializeField] private Color turnHighlightColor = new Color(0.35f, 1f, 1f);
    [SerializeField] private float highlightOutlineScale = 1.3f;

    [Header("업기 숫자")]
    [SerializeField] private Color stackTextColor = Color.white;
    [SerializeField] private float stackTextSize = 0.08f;

    private Vector3 homePosition;
    private Color normalColor;
    private TextMesh stackText;
    private SpriteRenderer highlightOutline;
    private bool isSelected;
    private bool isTurnHighlighted;

    public int Team => team;
    public Piece Piece { get; private set; }
    public bool IsMoving { get; private set; }

    private void Awake()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        homePosition = transform.position;
        normalColor = spriteRenderer.color;
        highlightOutline = CreateHighlightOutline();
        stackText = CreateStackText();
        SetStackCount(1);
    }

    public void Bind(Piece piece)
    {
        Piece = piece;
        transform.position = homePosition;
        gameObject.SetActive(true);
    }

    // 잡혔을 때 대기석으로 돌아감
    public void ReturnHome()
    {
        transform.position = homePosition;
        SetStackCount(1);
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;
        RefreshHighlight();
    }

    // 현재 차례에 선택 가능한 말은 청록색 외곽선으로 표시한다.
    // 선택된 말은 RefreshHighlight에서 selectedColor(노란색)를 우선 적용한다.
    public void SetTurnHighlighted(bool highlighted)
    {
        isTurnHighlighted = highlighted;
        RefreshHighlight();
    }

    private void RefreshHighlight()
    {
        spriteRenderer.color = normalColor;
        if (highlightOutline == null) return;
        highlightOutline.color = isSelected ? selectedColor : turnHighlightColor;
        highlightOutline.enabled = isSelected || isTurnHighlighted;
    }

    // 이동 애니메이션 후 같은 노드의 팀별 표시 위치를 보정할 때 사용한다.
    // Core의 Piece.Node는 바꾸지 않고 View 좌표만 변경한다.
    public void SetBoardPosition(Vector3 position)
    {
        transform.position = new Vector3(position.x, position.y, transform.position.z);
    }

    // 업힌 말 수. 2개 이상일 때만 숫자 표시
    public void SetStackCount(int count)
    {
        stackText.text = count >= 2 ? count.ToString() : "";
    }

    // 업힌 말끼리 같은 자리에 겹치므로, 맨 위에 그려질 말을 정한다
    public void SetSortingOrder(int order)
    {
        spriteRenderer.sortingOrder = order;
        if (highlightOutline != null) highlightOutline.sortingOrder = order - 1;
        stackText.GetComponent<MeshRenderer>().sortingOrder = order + 1;
    }

    public bool Contains(Vector3 worldPosition)
    {
        if (!gameObject.activeInHierarchy) return false;

        Vector3 center = transform.position;
        center.z = worldPosition.z;
        return Vector3.Distance(center, worldPosition) <= spriteRenderer.bounds.extents.x;
    }

    // points: 지나갈 칸들의 월드 좌표. hideAtEnd: 골인하면 도착 후 사라짐
    public void PlayMove(IReadOnlyList<Vector3> points, bool hideAtEnd, Action onComplete)
    {
        StartCoroutine(MoveRoutine(points, hideAtEnd, onComplete));
    }

    private IEnumerator MoveRoutine(IReadOnlyList<Vector3> points, bool hideAtEnd, Action onComplete)
    {
        IsMoving = true;

        foreach (Vector3 point in points)
        {
            Vector3 from = transform.position;
            Vector3 to = new Vector3(point.x, point.y, from.z);

            for (float t = 0f; t < stepDuration; t += Time.deltaTime)
            {
                transform.position = Vector3.Lerp(from, to, t / stepDuration);
                yield return null;
            }
            transform.position = to;
        }

        IsMoving = false;
        if (hideAtEnd) gameObject.SetActive(false);
        onComplete?.Invoke();
    }

    private TextMesh CreateStackText()
    {
        var go = new GameObject("StackCount");
        go.transform.SetParent(transform, false);

        var text = go.AddComponent<TextMesh>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 64;
        text.characterSize = stackTextSize;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.fontStyle = FontStyle.Bold;
        text.color = stackTextColor;

        var textRenderer = go.GetComponent<MeshRenderer>();
        textRenderer.sharedMaterial = text.font.material;
        textRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
        textRenderer.sortingOrder = spriteRenderer.sortingOrder + 1;
        return text;
    }

    private SpriteRenderer CreateHighlightOutline()
    {
        // 원본 말 색을 바꾸지 않고 테두리만 빛나게 보이도록
        // 같은 스프라이트를 뒤에 조금 크게 배치한다.
        var go = new GameObject("TurnHighlightOutline");
        go.transform.SetParent(spriteRenderer.transform, false);
        go.transform.localScale = Vector3.one * highlightOutlineScale;
        var outline = go.AddComponent<SpriteRenderer>();
        outline.sprite = spriteRenderer.sprite;
        outline.sortingLayerID = spriteRenderer.sortingLayerID;
        outline.sortingOrder = spriteRenderer.sortingOrder - 1;
        outline.enabled = false;
        return outline;
    }
}
