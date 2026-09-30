using System;
using TMPro;
using UnityEngine;


using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;

/// <summary>
/// LSU_Game의 상시 HUD.
/// UIManager의 Panel 레이어에서 생성되며, 게임 규칙은 처리하지 않고
/// 버튼 입력을 GameFlowController에 전달하고 화면 문구만 갱신한다.
/// </summary>
public class GameHudPanel : UIBase
{
    [SerializeField] private TMP_Text turnText;
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private TMP_Text instructionText;
    [SerializeField] private Button throwButton;
    

    [Header("P0 HUD")]
    [SerializeField] private RectTransform storedResultRoot;
    [SerializeField] private GameObject storedResultChipTemplate;
    [SerializeField] private Sprite chipNormal;
    [SerializeField] private Sprite chipBonus;
    
    [SerializeField] private Sprite chipConsumeRed;
    
    [SerializeField] private Sprite resultRevealBackground;
    [SerializeField] private Sprite resultRevealGlow;
[SerializeField] private Sprite chipConsumeBlue;
[SerializeField] private Sprite chipBack;
    [SerializeField] private Image timerFill;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private float visualTurnSeconds = 30f;

    
    
    private GameObject resultRevealRoot;
    private TMP_Text resultRevealTitle;
    private TMP_Text resultRevealDetail;
    private CanvasGroup resultRevealCanvasGroup;
    private Coroutine resultRevealRoutine;
private GameObject moveOptionPopup;
private readonly List<GameObject> resultChips = new();
    
    private bool timerRunning;
private float timerRemaining;
    private string lastTurn;
[SerializeField] private Button rethrowButton;

    public event Action ThrowClicked;
    
    public event Action<int, int> WaitingPieceClicked;
public event Action RethrowClicked;

    // UIManager가 프리팹을 처음 생성했을 때 한 번 호출한다.
public override void Init()
    {
        if (throwButton != null) throwButton.onClick.AddListener(() => ThrowClicked?.Invoke());
        if (rethrowButton != null) rethrowButton.onClick.AddListener(() => RethrowClicked?.Invoke());

        // 화면에 보이는 대기 말 슬롯이 실제 Piece 선택 입력이 되도록 연결한다.
        // 팀과 슬롯 번호만 Controller에 전달하며 말 상태는 이 UI에서 변경하지 않는다.
        for (int team = 0; team < 2; team++)
        {
            Transform panel = transform.Find(team == 0 ? "PlayerPanel_Left" : "PlayerPanel_Right");
            if (panel == null) continue;
            for (int slotIndex = 0; slotIndex < 4; slotIndex++)
            {
                Transform slot = panel.Find($"PieceSlot_{slotIndex}");
                if (slot == null) continue;
                Image slotGraphic = slot.GetComponent<Image>();
                if (slotGraphic != null) slotGraphic.raycastTarget = true;

                Button button = slot.GetComponent<Button>();
                if (button == null) button = slot.gameObject.AddComponent<Button>();
                button.targetGraphic = slotGraphic;
                button.transition = Selectable.Transition.None;
                int capturedTeam = team;
                int capturedSlot = slotIndex;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => WaitingPieceClicked?.Invoke(capturedTeam, capturedSlot));
            }
        }

        timerRemaining = visualTurnSeconds;
        RefreshTimer();
    }

private void Update()
    {
        // Panel GameObject가 활성 상태인 동안 Controller가 허용한 경우에만 카운트다운한다.
        // UIManager 구현에 따라 IsOpen 플래그가 설정되지 않는 씬 프리팹도 있으므로 active 상태를 기준으로 삼는다.
        if (!gameObject.activeInHierarchy || !timerRunning || timerText == null || timerRemaining <= 0f) return;
        timerRemaining = Mathf.Max(0f, timerRemaining - Time.deltaTime);
        RefreshTimer();
    }


    // Controller가 상태를 바꿀 때 호출하는 최소 표시 API.
public void SetTurn(string value)
    {
        if (turnText != null) turnText.text = value;
        if (lastTurn != value)
        {
            lastTurn = value;
            timerRemaining = visualTurnSeconds;
            RefreshTimer();
        }
        bool teamOne = value != null && value.Contains("1");
        SetPlayerPanelState("PlayerPanel_Left", teamOne);
        SetPlayerPanelState("PlayerPanel_Right", !teamOne);
    }
public void SetResults(string value)
    {
        if (resultText != null) resultText.text = value;
        RefreshResultChips(value);
    }
    public void SetInstruction(string value) { if (instructionText != null) instructionText.text = value; }
    public void SetThrowInteractable(bool value) { if (throwButton != null) throwButton.interactable = value; }
    public void SetRethrowVisible(bool value)
    {
        if (rethrowButton != null) rethrowButton.gameObject.SetActive(value);
    }

/// <summary>
    /// Controller의 상태에 맞춰 시각 타이머만 시작/정지한다.
    /// 시간이 0이 되어도 자동으로 윷을 던지거나 말을 이동시키지는 않는다.
    /// </summary>
    public void SetTimerRunning(bool value)
    {
        timerRunning = value;
    }

    /// <summary>
    /// Core BoardState에서 계산한 팀별 말/증강 현황을 플레이어 패널에 표시한다.
    /// 이 메서드는 표시만 담당하며 Piece 상태나 증강 효과를 변경하지 않는다.
    /// </summary>
    public void SetPlayerState(int team, int waiting, int onBoard, int finished, IReadOnlyList<string> augmentIds)
    {
        string panelName = team == 0 ? "PlayerPanel_Left" : "PlayerPanel_Right";
        Transform panel = transform.Find(panelName);
        if (panel == null) return;

        TMP_Text waitingLabel = panel.Find("WaitingLabel")?.GetComponent<TMP_Text>();
        if (waitingLabel != null) waitingLabel.text = $"WAITING {waiting}  /  BOARD {onBoard}";

        TMP_Text finishLabel = panel.Find("FinishLabel")?.GetComponent<TMP_Text>();
        if (finishLabel != null) finishLabel.text = $"FINISHED {finished}/4";

        for (int i = 0; i < 4; i++)
        {
            Transform waitingSlot = panel.Find($"PieceSlot_{i}");
            Transform piece = waitingSlot != null ? waitingSlot.Find("Piece") : null;
            if (piece != null) piece.gameObject.SetActive(i < waiting);

            Transform finishSlot = panel.Find($"FinishSlot_{i}");
            Image finishImage = finishSlot != null ? finishSlot.GetComponent<Image>() : null;
            if (finishImage != null)
            {
                bool filled = i < finished;
                finishImage.color = filled
                    ? (team == 0 ? new Color32(214, 64, 60, 255) : new Color32(46, 107, 209, 255))
                    : new Color32(241, 226, 196, 255);
            }
        }

        for (int i = 0; i < 3; i++)
        {
            Transform slot = panel.Find($"AugmentSlot_{i}");
            if (slot == null) continue;
            TMP_Text label = slot.Find("Label")?.GetComponent<TMP_Text>();
            if (label == null)
            {
                GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                labelObject.transform.SetParent(slot, false);
                RectTransform rect = labelObject.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(4f, 4f);
                rect.offsetMax = new Vector2(-4f, -4f);
                label = labelObject.GetComponent<TextMeshProUGUI>();
                label.alignment = TextAlignmentOptions.Center;
                label.fontSize = 14f;
                label.enableWordWrapping = false;
                label.overflowMode = TextOverflowModes.Ellipsis;
                label.color = new Color32(58, 38, 24, 255);
            }
            label.text = augmentIds != null && i < augmentIds.Count ? ShortAugmentName(augmentIds[i]) : "";
        }
    }

/// <summary>
    /// 선택한 대기 말을 PlayerPanel 슬롯에서 표시한다.
    /// 판 위의 말 선택 효과는 PieceView가 계속 담당한다.
    /// </summary>
    public void SetSelectedWaitingPiece(int team, int slotIndex)
    {
        for (int panelTeam = 0; panelTeam < 2; panelTeam++)
        {
            Transform panel = transform.Find(panelTeam == 0 ? "PlayerPanel_Left" : "PlayerPanel_Right");
            if (panel == null) continue;
            for (int i = 0; i < 4; i++)
            {
                Image slotImage = panel.Find($"PieceSlot_{i}")?.GetComponent<Image>();
                if (slotImage == null) continue;
                bool selected = panelTeam == team && i == slotIndex;
                slotImage.color = selected
                    ? new Color32(255, 231, 166, 255)
                    : new Color32(244, 231, 203, 255);
            }
        }
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



private void RefreshTimer()
    {
        if (timerText != null) timerText.text = Mathf.CeilToInt(timerRemaining).ToString();
        if (timerFill != null)
        {
            timerFill.fillAmount = visualTurnSeconds <= 0f ? 0f : timerRemaining / visualTurnSeconds;
            timerFill.color = timerRemaining <= 5f ? new Color32(214, 64, 60, 255) :
                timerRemaining <= 10f ? new Color32(232, 133, 12, 255) : new Color32(214, 64, 60, 255);
        }
    }

    private void SetPlayerPanelState(string panelName, bool active)
    {
        Transform panel = transform.Find(panelName);
        if (panel == null) return;
        Transform border = panel.Find("ActiveBorder");
        if (border != null) border.gameObject.SetActive(active);
        Image header = panel.Find("Header")?.GetComponent<Image>();
        if (header != null)
        {
            bool left = panelName.EndsWith("Left");
            header.color = active ? (left ? new Color32(214,64,60,255) : new Color32(46,107,209,255)) :
                (left ? new Color32(248,221,214,255) : new Color32(220,230,248,255));
        }
    }

    // Controller의 기존 문자열을 칩으로 변환한다. 게임 규칙과 저장 형식은 바꾸지 않는다.
private void RefreshResultChips(string value)
    {
        foreach (GameObject chip in resultChips) if (chip != null) Destroy(chip);
        resultChips.Clear();
        if (storedResultRoot == null || storedResultChipTemplate == null || string.IsNullOrWhiteSpace(value)) return;
        int colon = value.IndexOf(':');
        string payload = colon >= 0 ? value.Substring(colon + 1).Trim() : value.Trim();
        if (payload == "-" || payload.EndsWith("-")) return;
        string[] entries = payload.Split(new[] { "  " }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string raw in entries)
        {
            string entry = raw.Trim();
            GameObject chip = Instantiate(storedResultChipTemplate, storedResultRoot);
            chip.name = "StoredResultChip";
            chip.SetActive(true);
            Image image = chip.GetComponent<Image>();
            if (image != null) image.sprite = entry.Contains("빽도") ? chipBack : entry.Contains("(") ? chipBonus : chipNormal;
            TMP_Text label = chip.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                string display = entry.Replace("빽도", "BACK DO").Replace("도", "DO").Replace("개", "GAE")
                    .Replace("걸", "GEOL").Replace("윷", "YUT").Replace("모", "MO");
                int bonus = display.IndexOf('(');
                label.text = bonus >= 0 ? display.Substring(0, bonus) + "<size=79%><color=#B36B00>" + display.Substring(bonus) + "</color></size>" : display;
            }
            resultChips.Add(chip);
        }
    }


/// <summary>
    /// 이동 입력이 성공 판정되기 전, 사용될 저장 결과를 소비 예정 모양으로 표시한다.
    /// 실제 결과 제거는 GameRules가 성공한 이동에 대해서만 처리한다.
    /// </summary>
    public void MarkResultPending(int resultIndex, int team)
    {
        if (resultIndex < 0 || resultIndex >= resultChips.Count) return;

        GameObject chip = resultChips[resultIndex];
        if (chip == null) return;

        Image image = chip.GetComponent<Image>();
        if (image != null)
        {
            Sprite pendingSprite = team == 0 ? chipConsumeRed : chipConsumeBlue;
            if (pendingSprite != null) image.sprite = pendingSprite;
        }

        TMP_Text label = chip.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.color = Color.white;
            label.text = label.text.Replace("#B36B00", "#FFE7A6");
        }
    }


/// <summary>
    /// 같은 목적지에 둘 이상의 저장 결과가 도달할 때 사용할 결과를 고르는 작은 선택창을 연다.
    /// 선택 결과의 실제 소비와 이동 판정은 Controller/GameRules가 담당한다.
    /// </summary>
    public void ShowMoveOptionChoices(
        IReadOnlyList<string> optionLabels,
        int team,
        Action<int> onSelected)
    {
        HideMoveOptionChoices();
        if (optionLabels == null || optionLabels.Count == 0 || storedResultChipTemplate == null) return;

        moveOptionPopup = new GameObject(
            "MoveOptionPopup",
            typeof(RectTransform),
            typeof(Image),
            typeof(Outline),
            typeof(HorizontalLayoutGroup));

        RectTransform popupRect = moveOptionPopup.GetComponent<RectTransform>();
        popupRect.SetParent(transform, false);
        popupRect.anchorMin = new Vector2(0.5f, 0.5f);
        popupRect.anchorMax = new Vector2(0.5f, 0.5f);
        popupRect.pivot = new Vector2(0.5f, 0.5f);
        popupRect.anchoredPosition = new Vector2(0f, -285f);
        popupRect.sizeDelta = new Vector2(Mathf.Max(210f, optionLabels.Count * 148f + 24f), 84f);
        popupRect.SetAsLastSibling();

        Image background = moveOptionPopup.GetComponent<Image>();
        background.color = new Color32(255, 249, 236, 250);
        // 배경은 버튼 뒤의 장식일 뿐이므로 포인터 입력을 가로채지 않는다.
        background.raycastTarget = false;

        Outline outline = moveOptionPopup.GetComponent<Outline>();
        outline.effectColor = team == 0
            ? new Color32(214, 64, 60, 255)
            : new Color32(46, 107, 209, 255);
        outline.effectDistance = new Vector2(3f, -3f);

        HorizontalLayoutGroup layout = moveOptionPopup.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 14, 14);
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        for (int i = 0; i < optionLabels.Count; i++)
        {
            int selectedIndex = i;
            string optionLabel = optionLabels[i];

            GameObject chip = Instantiate(storedResultChipTemplate, moveOptionPopup.transform);
            chip.name = $"MoveOption_{i}";
            chip.SetActive(true);

            RectTransform chipRect = chip.GetComponent<RectTransform>();
            chipRect.sizeDelta = new Vector2(134f, 52f);

            Image image = chip.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = optionLabel.Contains("빽도")
                    ? chipBack
                    : optionLabel.Contains("(") ? chipBonus : chipNormal;
                // 저장 결과 칩 프리팹은 평소 표시용이라 Raycast Target이 꺼져 있다.
                // 결과 선택창에서는 이 이미지를 실제 Button의 클릭 대상으로 사용한다.
                image.raycastTarget = true;
            }

            TMP_Text label = chip.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = optionLabel;

            Button button = chip.GetComponent<Button>();
            if (button == null) button = chip.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                HideMoveOptionChoices();
                onSelected?.Invoke(selectedIndex);
            });
        }
    }

    /// <summary>목적지 또는 말을 다시 선택하면 이전 결과 선택창을 정리한다.</summary>
    public void HideMoveOptionChoices()
    {
        if (moveOptionPopup == null) return;
        Destroy(moveOptionPopup);
        moveOptionPopup = null;
    }


/// <summary>
    /// 윷을 던진 직후 결과를 화면 중앙에 잠깐 크게 보여준다.
    /// 저장 결과와 추가 턴 계산은 이미 GameRules가 처리했으며, 이 메서드는 연출만 담당한다.
    /// </summary>
    public void ShowThrowResult(YutResult result, int remainingThrows, int team)
    {
        EnsureResultReveal();

        int steps = result.Steps();
        resultRevealTitle.text = result.DisplayName();
        resultRevealTitle.color = team == 0
            ? new Color32(214, 64, 60, 255)
            : new Color32(46, 107, 209, 255);

        string moveText = steps < 0
            ? $"뒤로 {Mathf.Abs(steps)}칸"
            : $"앞으로 {steps}칸";
        resultRevealDetail.text = result.GrantsExtraThrow() && remainingThrows > 0
            ? $"{moveText} · 추가 던지기 1회"
            : moveText;

        if (resultRevealRoutine != null) StopCoroutine(resultRevealRoutine);
        resultRevealRoot.SetActive(true);
        resultRevealRoutine = StartCoroutine(PlayResultReveal());
    }

    private void EnsureResultReveal()
    {
        if (resultRevealRoot != null) return;

        resultRevealRoot = new GameObject(
            "YutResultReveal",
            typeof(RectTransform),
            typeof(CanvasGroup),
            typeof(Image));
        RectTransform rootRect = resultRevealRoot.GetComponent<RectTransform>();
        rootRect.SetParent(transform, false);
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.anchoredPosition = new Vector2(0f, 55f);
        rootRect.sizeDelta = new Vector2(420f, 176f);
        rootRect.SetAsLastSibling();

        Image background = resultRevealRoot.GetComponent<Image>();
        background.sprite = resultRevealBackground;
        background.type = resultRevealBackground != null ? Image.Type.Sliced : Image.Type.Simple;
        background.color = resultRevealBackground != null
            ? Color.white
            : new Color32(255, 249, 236, 248);
        background.raycastTarget = false;

        if (resultRevealGlow != null)
        {
            GameObject glowObject = new GameObject("Glow", typeof(RectTransform), typeof(Image));
            RectTransform glowRect = glowObject.GetComponent<RectTransform>();
            glowRect.SetParent(rootRect, false);
            glowRect.anchorMin = Vector2.zero;
            glowRect.anchorMax = Vector2.one;
            glowRect.offsetMin = new Vector2(-28f, -28f);
            glowRect.offsetMax = new Vector2(28f, 28f);
            Image glow = glowObject.GetComponent<Image>();
            glow.sprite = resultRevealGlow;
            glow.color = Color.white;
            glow.raycastTarget = false;
        }

        resultRevealTitle = CreateRevealText(
            "ResultTitle",
            rootRect,
            new Vector2(0f, 22f),
            new Vector2(360f, 82f),
            52f,
            FontStyles.Normal);
        resultRevealDetail = CreateRevealText(
            "ResultDetail",
            rootRect,
            new Vector2(0f, -48f),
            new Vector2(360f, 40f),
            23f,
            FontStyles.Normal);
        resultRevealDetail.color = new Color32(58, 38, 24, 255);

        resultRevealCanvasGroup = resultRevealRoot.GetComponent<CanvasGroup>();
        resultRevealCanvasGroup.interactable = false;
        resultRevealCanvasGroup.blocksRaycasts = false;
        resultRevealRoot.SetActive(false);
    }

    private TMP_Text CreateRevealText(
        string objectName,
        Transform parent,
        Vector2 anchoredPosition,
        Vector2 size,
        float fontSize,
        FontStyles style)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        if (resultText != null) text.font = resultText.font;
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.enableWordWrapping = false;
        text.raycastTarget = false;
        return text;
    }

    private IEnumerator PlayResultReveal()
    {
        const float enterDuration = 0.18f;
        const float holdDuration = 0.95f;
        const float exitDuration = 0.25f;

        RectTransform rect = resultRevealRoot.GetComponent<RectTransform>();
        float elapsed = 0f;
        while (elapsed < enterDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / enterDuration);
            resultRevealCanvasGroup.alpha = t;
            rect.localScale = Vector3.one * Mathf.Lerp(0.84f, 1f, t);
            yield return null;
        }

        resultRevealCanvasGroup.alpha = 1f;
        rect.localScale = Vector3.one;
        yield return new WaitForSecondsRealtime(holdDuration);

        elapsed = 0f;
        while (elapsed < exitDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            resultRevealCanvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / exitDuration);
            yield return null;
        }

        resultRevealRoot.SetActive(false);
        resultRevealRoutine = null;
    }
}
