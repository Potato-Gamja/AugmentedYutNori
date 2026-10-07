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
    private const int MaxTeamCount = 4;
    private const int MaxPieceSlotCount = 4;
    private const int AugmentSlotCount = 3;
    private int activeTeamCount = 2;
    private int pieceSlotCount = MaxPieceSlotCount;
    private Action<int, int> waitingPieceClicked;

    public void Configure(int teamCount, int piecesPerTeam)
    {
        activeTeamCount = Mathf.Clamp(teamCount, 2, MaxTeamCount);
        pieceSlotCount = Mathf.Clamp(piecesPerTeam, 1, MaxPieceSlotCount);
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

    public void SetState(int team, int waiting, int onBoard, int finished, IReadOnlyList<string> augmentIds)
    {
        Transform panel = FindPanel(team);
        if (panel == null) return;

        TMP_Text waitingLabel = panel.Find("WaitingLabel")?.GetComponent<TMP_Text>();
        if (waitingLabel != null) waitingLabel.text = $"WAITING {waiting}  /  BOARD {onBoard}";

        TMP_Text finishLabel = panel.Find("FinishLabel")?.GetComponent<TMP_Text>();
        if (finishLabel != null) finishLabel.text = $"FINISHED {finished}/{pieceSlotCount}";

        for (int i = 0; i < MaxPieceSlotCount; i++)
        {
            Transform waitingSlot = panel.Find($"PieceSlot_{i}");
            Transform piece = waitingSlot != null ? waitingSlot.Find("Piece") : null;
            if (piece != null) piece.gameObject.SetActive(i < pieceSlotCount && i < waiting);

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
            label.text = augmentIds != null && i < augmentIds.Count ? ShortAugmentName(augmentIds[i]) : "";
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
        for (int team = 0; team < MaxTeamCount; team++)
        {
            Transform panel = FindPanel(team);
            if (panel == null) continue;

            Transform border = panel.Find("ActiveBorder");
            if (border != null) border.gameObject.SetActive(team == activeTeam);

            Image header = panel.Find("Header")?.GetComponent<Image>();
            if (header != null)
                header.color = team == activeTeam ? TurnHudView.GetTeamColor(team) : InactiveHeaderColor(team);
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
        PlacePanel(FindPanel(2), false, false);
        PlacePanel(FindPanel(3), true, false);
    }

    private void ConfigureSeat(int team, bool occupied)
    {
        Transform panel = FindPanel(team);
        if (panel == null) return;
        panel.gameObject.SetActive(true); // 2·3인에서도 남은 자리를 빈 좌석으로 표시한다.

        TMP_Text headerLabel = panel.Find("Header")?.GetComponentInChildren<TMP_Text>(true);
        if (headerLabel != null)
        {
            headerLabel.text = occupied ? $"PLAYER {team + 1}" : "EMPTY";
            headerLabel.color = occupied ? TurnHudView.GetTeamColor(team) : new Color32(150, 140, 125, 255);
        }

        for (int slot = 0; slot < MaxPieceSlotCount; slot++)
        {
            Transform waitingSlot = panel.Find($"PieceSlot_{slot}");
            if (waitingSlot != null) waitingSlot.gameObject.SetActive(occupied && slot < pieceSlotCount);
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

    private static string ShortAugmentName(string id)
    {
        if (string.IsNullOrEmpty(id)) return "";
        return id switch
        {
            "BackDoSwitch" => "BACK DO",
            "QuickStart" => "QUICK",
            "WeakBoost" => "BOOST",
            "Shield" => "SHIELD",
            "SturdyCarry" => "CARRY",
            "Rethrow" => "RETHROW",
            _ => id
        };
    }
}
