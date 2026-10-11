using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 말 표시. 판에 안 나온 동안은 처음 놓인 자리(대기석)에 있고, 이동은 받은 좌표를 따라 한 칸씩 움직인다.
public class PieceView : MonoBehaviour
{
    [SerializeField] private int team;
    [Tooltip("팀 안에서 말의 고정 순서. 서버 GameSetup의 slotIndex와 같은 값을 사용한다.")]
    [SerializeField] private int slotIndex = -1;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private float stepDuration = 0.15f;
    [SerializeField] private Color selectedColor = new Color(1f, 0.85f, 0.3f);
    [SerializeField] private Color turnHighlightColor = new Color(0.35f, 1f, 1f);
    [SerializeField] private float highlightOutlineScale = 1.3f;

    [Header("P0 UI Art (선택 사항)")]
    [SerializeField] private Sprite bodySprite;
    [SerializeField] private Sprite availableGlowSprite;
    [SerializeField] private Sprite selectedRingSprite;
    [SerializeField] private Sprite selectedFloorSprite;
    [SerializeField] private Sprite selectedFloorShadowSprite;
    [SerializeField] private Sprite stackBadgeSprite;
    [SerializeField] private Color stackTextColor = Color.white;
    [SerializeField] private float stackTextSize = 0.08f;

    private Vector3 homePosition;
    private Color normalColor;
    private TextMesh stackText;
    private SpriteRenderer stackBadge;
    private SpriteRenderer highlightOutline;
    private SpriteRenderer availableGlow;
    private SpriteRenderer selectedRing;
    private SpriteRenderer selectedFloor;
    private SpriteRenderer selectedFloorShadow;
    private Vector3 baseScale;
    private bool isSelected;
    
    private bool worldVisible = true;
private bool isTurnHighlighted;

    public int Team => team;
    public int SlotIndex => slotIndex;
    public Piece Piece { get; private set; }
    public bool IsMoving { get; private set; }

    /// <summary>
    /// GameSetup과 서버가 공통으로 사용하는 고정 말 ID를 반환한다.
    /// </summary>
    public bool TryGetConfiguredPieceId(out int pieceId)
    {
        if (team < 0 || slotIndex < 0)
        {
            pieceId = -1;
            return false;
        }

        pieceId = GameSetup.PieceId(team, slotIndex);
        return true;
    }

private void Awake()
    {
        baseScale = transform.localScale;
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (bodySprite != null) spriteRenderer.sprite = bodySprite;
        homePosition = transform.position;
        normalColor = spriteRenderer.color;
        if (availableGlowSprite != null)
        {
            selectedFloor = FindOrCreateEffectRenderer("SelectedFloor", selectedFloorSprite, -3);
            selectedFloorShadow = FindOrCreateEffectRenderer("SelectedFloorShadow", selectedFloorShadowSprite, -2);
            availableGlow = FindOrCreateEffectRenderer("AvailableGlow", availableGlowSprite, -1);
            selectedRing = FindOrCreateEffectRenderer("SelectedRing", selectedRingSprite, 1);
        }
        else highlightOutline = FindOrCreateHighlightOutline();
        stackText = transform.Find("StackCount")?.GetComponent<TextMesh>() ?? CreateStackText();
        stackBadge = FindOrCreateEffectRenderer("StackBadge", stackBadgeSprite, 2);
        if (stackBadge != null)
        {
            stackBadge.transform.SetParent(transform, false);
            stackBadge.transform.localPosition = new Vector3(0.28f, 0.28f, 0f);
            stackBadge.transform.localScale = Vector3.one * 0.9f;
        }
        stackText.transform.localPosition = new Vector3(0.28f, 0.28f, 0f);
        stackText.color = GetStackTextColor(team);
        SetStackCount(1);
        RefreshHighlight();
    }

    public void Bind(Piece piece)
    {
        Piece = piece;
        transform.position = homePosition;
        gameObject.SetActive(true);
        // 대기 말은 PlayerPanel 슬롯으로 표시한다. 월드 말은 판에 진입할 때 켠다.
        SetWorldVisible(!piece.IsWaiting);
    }

    /// <summary>
    /// GAME_START에서 3·4팀을 받은 경우 씬에 없는 PieceView 복제본의 고정 ID와 표시색을 지정한다.
    /// 서버의 pieceId 규칙은 GameSetup.PieceId(team, slot)과 같아야 한다.
    /// </summary>
    public void ConfigureRuntimeIdentity(int configuredTeam, int configuredSlot, Color teamColor)
    {
        team = configuredTeam;
        slotIndex = configuredSlot;
        name = $"Piece_Team{configuredTeam + 1}_{configuredSlot}";
        normalColor = teamColor;
        if (spriteRenderer != null) spriteRenderer.color = teamColor;
        if (stackText != null) stackText.color = GetStackTextColor(team);
        RefreshHighlight();
    }

    // 잡혔을 때 대기석으로 돌아감
public void ReturnHome()
    {
        transform.position = homePosition;
        SetStackCount(1);
        SetWorldVisible(false);
    }

public void SetSelected(bool selected)
    {
        isSelected = selected;
        transform.localScale = selected
            ? new Vector3(baseScale.x * 1.15f, baseScale.y * 1.15f, baseScale.z)
            : baseScale;
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
        spriteRenderer.enabled = worldVisible;
        spriteRenderer.color = normalColor;
        if (availableGlow != null)
        {
            availableGlow.enabled = worldVisible && isTurnHighlighted && !isSelected;
            if (selectedRing != null) selectedRing.enabled = worldVisible && isSelected;
            if (selectedFloor != null) selectedFloor.enabled = worldVisible && isSelected;
            if (selectedFloorShadow != null) selectedFloorShadow.enabled = worldVisible && isSelected;
            return;
        }
        if (highlightOutline == null) return;
        highlightOutline.color = isSelected ? selectedColor : turnHighlightColor;
        highlightOutline.enabled = worldVisible && (isSelected || isTurnHighlighted);
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
        bool stacked = count >= 2;
        stackText.text = stacked ? count.ToString() : "";
        if (stackBadge != null) stackBadge.enabled = worldVisible && stacked;
    }

    // 업힌 말끼리 같은 자리에 겹치므로, 맨 위에 그려질 말을 정한다
public void SetSortingOrder(int order)
    {
        spriteRenderer.sortingOrder = order;
        if (selectedFloor != null) selectedFloor.sortingOrder = order - 3;
        if (selectedFloorShadow != null) selectedFloorShadow.sortingOrder = order - 2;
        if (availableGlow != null) availableGlow.sortingOrder = order - 1;
        if (selectedRing != null) selectedRing.sortingOrder = order + 1;
        if (highlightOutline != null) highlightOutline.sortingOrder = order - 1;
        if (stackBadge != null) stackBadge.sortingOrder = order + 2;
        stackText.GetComponent<MeshRenderer>().sortingOrder = order + 3;
    }

public bool Contains(Vector3 worldPosition)
    {
        if (!gameObject.activeInHierarchy || !worldVisible) return false;

        Vector3 center = spriteRenderer.bounds.center;
        center.z = worldPosition.z;
        float radius = Mathf.Max(spriteRenderer.bounds.extents.x, spriteRenderer.bounds.extents.y);
        return Vector3.Distance(center, worldPosition) <= radius;
    }

    // 같은 칸에서 여러 팀 말의 클릭 영역이 겹칠 때 실제 보이는 말과 가장 가까운 대상을 고르는 데 사용한다.
    public float DistanceTo(Vector3 worldPosition)
    {
        Vector3 center = spriteRenderer.bounds.center;
        return Vector2.Distance(center, worldPosition);
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
        text.fontStyle = FontStyle.Normal;
        text.color = stackTextColor;

        var textRenderer = go.GetComponent<MeshRenderer>();
        textRenderer.sharedMaterial = text.font.material;
        textRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
        textRenderer.sortingOrder = spriteRenderer.sortingOrder + 1;
        return text;
    }

    private static Color GetStackTextColor(int teamIndex)
    {
        return teamIndex switch
        {
            0 => new Color32(170, 45, 45, 255),
            1 => new Color32(35, 88, 165, 255),
            2 => new Color32(32, 127, 83, 255),
            _ => new Color32(117, 75, 18, 255)
        };
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

    private SpriteRenderer FindOrCreateHighlightOutline()
    {
        SpriteRenderer existing = spriteRenderer.transform.Find("TurnHighlightOutline")?.GetComponent<SpriteRenderer>();
        return existing != null ? existing : CreateHighlightOutline();
    }


// P0 아트는 같은 캔버스와 Pivot을 사용하므로 로컬 좌표 0에서 정확히 겹친다.
    private SpriteRenderer CreateEffectRenderer(string objectName, Sprite sprite, int orderOffset)
    {
        if (sprite == null) return null;
        var go = new GameObject(objectName);
        go.transform.SetParent(spriteRenderer.transform, false);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingLayerID = spriteRenderer.sortingLayerID;
        renderer.sortingOrder = spriteRenderer.sortingOrder + orderOffset;
        renderer.enabled = false;
        return renderer;
    }

    private SpriteRenderer FindOrCreateEffectRenderer(string objectName, Sprite sprite, int orderOffset)
    {
        SpriteRenderer existing = spriteRenderer.transform.Find(objectName)?.GetComponent<SpriteRenderer>();
        return existing != null ? existing : CreateEffectRenderer(objectName, sprite, orderOffset);
    }


/// <summary>
    /// 대기석은 PlayerPanel UI가 대신 표시하므로 월드 스프라이트의 중복 노출을 제어한다.
    /// GameObject는 활성 상태로 유지해 이동 코루틴과 모델 바인딩이 끊기지 않게 한다.
    /// </summary>
    public void SetWorldVisible(bool visible)
    {
        worldVisible = visible;
        if (stackText != null) stackText.gameObject.SetActive(visible);
        if (stackBadge != null) stackBadge.enabled = visible && !string.IsNullOrEmpty(stackText.text);
        RefreshHighlight();
    }
}
