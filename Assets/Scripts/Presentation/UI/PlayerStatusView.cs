using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 플레이어 패널의 말, 완주 수와 증강 슬롯 표시를 담당한다.
/// 향후 3·4팀 패널을 추가하면 PlayerPanel_3, PlayerPanel_4 이름으로 자동 탐색한다.
/// </summary>
public sealed class PlayerStatusView : MonoBehaviour
{
    public enum ConnectionState { Connected, Reconnecting, Removed }
    private const int MaxTeamCount = 4;
    private const int MaxPieceSlotCount = 4;
    private const int AugmentSlotCount = 3;
    private int activeTeamCount = 2;
    private int pieceSlotCount = MaxPieceSlotCount;
    private int currentActiveTeam;
    private Action<int, int> waitingPieceClicked;
    private IReadOnlyList<Sprite> teamPieceSprites;
    private IReadOnlyList<Sprite> teamPieceGlowSprites;
    private readonly Dictionary<int, float> reconnectDeadlines = new();

    private void Update()
    {
        if (reconnectDeadlines.Count == 0) return;
        foreach (KeyValuePair<int, float> pair in reconnectDeadlines)
        {
            TMP_Text label = GetConnectionLabel(pair.Key);
            if (label == null) continue;
            int seconds = Mathf.Max(0, Mathf.CeilToInt(pair.Value - Time.unscaledTime));
            if (seconds > 0) label.SetText("재접속 대기 {0}초", seconds);
            else label.text = "재접속 처리 중";
        }
    }

    public void Configure(
        int teamCount,
        int piecesPerTeam,
        IReadOnlyList<Sprite> pieceSprites,
        IReadOnlyList<Sprite> pieceGlowSprites)
    {
        activeTeamCount = Mathf.Clamp(teamCount, 2, MaxTeamCount);
        pieceSlotCount = Mathf.Clamp(piecesPerTeam, 1, MaxPieceSlotCount);
        teamPieceSprites = pieceSprites;
        teamPieceGlowSprites = pieceGlowSprites;
        EnsureFourPlayerPanels();

        for (int team = 0; team < MaxTeamCount; team++)
            ConfigureSeat(team, team < activeTeamCount);

        if (waitingPieceClicked != null) BindWaitingPieceClicks(waitingPieceClicked);
    }

    public void BindWaitingPieceClicks(Action<int, int> onClicked)
    {
        waitingPieceClicked = onClicked;
        for (int team = 0; team < MaxTeamCount; team++)
        {
            Transform panel = FindPanel(team);
            if (panel == null) continue;

            for (int slotIndex = 0; slotIndex < MaxPieceSlotCount; slotIndex++)
            {
                Transform slot = panel.Find($"PieceSlot_{slotIndex}");
                if (slot == null) continue;

                Image graphic = slot.GetComponent<Image>();
                if (graphic != null) graphic.raycastTarget = true;

                Button button = slot.GetComponent<Button>();
                if (button == null) button = slot.gameObject.AddComponent<Button>();
                button.targetGraphic = graphic;
                button.transition = Selectable.Transition.None;

                int capturedTeam = team;
                int capturedSlot = slotIndex;
                button.onClick.RemoveAllListeners();
                button.interactable = capturedTeam < activeTeamCount && capturedSlot < pieceSlotCount;
                button.onClick.AddListener(() => waitingPieceClicked?.Invoke(capturedTeam, capturedSlot));
            }
        }
    }

    public void SetState(
        int team,
        int waiting,
        int onBoard,
        int finished,
        IReadOnlyList<string> augmentNames,
        IReadOnlyCollection<int> waitingSlots = null)
    {
        Transform panel = FindPanel(team);
        if (panel == null) return;

        TMP_Text waitingLabel = panel.Find("WaitingLabel")?.GetComponent<TMP_Text>();
        if (waitingLabel != null) waitingLabel.gameObject.SetActive(false);

        TMP_Text finishLabel = panel.Find("FinishLabel")?.GetComponent<TMP_Text>();
        if (finishLabel != null) finishLabel.text = $"완주 {finished}/{pieceSlotCount}";

        for (int i = 0; i < MaxPieceSlotCount; i++)
        {
            Transform waitingSlot = panel.Find($"PieceSlot_{i}");
            Transform piece = waitingSlot != null ? waitingSlot.Find("Piece") : null;
            bool isWaiting = waitingSlots != null ? ContainsSlot(waitingSlots, i) : i < waiting;
            if (piece != null) piece.gameObject.SetActive(i < pieceSlotCount && isWaiting);

            Image finishImage = panel.Find($"FinishSlot_{i}")?.GetComponent<Image>();
            if (finishImage != null)
            {
                finishImage.gameObject.SetActive(i < pieceSlotCount);
                finishImage.color = i < finished
                    ? TurnHudView.GetTeamColor(team)
                    : new Color32(241, 226, 196, 255);
            }
        }

        for (int i = 0; i < AugmentSlotCount; i++)
        {
            Transform slot = panel.Find($"AugmentSlot_{i}");
            if (slot == null) continue;
            TMP_Text label = GetOrCreateAugmentLabel(slot);
            label.text = augmentNames != null && i < augmentNames.Count ? augmentNames[i] : string.Empty;
        }
    }

    public void SetSelectedWaitingPiece(int team, int slotIndex)
    {
        for (int panelTeam = 0; panelTeam < MaxTeamCount; panelTeam++)
        {
            Transform panel = FindPanel(panelTeam);
            if (panel == null) continue;

            for (int i = 0; i < MaxPieceSlotCount; i++)
            {
                Image slotImage = panel.Find($"PieceSlot_{i}")?.GetComponent<Image>();
                if (slotImage == null) continue;
                slotImage.color = panelTeam == team && i == slotIndex
                    ? new Color32(255, 231, 166, 255)
                    : new Color32(244, 231, 203, 255);
            }
        }
    }

    public void SetActiveTeam(int activeTeam)
    {
        currentActiveTeam = activeTeam;
        for (int team = 0; team < MaxTeamCount; team++)
        {
            Transform panel = FindPanel(team);
            if (panel == null) continue;

            Transform border = panel.Find("ActiveBorder");
            if (border != null) border.gameObject.SetActive(team == activeTeam);

            Image header = panel.Find("Header")?.GetComponent<Image>();
            if (header != null)
                header.color = team == activeTeam ? TurnHudView.GetTeamColor(team) : InactiveHeaderColor(team);

            TMP_Text headerLabel = panel.Find("Header")?.GetComponentInChildren<TMP_Text>(true);
            if (headerLabel != null)
                headerLabel.color = team == activeTeam ? Color.white : TurnHudView.GetTeamColor(team);
        }
    }

    public void SetWaitingPieceAvailable(int team, int slotIndex, bool available)
    {
        Transform slot = FindPanel(team)?.Find($"PieceSlot_{slotIndex}");
        if (slot == null) return;

        Transform glow = slot.Find("AvailableGlow");
        if (glow == null)
        {
            var glowObject = new GameObject("AvailableGlow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            glowObject.transform.SetParent(slot, false);
            glowObject.transform.SetAsFirstSibling();
            RectTransform rect = glowObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(104f, 104f);
            glow = glowObject.transform;
        }

        Image image = glow.GetComponent<Image>();
        image.raycastTarget = false;
        if (teamPieceGlowSprites != null && team >= 0 && team < teamPieceGlowSprites.Count)
            image.sprite = teamPieceGlowSprites[team];
        image.color = Color.white;
        image.preserveAspect = true;
        glow.gameObject.SetActive(available && image.sprite != null);
    }

    public void SetConnectionState(int team, ConnectionState state, int graceSec = 0)
    {
        TMP_Text label = GetConnectionLabel(team);
        if (label == null) return;

        reconnectDeadlines.Remove(team);
        switch (state)
        {
            case ConnectionState.Connected:
                label.gameObject.SetActive(false);
                break;
            case ConnectionState.Reconnecting:
                reconnectDeadlines[team] = Time.unscaledTime + Mathf.Max(0, graceSec);
                label.text = $"재접속 대기 {Mathf.Max(0, graceSec)}초";
                label.color = new Color32(181, 94, 18, 255);
                label.gameObject.SetActive(true);
                break;
            case ConnectionState.Removed:
                label.text = "탈주";
                label.color = new Color32(150, 140, 125, 255);
                label.gameObject.SetActive(true);
                SetTeamPiecesVisible(team, false);
                break;
        }
    }

    private TMP_Text GetConnectionLabel(int team)
    {
        Transform panel = FindPanel(team);
        if (panel == null) return null;
        TMP_Text existing = panel.Find("ConnectionStatus")?.GetComponent<TMP_Text>();
        if (existing != null) return existing;

        GameObject labelObject = new("ConnectionStatus", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(panel, false);
        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-18f, -58f);
        rect.sizeDelta = new Vector2(170f, 30f);
        TMP_Text label = labelObject.GetComponent<TMP_Text>();
        label.alignment = TextAlignmentOptions.Right;
        label.fontSize = 18f;
        label.fontStyle = FontStyles.Bold;
        label.raycastTarget = false;
        return label;
    }

    private void SetTeamPiecesVisible(int team, bool visible)
    {
        Transform panel = FindPanel(team);
        if (panel == null) return;
        for (int slot = 0; slot < MaxPieceSlotCount; slot++)
        {
            Transform waiting = panel.Find($"PieceSlot_{slot}");
            Transform finished = panel.Find($"FinishSlot_{slot}");
            if (waiting != null) waiting.gameObject.SetActive(visible && slot < pieceSlotCount);
            if (finished != null) finished.gameObject.SetActive(visible && slot < pieceSlotCount);
        }
    }

    private Transform FindPanel(int team)
    {
        return team switch
        {
            0 => transform.Find("PlayerPanel_Left"),
            1 => transform.Find("PlayerPanel_Right"),
            _ => transform.Find($"PlayerPanel_{team + 1}")
        };
    }

    private void EnsureFourPlayerPanels()
    {
        Transform left = FindPanel(0);
        Transform right = FindPanel(1);
        if (left == null || right == null) return;

        if (FindPanel(2) == null)
        {
            Transform panel = Instantiate(left.gameObject, transform).transform;
            panel.name = "PlayerPanel_3";
        }
        if (FindPanel(3) == null)
        {
            Transform panel = Instantiate(right.gameObject, transform).transform;
            panel.name = "PlayerPanel_4";
        }

        PlacePanel(FindPanel(0), false, true);
        PlacePanel(FindPanel(1), true, true);
        // 4인 시안 기준: 4번은 좌하단, 현재 차례가 강조되는 3번은 우하단에 둔다.
        PlacePanel(FindPanel(2), true, false);
        PlacePanel(FindPanel(3), false, false);
    }

    private void ConfigureSeat(int team, bool occupied)
    {
        Transform panel = FindPanel(team);
        if (panel == null) return;
        panel.gameObject.SetActive(true); // 2·3인에서도 남은 자리를 빈 좌석으로 표시한다.

        TMP_Text waitingLabel = panel.Find("WaitingLabel")?.GetComponent<TMP_Text>();
        if (waitingLabel != null) waitingLabel.gameObject.SetActive(false);

        TMP_Text headerLabel = panel.Find("Header")?.GetComponentInChildren<TMP_Text>(true);
        if (headerLabel != null)
        {
            headerLabel.text = occupied ? $"플레이어 {team + 1}  /  {TeamName(team)} 팀" : "빈 자리";
            headerLabel.color = !occupied
                ? new Color32(150, 140, 125, 255)
                : team == currentActiveTeam ? Color.white : TurnHudView.GetTeamColor(team);
        }

        for (int slot = 0; slot < MaxPieceSlotCount; slot++)
        {
            Transform waitingSlot = panel.Find($"PieceSlot_{slot}");
            if (waitingSlot != null)
            {
                waitingSlot.gameObject.SetActive(occupied && slot < pieceSlotCount);
                Image pieceImage = waitingSlot.Find("Piece")?.GetComponent<Image>();
                if (pieceImage != null && teamPieceSprites != null && team < teamPieceSprites.Count && teamPieceSprites[team] != null)
                {
                    pieceImage.sprite = teamPieceSprites[team];
                    pieceImage.color = Color.white;
                }
                SetWaitingPieceAvailable(team, slot, false);
            }
            Transform finishSlot = panel.Find($"FinishSlot_{slot}");
            if (finishSlot != null) finishSlot.gameObject.SetActive(occupied && slot < pieceSlotCount);
        }
    }

    private static void PlacePanel(Transform panel, bool right, bool top)
    {
        if (panel is not RectTransform rect) return;
        rect.anchorMin = rect.anchorMax = new Vector2(right ? 1f : 0f, top ? 1f : 0f);
        rect.pivot = new Vector2(right ? 1f : 0f, top ? 1f : 0f);
        rect.anchoredPosition = new Vector2(right ? -24f : 24f, top ? -24f : 24f);
    }

    private static TMP_Text GetOrCreateAugmentLabel(Transform slot)
    {
        TMP_Text label = slot.Find("Label")?.GetComponent<TMP_Text>();
        if (label != null) return label;

        GameObject labelObject = new("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(slot, false);
        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(4f, 4f);
        rect.offsetMax = new Vector2(-4f, -4f);

        label = labelObject.GetComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 14f;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.color = new Color32(58, 38, 24, 255);
        return label;
    }

    private static Color32 InactiveHeaderColor(int team)
    {
        return team switch
        {
            0 => new Color32(248, 221, 214, 255),
            1 => new Color32(220, 230, 248, 255),
            2 => new Color32(217, 239, 228, 255),
            _ => new Color32(250, 232, 202, 255)
        };
    }

    private static bool ContainsSlot(IReadOnlyCollection<int> slots, int target)
    {
        foreach (int slot in slots)
            if (slot == target) return true;
        return false;
    }

    private static string TeamName(int team)
    {
        return team switch
        {
            0 => "빨강",
            1 => "파랑",
            2 => "초록",
            _ => "노랑"
        };
    }
}
