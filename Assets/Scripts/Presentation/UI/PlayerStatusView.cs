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
    private const int AugmentSlotCount = 4;
    private static readonly string[] AugmentIds =
    {
        "Assassin", "Hunter", "Marathoner", "Monk", "Sprinter", "Loner",
        "Pacifist", "FriendShield", "Gambler", "Rethrow", "Contrarian", "Veteran"
    };
    private int activeTeamCount = 2;
    private readonly int[] pieceSlotCounts = { MaxPieceSlotCount, MaxPieceSlotCount, MaxPieceSlotCount, MaxPieceSlotCount };
    private int currentActiveTeam;
    private Action<int, int> waitingPieceClicked;
    private IReadOnlyList<Sprite> teamPieceSprites;
    private IReadOnlyList<Sprite> teamPieceGlowSprites;
    private readonly Dictionary<string, Sprite> augmentIconById = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AugmentDefinition> augmentDefinitionById = new(StringComparer.Ordinal);
    private Sprite augmentActiveRing;
    private IReadOnlyList<PlayerDisplayInfo> playerProfiles = Array.Empty<PlayerDisplayInfo>();
    private IReadOnlyList<Sprite> profileIcons = Array.Empty<Sprite>();
    private RectTransform augmentTooltip;
    private Image augmentTooltipIcon;
    private TMP_Text augmentTooltipName;
    private TMP_Text augmentTooltipDescription;
    private readonly Dictionary<int, float> reconnectDeadlines = new();
    private readonly Dictionary<int, ReconnectOverlayView> reconnectOverlays = new();

    private sealed class ReconnectOverlayView
    {
        public GameObject Root;
        public Image Background;
        public TMP_Text StatusText;
        public TMP_Text TimerText;
    }

    private void Update()
    {
        if (reconnectDeadlines.Count == 0) return;
        foreach (KeyValuePair<int, float> pair in reconnectDeadlines)
        {
            ReconnectOverlayView overlay = GetReconnectOverlay(pair.Key);
            if (overlay == null) continue;
            int seconds = Mathf.Max(0, Mathf.CeilToInt(pair.Value - Time.unscaledTime));
            overlay.TimerText.SetText("{0}초 남음", seconds);
        }
    }

    public void Configure(
        int teamCount,
        int piecesPerTeam,
        IReadOnlyList<Sprite> pieceSprites,
        IReadOnlyList<Sprite> pieceGlowSprites)
    {
        activeTeamCount = Mathf.Clamp(teamCount, 2, MaxTeamCount);
        int slotCount = Mathf.Clamp(piecesPerTeam, 1, MaxPieceSlotCount);
        for (int team = 0; team < MaxTeamCount; team++) pieceSlotCounts[team] = slotCount;
        teamPieceSprites = pieceSprites;
        teamPieceGlowSprites = pieceGlowSprites;
        EnsureFourPlayerPanels();

        for (int team = 0; team < MaxTeamCount; team++)
            ConfigureSeat(team, team < activeTeamCount);

        if (waitingPieceClicked != null) BindWaitingPieceClicks(waitingPieceClicked);
    }

    // 팀 하나의 말 슬롯 수를 바꾼다 (노장은 1개 적음). 말을 만든 뒤 호출
    public void SetPieceCount(int team, int count)
    {
        if (team < 0 || team >= MaxTeamCount) return;
        pieceSlotCounts[team] = Mathf.Clamp(count, 1, MaxPieceSlotCount);
        ConfigureSeat(team, team < activeTeamCount);
        if (waitingPieceClicked != null) BindWaitingPieceClicks(waitingPieceClicked);
    }

    public void SetPlayerProfiles(IReadOnlyList<PlayerDisplayInfo> profiles, IReadOnlyList<Sprite> icons)
    {
        playerProfiles = profiles ?? Array.Empty<PlayerDisplayInfo>();
        profileIcons = icons ?? Array.Empty<Sprite>();
        for (int team = 0; team < MaxTeamCount; team++) ApplyPlayerProfile(team);
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
                button.interactable = capturedTeam < activeTeamCount && capturedSlot < pieceSlotCounts[capturedTeam];
                button.onClick.AddListener(() => waitingPieceClicked?.Invoke(capturedTeam, capturedSlot));
                SoundManager.BindButtonClick(button);
            }
        }
    }

    public void ConfigureAugmentIcons(IReadOnlyList<Sprite> icons, Sprite activeRing, TextAsset definitions)
    {
        augmentIconById.Clear();
        if (icons != null)
        {
            int count = Mathf.Min(AugmentIds.Length, icons.Count);
            for (int i = 0; i < count; i++)
                if (icons[i] != null) augmentIconById[AugmentIds[i]] = icons[i];
        }
        augmentActiveRing = activeRing;

        augmentDefinitionById.Clear();
        if (definitions != null)
        {
            AugmentDefinitionFile file = JsonUtility.FromJson<AugmentDefinitionFile>(definitions.text);
            if (file?.augments != null)
                foreach (AugmentDefinition definition in file.augments)
                    if (definition != null && !string.IsNullOrEmpty(definition.id))
                        augmentDefinitionById[definition.id] = definition;
        }

        BindAugmentTooltip();
    }

    public void SetState(
        int team,
        int waiting,
        int onBoard,
        int finished,
        IReadOnlyList<string> augmentIds,
        IReadOnlyCollection<int> waitingSlots = null,
        bool rethrowUsed = false)
    {
        Transform panel = FindPanel(team);
        if (panel == null) return;
        int slotCount = pieceSlotCounts[team];

        TMP_Text waitingLabel = panel.Find("WaitingLabel")?.GetComponent<TMP_Text>();
        if (waitingLabel != null) waitingLabel.gameObject.SetActive(false);

        TMP_Text finishLabel = panel.Find("FinishLabel")?.GetComponent<TMP_Text>();
        if (finishLabel != null) finishLabel.text = $"완주 {finished}/{slotCount}";

        for (int i = 0; i < MaxPieceSlotCount; i++)
        {
            Transform waitingSlot = panel.Find($"PieceSlot_{i}");
            Transform piece = waitingSlot != null ? waitingSlot.Find("Piece") : null;
            bool isWaiting = waitingSlots != null ? ContainsSlot(waitingSlots, i) : i < waiting;
            if (piece != null) piece.gameObject.SetActive(i < slotCount && isWaiting);

            Image finishImage = panel.Find($"FinishSlot_{i}")?.GetComponent<Image>();
            if (finishImage != null)
            {
                finishImage.gameObject.SetActive(i < slotCount);
                finishImage.color = i < finished
                    ? TurnHudView.GetTeamColor(team)
                    : new Color32(241, 226, 196, 255);
            }
        }

        for (int i = 0; i < AugmentSlotCount; i++)
        {
            Transform slot = panel.Find($"AugmentSlot_{i}");
            if (slot == null) continue;
            bool hasAugment = augmentIds != null && i < augmentIds.Count;
            bool isUsed = hasAugment && augmentIds[i] == "Rethrow" && rethrowUsed;
            Image icon = slot.GetComponent<Image>();
            if (icon != null)
            {
                icon.sprite = hasAugment && augmentIconById.TryGetValue(augmentIds[i], out Sprite sprite)
                    ? sprite
                    : null;
                icon.enabled = icon.sprite != null;
                icon.preserveAspect = true;
                icon.color = isUsed ? new Color32(110, 100, 90, 255) : Color.white;
                icon.raycastTarget = hasAugment;
            }

            Transform usedBadge = slot.Find("UsedBadge");
            if (usedBadge != null) usedBadge.gameObject.SetActive(isUsed);

            AugmentTooltipTrigger trigger = slot.GetComponent<AugmentTooltipTrigger>();
            if (trigger == null) trigger = slot.gameObject.AddComponent<AugmentTooltipTrigger>();
            if (hasAugment)
            {
                string augmentId = augmentIds[i];
                trigger.enabled = true;
                trigger.Configure(
                    () => ShowAugmentTooltip(slot as RectTransform, augmentId),
                    HideAugmentTooltip);
            }
            else
            {
                trigger.Configure(null, null);
                trigger.enabled = false;
            }

            Image activeRing = slot.Find("ActiveRing")?.GetComponent<Image>();
            if (activeRing != null)
            {
                activeRing.sprite = augmentActiveRing;
                activeRing.color = TurnHudView.GetTeamColor(team);
                activeRing.preserveAspect = true;
                activeRing.gameObject.SetActive(hasAugment && team == currentActiveTeam && augmentActiveRing != null);
            }
        }
    }

    private void BindAugmentTooltip()
    {
        augmentTooltip = transform.Find("AugmentTooltip") as RectTransform;
        if (augmentTooltip == null) return;
        augmentTooltipIcon = augmentTooltip.Find("Icon")?.GetComponent<Image>();
        augmentTooltipName = augmentTooltip.Find("Name")?.GetComponent<TMP_Text>();
        augmentTooltipDescription = augmentTooltip.Find("Description")?.GetComponent<TMP_Text>();
        augmentTooltip.gameObject.SetActive(false);
    }

    private void ShowAugmentTooltip(RectTransform slot, string augmentId)
    {
        if (slot == null || augmentTooltip == null || !augmentDefinitionById.TryGetValue(augmentId, out AugmentDefinition definition))
            return;

        if (augmentTooltipIcon != null)
        {
            augmentTooltipIcon.sprite = augmentIconById.TryGetValue(augmentId, out Sprite sprite) ? sprite : null;
            augmentTooltipIcon.enabled = augmentTooltipIcon.sprite != null;
            augmentTooltipIcon.preserveAspect = true;
        }
        if (augmentTooltipName != null) augmentTooltipName.text = definition.name;
        if (augmentTooltipDescription != null) augmentTooltipDescription.text = definition.description;
        ResizeAugmentTooltip();

        RectTransform panel = slot.parent as RectTransform;
        if (panel == null) return;

        augmentTooltip.SetParent(panel, false);
        augmentTooltip.anchorMin = new Vector2(0f, 1f);
        augmentTooltip.anchorMax = new Vector2(0f, 1f);
        augmentTooltip.pivot = new Vector2(0f, 0.5f);
        augmentTooltip.anchoredPosition = new Vector2(
            slot.anchoredPosition.x + slot.rect.width + 10f,
            slot.anchoredPosition.y - slot.rect.height * 0.5f);
        augmentTooltip.SetAsLastSibling();
        augmentTooltip.gameObject.SetActive(true);
    }

    private void ResizeAugmentTooltip()
    {
        if (augmentTooltip == null || augmentTooltipDescription == null) return;
        const float padding = 12f;
        const float gap = 6f;
        float nameHeight = augmentTooltipName != null
            ? Mathf.Max(30f, augmentTooltipName.GetPreferredValues(augmentTooltipName.text,
                augmentTooltipName.rectTransform.rect.width, Mathf.Infinity).y)
            : 0f;
        augmentTooltipDescription.textWrappingMode = TextWrappingModes.Normal;
        float descriptionHeight = Mathf.Max(32f, Mathf.Ceil(augmentTooltipDescription.GetPreferredValues(
            augmentTooltipDescription.text, augmentTooltipDescription.rectTransform.rect.width, Mathf.Infinity).y));
        augmentTooltip.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
            Mathf.Max(100f, padding * 2f + nameHeight + gap + descriptionHeight));
        if (augmentTooltipName != null)
            LayoutTooltipText(augmentTooltipName.rectTransform, padding, nameHeight);
        LayoutTooltipText(augmentTooltipDescription.rectTransform, padding + nameHeight + gap, descriptionHeight);
    }

    private static void LayoutTooltipText(RectTransform rect, float top, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -top);
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
    }

    private void HideAugmentTooltip()
    {
        if (augmentTooltip != null) augmentTooltip.gameObject.SetActive(false);
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
            if (border != null)
            {
                Image borderImage = border.GetComponent<Image>();
                if (borderImage != null) borderImage.color = TurnHudView.GetTeamColor(team);
                border.gameObject.SetActive(team == activeTeam);
            }

            Image header = panel.Find("Header")?.GetComponent<Image>();
            if (header != null)
                header.color = team == activeTeam ? TurnHudView.GetTeamColor(team) : InactiveHeaderColor(team);

            for (int i = 0; i < AugmentSlotCount; i++)
            {
                Transform slot = panel.Find($"AugmentSlot_{i}");
                Image icon = slot?.GetComponent<Image>();
                Transform ring = slot?.Find("ActiveRing");
                if (ring != null)
                    ring.gameObject.SetActive(team == activeTeam && icon != null && icon.enabled && augmentActiveRing != null);
            }

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
        ReconnectOverlayView overlay = GetReconnectOverlay(team);
        if (overlay == null) return;

        reconnectDeadlines.Remove(team);
        switch (state)
        {
            case ConnectionState.Connected:
                overlay.Root.SetActive(false);
                break;
            case ConnectionState.Reconnecting:
                if (graceSec > 0) reconnectDeadlines[team] = Time.unscaledTime + graceSec;
                overlay.Background.color = new Color32(255, 249, 236, 225);
                overlay.StatusText.text = graceSec > 0 ? "재접속 중..." : "재연결 대기 중...";
                overlay.StatusText.color = new Color32(92, 62, 42, 255);
                overlay.TimerText.SetText("{0}초 남음", Mathf.Max(0, graceSec));
                overlay.TimerText.gameObject.SetActive(graceSec > 0);
                overlay.Root.SetActive(true);
                overlay.Root.transform.SetAsLastSibling();
                break;
            case ConnectionState.Removed:
                overlay.Background.color = new Color32(232, 226, 216, 235);
                overlay.StatusText.text = "탈주";
                overlay.StatusText.color = new Color32(120, 110, 96, 255);
                overlay.TimerText.gameObject.SetActive(false);
                overlay.Root.SetActive(true);
                overlay.Root.transform.SetAsLastSibling();
                break;
        }
    }

    private ReconnectOverlayView GetReconnectOverlay(int team)
    {
        if (reconnectOverlays.TryGetValue(team, out ReconnectOverlayView cached) && cached.Root != null)
            return cached;

        Transform panel = FindPanel(team);
        if (panel == null) return null;
        Transform oldStatus = panel.Find("ConnectionStatus");
        if (oldStatus != null) oldStatus.gameObject.SetActive(false);

        Transform existing = panel.Find("ReconnectOverlay");
        GameObject root = existing != null
            ? existing.gameObject
            : new GameObject("ReconnectOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        if (existing == null) root.transform.SetParent(panel, false);

        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        Image background = root.GetComponent<Image>();
        Image panelBackground = panel.GetComponent<Image>();
        if (panelBackground != null)
        {
            background.sprite = panelBackground.sprite;
            background.type = panelBackground.type;
            background.pixelsPerUnitMultiplier = panelBackground.pixelsPerUnitMultiplier;
        }
        background.raycastTarget = false;

        TMP_Text statusText = GetOrCreateOverlayText(root.transform, "ReconnectText");
        RectTransform statusRect = statusText.rectTransform;
        statusRect.anchorMin = statusRect.anchorMax = new Vector2(0.5f, 0.5f);
        statusRect.pivot = new Vector2(0.5f, 0.5f);
        statusRect.anchoredPosition = new Vector2(0f, 14f);
        statusRect.sizeDelta = new Vector2(190f, 34f);
        statusText.fontSize = 22f;

        TMP_Text timerText = GetOrCreateOverlayText(root.transform, "ReconnectTimerText");
        RectTransform timerRect = timerText.rectTransform;
        timerRect.anchorMin = timerRect.anchorMax = new Vector2(0.5f, 0.5f);
        timerRect.pivot = new Vector2(0.5f, 0.5f);
        timerRect.anchoredPosition = new Vector2(0f, -18f);
        timerRect.sizeDelta = new Vector2(190f, 30f);
        timerText.fontSize = 18f;
        timerText.color = new Color32(181, 94, 18, 255);

        if (existing == null) root.SetActive(false);
        var overlay = new ReconnectOverlayView
        {
            Root = root,
            Background = background,
            StatusText = statusText,
            TimerText = timerText
        };
        reconnectOverlays[team] = overlay;
        return overlay;
    }

    private static TMP_Text GetOrCreateOverlayText(Transform parent, string name)
    {
        TMP_Text text = parent.Find(name)?.GetComponent<TMP_Text>();
        if (text != null) return text;

        GameObject textObject = new(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        text = textObject.GetComponent<TMP_Text>();
        text.alignment = TextAlignmentOptions.Center;
        text.fontStyle = FontStyles.Normal;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
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
            headerLabel.text = occupied ? DisplayName(team) : "빈 자리";
            headerLabel.color = !occupied
                ? new Color32(150, 140, 125, 255)
                : team == currentActiveTeam ? Color.white : TurnHudView.GetTeamColor(team);
        }

        for (int slot = 0; slot < MaxPieceSlotCount; slot++)
        {
            Transform waitingSlot = panel.Find($"PieceSlot_{slot}");
            if (waitingSlot != null)
            {
                waitingSlot.gameObject.SetActive(occupied && slot < pieceSlotCounts[team]);
                Image pieceImage = waitingSlot.Find("Piece")?.GetComponent<Image>();
                if (pieceImage != null && teamPieceSprites != null && team < teamPieceSprites.Count && teamPieceSprites[team] != null)
                {
                    pieceImage.sprite = teamPieceSprites[team];
                    pieceImage.color = Color.white;
                }
                SetWaitingPieceAvailable(team, slot, false);
            }
            Transform finishSlot = panel.Find($"FinishSlot_{slot}");
            if (finishSlot != null) finishSlot.gameObject.SetActive(occupied && slot < pieceSlotCounts[team]);
        }
        ApplyPlayerProfile(team);
    }

    private void ApplyPlayerProfile(int team)
    {
        Transform panel = FindPanel(team);
        if (panel == null) return;

        PlayerDisplayInfo profile = FindProfile(team);
        TMP_Text headerLabel = panel.Find("Header")?.GetComponentInChildren<TMP_Text>(true);
        if (headerLabel != null && team < activeTeamCount)
            headerLabel.text = DisplayName(team);

        Image icon = (panel.Find("Header/ProfileIcon") ?? panel.Find("ProfileIcon"))?.GetComponent<Image>();
        Transform frame = panel.Find("Header/ProfileFrame") ?? panel.Find("ProfileFrame");
        if (icon == null)
        {
            if (frame != null) frame.gameObject.SetActive(false);
            return;
        }
        icon.sprite = PlayerProfileIconResolver.Resolve(profileIcons, profile?.iconId, team);
        icon.enabled = team < activeTeamCount && icon.sprite != null;
        icon.preserveAspect = true;
        if (frame != null)
        {
            bool visible = icon.enabled && icon.gameObject.activeSelf;
            Image frameImage = frame.GetComponent<Image>();
            if (frameImage != null) frameImage.enabled = visible;
            frame.gameObject.SetActive(visible);
        }
    }

    private PlayerDisplayInfo FindProfile(int team)
    {
        foreach (PlayerDisplayInfo profile in playerProfiles)
            if (profile != null && profile.team == team) return profile;
        return null;
    }

    private string DisplayName(int team)
    {
        PlayerDisplayInfo profile = FindProfile(team);
        return !string.IsNullOrWhiteSpace(profile?.nickname) ? profile.nickname : $"{TeamName(team)} 팀";
    }

    private static void PlacePanel(Transform panel, bool right, bool top)
    {
        if (panel is not RectTransform rect) return;
        rect.anchorMin = rect.anchorMax = new Vector2(right ? 1f : 0f, top ? 1f : 0f);
        rect.pivot = new Vector2(right ? 1f : 0f, top ? 1f : 0f);
        rect.anchoredPosition = new Vector2(right ? -24f : 24f, top ? -24f : 24f);
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
