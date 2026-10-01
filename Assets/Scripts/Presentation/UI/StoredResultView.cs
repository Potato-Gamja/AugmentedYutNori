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
    private TMP_Text resultText;
    private RectTransform resultRoot;
    private GameObject chipTemplate;
    private Sprite chipNormal;
    private Sprite chipBonus;
    private Sprite chipConsumeRed;
    private Sprite chipConsumeBlue;
    private Sprite chipBack;
    private GameObject moveOptionPopup;
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
        popupRect.SetParent(transform, false);
        popupRect.anchorMin = new Vector2(0.5f, 0.5f);
        popupRect.anchorMax = new Vector2(0.5f, 0.5f);
        popupRect.pivot = new Vector2(0.5f, 0.5f);
        popupRect.anchoredPosition = new Vector2(0f, -285f);
        popupRect.sizeDelta = new Vector2(Mathf.Max(210f, optionLabels.Count * 148f + 24f), 84f);
        popupRect.SetAsLastSibling();

        Image background = moveOptionPopup.GetComponent<Image>();
        background.color = new Color32(255, 249, 236, 250);
        background.raycastTarget = false;

        Outline outline = moveOptionPopup.GetComponent<Outline>();
        outline.effectColor = TurnHudView.GetTeamColor(team);
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
            GameObject chip = CreateChip(moveOptionPopup.transform, optionLabel);
            chip.name = $"MoveOption_{i}";

            RectTransform chipRect = chip.GetComponent<RectTransform>();
            chipRect.sizeDelta = new Vector2(134f, 52f);

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

        if (resultRoot == null || chipTemplate == null || string.IsNullOrWhiteSpace(value)) return;
        int colon = value.IndexOf(':');
        string payload = colon >= 0 ? value[(colon + 1)..].Trim() : value.Trim();
        if (payload == "-" || payload.EndsWith("-")) return;

        string[] entries = payload.Split(new[] { "  " }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string raw in entries)
            resultChips.Add(CreateChip(resultRoot, raw.Trim()));
    }

    private GameObject CreateChip(Transform parent, string entry)
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
            string display = entry.Replace("빽도", "BACK DO").Replace("도", "DO").Replace("개", "GAE")
                .Replace("걸", "GEOL").Replace("윷", "YUT").Replace("모", "MO");
            int bonus = display.IndexOf('(');
            label.text = bonus >= 0
                ? display[..bonus] + "<size=79%><color=#B36B00>" + display[bonus..] + "</color></size>"
                : display;
        }

        return chip;
    }
}
