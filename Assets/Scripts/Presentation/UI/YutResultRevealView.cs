using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 윷을 던진 직후 결과를 크게 보여주는 일회성 연출을 담당한다.
/// 저장 결과와 추가 던지기 계산은 GameRules가 이미 끝낸 값을 받는다.
/// </summary>
public sealed class YutResultRevealView : MonoBehaviour
{
    private TMP_Text fontSource;
    private Sprite backgroundSprite;
    private Sprite glowSprite;
    private GameObject revealRoot;
    private TMP_Text title;
    private TMP_Text detail;
    private CanvasGroup canvasGroup;
    private Coroutine routine;
    private Sprite stickFlat;
    private Sprite stickRound;
    private Sprite stickBackdo;
    private RectTransform resultPanel;
    private TMP_Text panelResult;
    private TMP_Text panelSteps;
    private readonly Image[] panelSticks = new Image[4];

    public void Configure(TMP_Text source, Sprite background, Sprite glow, Sprite flat, Sprite round, Sprite backdo)
    {
        fontSource = source;
        backgroundSprite = background;
        glowSprite = glow;
        stickFlat = flat;
        stickRound = round;
        stickBackdo = backdo;
        TryBindResultPanel();
    }

    public void Show(YutThrowOutcome outcome, bool grantedExtraThrow, int team)
    {
        if (TryBindResultPanel())
        {
            ShowInResultPanel(outcome, grantedExtraThrow, team);
            return;
        }

        EnsureCreated();
        int steps = outcome.Result.Steps();
        title.text = outcome.IsResultChanged
            ? $"{outcome.OriginalResult.DisplayName()} → {outcome.Result.DisplayName()}"
            : outcome.Result.DisplayName();
        title.color = TurnHudView.GetTeamColor(team);

        string moveText = steps < 0 ? $"뒤로 {Mathf.Abs(steps)}칸" : $"앞으로 {steps}칸";
        if (outcome.IsResultChanged)
            detail.text = $"도박사 효과 · {moveText} · 추가 던지기 없음";
        else if (outcome.Result.GrantsExtraThrow() && !grantedExtraThrow)
            detail.text = $"사냥꾼 효과 · {moveText} · 추가 던지기 없음";
        else
            detail.text = grantedExtraThrow ? $"{moveText} · 추가 던지기 1회" : moveText;

        if (routine != null) StopCoroutine(routine);
        revealRoot.SetActive(true);
        routine = StartCoroutine(Play());
    }

    public void Show(YutResult result, int remainingThrows, int team)
    {
        bool[] sticks = GetRepresentativeSticks(result);
        Show(new YutThrowOutcome(sticks, result), result.GrantsExtraThrow() && remainingThrows > 0, team);
    }

    private static bool[] GetRepresentativeSticks(YutResult result)
    {
        return result switch
        {
            YutResult.BackDo => new[] { true, false, false, false },
            YutResult.Do => new[] { false, true, false, false },
            YutResult.Gae => new[] { false, true, true, false },
            YutResult.Geol => new[] { false, true, true, true },
            YutResult.Yut => new[] { true, true, true, true },
            _ => new[] { false, false, false, false }
        };
    }

    private bool TryBindResultPanel()
    {
        if (resultPanel != null) return panelResult != null && panelSteps != null;

        resultPanel = transform.Find("RightColumn/ResultPanel") as RectTransform;
        if (resultPanel == null) return false;

        panelResult = resultPanel.Find("Well/ResultValue")?.GetComponent<TMP_Text>();
        panelSteps = resultPanel.Find("Well/ResultSteps")?.GetComponent<TMP_Text>();
        for (int i = 0; i < panelSticks.Length; i++)
            panelSticks[i] = resultPanel.Find($"Well/Stick_{i}")?.GetComponent<Image>();
        return panelResult != null && panelSteps != null;
    }

    private void ShowInResultPanel(YutThrowOutcome outcome, bool grantedExtraThrow, int team)
    {
        int steps = outcome.Result.Steps();
        panelResult.text = outcome.OriginalResult.DisplayName();
        panelResult.color = TurnHudView.GetTeamColor(team);
        panelSteps.text = steps < 0 ? "1칸 뒤로" : $"{steps}칸 이동";
        if (grantedExtraThrow)
            panelSteps.text += " · 한 번 더";
        else if (outcome.Result.GrantsExtraThrow())
            panelSteps.text += " · 사냥꾼 효과 · 추가 던지기 없음";

        for (int i = 0; i < panelSticks.Length; i++)
        {
            if (panelSticks[i] == null) continue;
            Sprite face = GetStickFace(outcome.Sticks, i);
            if (face != null) panelSticks[i].sprite = face;
            panelSticks[i].color = Color.white;
            panelSticks[i].preserveAspect = true;
            panelSticks[i].rectTransform.localScale = Vector3.one;
        }

        if (routine != null) StopCoroutine(routine);
        routine = outcome.IsResultChanged
            ? StartCoroutine(PlayResultChange(outcome, steps))
            : StartCoroutine(PulsePanelResult());
    }

    private Sprite GetStickFace(bool[] sticks, int index)
    {
        if (sticks == null || index < 0 || index >= sticks.Length || !sticks[index])
            return stickRound;

        return index == StandardGetResult.MarkedStick && stickBackdo != null
            ? stickBackdo
            : stickFlat;
    }

    private IEnumerator PlayResultChange(YutThrowOutcome outcome, int steps)
    {
        const float effectDuration = 0.9f;
        const float resultPause = 0.2f;
        float elapsed = 0f;
        Color effectColor = new Color32(255, 205, 82, 255);
        panelSteps.text = "도박사 효과";

        Vector2[] originalPositions = new Vector2[panelSticks.Length];
        for (int i = 0; i < panelSticks.Length; i++)
            if (panelSticks[i] != null) originalPositions[i] = panelSticks[i].rectTransform.anchoredPosition;

        while (elapsed < effectDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / effectDuration);
            float envelope = Mathf.Sin(t * Mathf.PI);
            float pulse = 1f + envelope * 0.1f;
            Color color = Color.Lerp(Color.white, effectColor, envelope);
            for (int i = 0; i < panelSticks.Length; i++)
            {
                if (panelSticks[i] == null) continue;
                panelSticks[i].rectTransform.localScale = Vector3.one * pulse;
                float offset = Mathf.Sin((t * 4f + i * 0.35f) * Mathf.PI * 2f) * 6f * envelope;
                panelSticks[i].rectTransform.anchoredPosition = originalPositions[i] + Vector2.up * offset;
                panelSticks[i].color = color;
            }
            yield return null;
        }

        for (int i = 0; i < panelSticks.Length; i++)
        {
            if (panelSticks[i] == null) continue;
            panelSticks[i].rectTransform.localScale = Vector3.one;
            panelSticks[i].rectTransform.anchoredPosition = originalPositions[i];
            panelSticks[i].color = Color.white;
        }

        yield return new WaitForSecondsRealtime(resultPause);
        panelResult.text = outcome.Result.DisplayName();
        panelSteps.text = $"도박사 효과\n{(steps < 0 ? "1칸 뒤로" : $"{steps}칸 이동")}";
        yield return PulsePanelResult();
    }

    private IEnumerator PulsePanelResult()
    {
        RectTransform resultRect = panelResult.rectTransform;
        const float duration = 0.45f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float scale = Mathf.Lerp(1.28f, 1f, 1f - (1f - t) * (1f - t));
            resultRect.localScale = Vector3.one * scale;
            yield return null;
        }
        resultRect.localScale = Vector3.one;
        routine = null;
    }

    private void EnsureCreated()
    {
        if (revealRoot != null) return;

        revealRoot = new GameObject("YutResultReveal", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        RectTransform rootRect = revealRoot.GetComponent<RectTransform>();
        rootRect.SetParent(transform, false);
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.anchoredPosition = new Vector2(0f, 55f);
        rootRect.sizeDelta = new Vector2(420f, 176f);
        rootRect.SetAsLastSibling();

        Image background = revealRoot.GetComponent<Image>();
        background.sprite = backgroundSprite;
        background.type = backgroundSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        background.color = backgroundSprite != null ? Color.white : new Color32(255, 249, 236, 248);
        background.raycastTarget = false;

        if (glowSprite != null)
        {
            GameObject glowObject = new("Glow", typeof(RectTransform), typeof(Image));
            RectTransform glowRect = glowObject.GetComponent<RectTransform>();
            glowRect.SetParent(rootRect, false);
            glowRect.anchorMin = Vector2.zero;
            glowRect.anchorMax = Vector2.one;
            glowRect.offsetMin = new Vector2(-28f, -28f);
            glowRect.offsetMax = new Vector2(28f, 28f);
            Image glow = glowObject.GetComponent<Image>();
            glow.sprite = glowSprite;
            glow.color = Color.white;
            glow.raycastTarget = false;
        }

        title = CreateText("ResultTitle", rootRect, new Vector2(0f, 22f), new Vector2(360f, 82f), 52f);
        detail = CreateText("ResultDetail", rootRect, new Vector2(0f, -48f), new Vector2(360f, 40f), 23f);
        detail.color = new Color32(58, 38, 24, 255);

        canvasGroup = revealRoot.GetComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        revealRoot.SetActive(false);
    }

    private TMP_Text CreateText(string objectName, Transform parent, Vector2 position, Vector2 size, float fontSize)
    {
        GameObject textObject = new(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        if (fontSource != null) text.font = fontSource.font;
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = fontSize;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }

    private IEnumerator Play()
    {
        const float enterDuration = 0.18f;
        const float holdDuration = 0.95f;
        const float exitDuration = 0.25f;
        RectTransform rect = revealRoot.GetComponent<RectTransform>();

        float elapsed = 0f;
        while (elapsed < enterDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / enterDuration);
            canvasGroup.alpha = t;
            rect.localScale = Vector3.one * Mathf.Lerp(0.84f, 1f, t);
            yield return null;
        }

        canvasGroup.alpha = 1f;
        rect.localScale = Vector3.one;
        yield return new WaitForSecondsRealtime(holdDuration);

        elapsed = 0f;
        while (elapsed < exitDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / exitDuration);
            yield return null;
        }

        revealRoot.SetActive(false);
        routine = null;
    }
}
