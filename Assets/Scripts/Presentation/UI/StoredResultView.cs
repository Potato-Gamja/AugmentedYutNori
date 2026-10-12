using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 저장된 윷 결과 칩과 동일 목적지 결과 선택창의 생성·정리를 담당한다.
/// 결과 소비 여부는 GameRules가 결정하며 이 클래스는 표시와 입력 전달만 수행한다.
/// </summary>
public sealed class StoredResultView : MonoBehaviour
{
    private const int MaxStoredResultCount = 6;
    private TMP_Text resultText;
    private RectTransform resultRoot;
    private GameObject chipTemplate;
    private Sprite chipNormal;
    private Sprite chipBonus;
    private Sprite chipConsumeRed;
    private Sprite chipConsumeBlue;
    private Sprite chipBack;
    private GameObject moveOptionPopup;
    private TMP_Text countText;
    private GameObject emptyHint;
    private readonly List<GameObject> resultChips = new();

    public void Configure(
        TMP_Text resultLabel,
        RectTransform storedResultRoot,
        GameObject storedResultChipTemplate,
        Sprite normal,
        Sprite bonus,
        Sprite consumeRed,
        Sprite consumeBlue,
        Sprite back)
    {
        resultText = resultLabel;
        resultRoot = storedResultRoot;
        chipTemplate = storedResultChipTemplate;
        chipNormal = normal;
        chipBonus = bonus;
        chipConsumeRed = consumeRed;
        chipConsumeBlue = consumeBlue;
        chipBack = back;
        Transform panel = resultRoot != null ? resultRoot.parent : null;
        countText = panel?.Find("Count")?.GetComponent<TMP_Text>();
        emptyHint = panel?.Find("EmptyHint")?.gameObject;
        ConfigureResultGrid();
    }

    public void SetResults(string value)
    {
        if (resultText != null) resultText.text = value;
        RefreshResultChips(value);
    }

    public void MarkPending(int resultIndex, int team)
    {
        if (resultIndex < 0 || resultIndex >= resultChips.Count) return;
        GameObject chip = resultChips[resultIndex];
        if (chip == null) return;

        Image image = chip.GetComponent<Image>();
        if (image != null)
        {
            Sprite pendingSprite = team == 0 ? chipConsumeRed : chipConsumeBlue;
            if (pendingSprite != null) image.sprite = pendingSprite;
            if (team >= 2) image.color = TurnHudView.GetTeamColor(team);
        }

        TMP_Text label = chip.GetComponentInChildren<TMP_Text>(true);
        if (label == null) return;
        label.color = Color.white;
        label.text = label.text.Replace("#B36B00", "#FFE7A6");
    }

    public void ShowChoices(IReadOnlyList<string> optionLabels, int team, Action<int> onSelected)
    {
        HideChoices();
        if (optionLabels == null || optionLabels.Count == 0 || chipTemplate == null) return;

        moveOptionPopup = new GameObject(
            "MoveOptionPopup",
            typeof(RectTransform),
            typeof(Image),
            typeof(Outline),
            typeof(HorizontalLayoutGroup));

        RectTransform popupRect = moveOptionPopup.GetComponent<RectTransform>();
        Canvas canvas = GetComponentInParent<Canvas>();
        Transform popupParent = canvas != null ? canvas.rootCanvas.transform : transform;
        popupRect.SetParent(popupParent, false);
        popupRect.anchorMin = new Vector2(0.5f, 0.5f);
        popupRect.anchorMax = new Vector2(0.5f, 0.5f);
        popupRect.pivot = new Vector2(0.5f, 0.5f);
        popupRect.anchoredPosition = new Vector2(0f, -260f);
        popupRect.sizeDelta = new Vector2(Mathf.Max(260f, optionLabels.Count * 206f + 32f), 100f);
        popupRect.SetAsLastSibling();

        Image background = moveOptionPopup.GetComponent<Image>();
        background.color = new Color32(255, 249, 236, 250);
        background.raycastTarget = false;

        Outline outline = moveOptionPopup.GetComponent<Outline>();
        outline.effectColor = TurnHudView.GetTeamColor(team);
        outline.effectDistance = new Vector2(3f, -3f);

        HorizontalLayoutGroup layout = moveOptionPopup.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(16, 16, 18, 18);
        layout.spacing = 16f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        for (int i = 0; i < optionLabels.Count; i++)
        {
            int selectedIndex = i;
            string optionLabel = optionLabels[i];
            GameObject chip = CreateChip(moveOptionPopup.transform, optionLabel, false);
            chip.name = $"MoveOption_{i}";

            RectTransform chipRect = chip.GetComponent<RectTransform>();
            chipRect.sizeDelta = new Vector2(190f, 64f);

            Image image = chip.GetComponent<Image>();
            if (image != null) image.raycastTarget = true;

            Button button = chip.GetComponent<Button>();
            if (button == null) button = chip.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                HideChoices();
                onSelected?.Invoke(selectedIndex);
            });
            SoundManager.BindButtonClick(button);
        }
    }

    public void HideChoices()
    {
        if (moveOptionPopup == null) return;
        Destroy(moveOptionPopup);
        moveOptionPopup = null;
    }

    private void RefreshResultChips(string value)
    {
        foreach (GameObject chip in resultChips)
            if (chip != null) Destroy(chip);
        resultChips.Clear();

        SetEmptyState(0);
        if (resultRoot == null || chipTemplate == null || string.IsNullOrWhiteSpace(value)) return;
        int colon = value.IndexOf(':');
        string payload = colon >= 0 ? value[(colon + 1)..].Trim() : value.Trim();
        if (payload == "-" || payload.EndsWith("-")) return;

        string[] entries = payload.Split(new[] { "  " }, StringSplitOptions.RemoveEmptyEntries);
        int visibleCount = Mathf.Min(entries.Length, MaxStoredResultCount);
        for (int i = 0; i < visibleCount; i++)
        {
            GameObject chip = CreateChip(resultRoot, entries[i].Trim(), true);
            RectTransform rect = chip.GetComponent<RectTransform>();
            if (rect != null)
            {
                int column = i % 2;
                int row = i / 2;
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.sizeDelta = new Vector2(172f, 52f);
                rect.anchoredPosition = new Vector2(column * 188f, -row * 64f);
            }
            TMP_Text label = chip.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.fontSize = 28f;
            resultChips.Add(chip);
        }
        SetEmptyState(visibleCount);
    }

    private void SetEmptyState(int visibleCount)
    {
        if (countText != null) countText.text = $"{visibleCount}개";
        if (emptyHint != null) emptyHint.SetActive(visibleCount == 0);
    }

    private void ConfigureResultGrid()
    {
        if (resultRoot == null) return;

        HorizontalLayoutGroup horizontal = resultRoot.GetComponent<HorizontalLayoutGroup>();
        if (horizontal != null) horizontal.enabled = false;

    }

    private GameObject CreateChip(Transform parent, string entry, bool resultNameOnly)
    {
        GameObject chip = Instantiate(chipTemplate, parent);
        chip.name = "StoredResultChip";
        chip.SetActive(true);

        Image image = chip.GetComponent<Image>();
        if (image != null)
            image.sprite = entry.Contains("빽도") ? chipBack : entry.Contains("(") ? chipBonus : chipNormal;

        TMP_Text label = chip.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            string display = entry.Trim();
            if (resultNameOnly)
            {
                int separator = display.IndexOfAny(new[] { ' ', '(' });
                label.text = separator > 0 ? display[..separator] : display;
            }
            else
            {
                int bonus = display.IndexOf('(');
                label.text = bonus >= 0
                    ? display[..bonus] + "<size=79%><color=#B36B00>" + display[bonus..] + "</color></size>"
                    : display;
            }
        }

        return chip;
    }
}
