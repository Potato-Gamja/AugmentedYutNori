using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 뽑기 결과 팝업. 1회는 아이콘 하나를 크게, 10연차는 칸 10개를 한 화면에 보여 준다.
/// 아이콘은 작게 시작해 튀어나오듯 커지고, 10연차는 칸마다 조금씩 늦게 나온다. 새 아이콘은 NEW로 강조한다.
/// 결과를 받지 못했을 때(시간 초과 등)는 ShowNotice로 아이콘 없이 안내 문구만 크게 보여 준다.
/// </summary>
public sealed class GachaResultView : UIBase
{
    [SerializeField] private RectTransform iconRoot;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private Image resultPill;
    [SerializeField] private Button confirmButton;
    [SerializeField] private float revealDuration = 0.35f;

    [Header("10연차")]
    [SerializeField] private RectTransform popup;
    [SerializeField] private RectTransform confirmRoot;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private GameObject[] singleOnly;    // 1회 결과에만 보이는 것 (큰 아이콘, 이름, 결과 표시 등)
    [SerializeField] private GameObject multiRoot;       // 10연차 칸들의 부모
    [SerializeField] private GachaResultSlot[] slots;
    [SerializeField] private Vector2 multiPopupSize = new Vector2(1000f, 700f);
    [SerializeField] private float slotRevealDelay = 0.08f;

    [Header("안내 (결과를 받지 못했을 때)")]
    [SerializeField] private float noticeNameY = -270f;
    [SerializeField] private float noticePillY = -340f;

    private Coroutine reveal;
    private Vector2 singlePopupSize;
    private Vector2 singleConfirmPosition;
    private string singleDescription;
    private Vector2 singleNamePosition;
    private Vector2 singlePillPosition;
    private bool layoutSaved;

    public override void Init()
    {
        confirmButton?.onClick.AddListener(Close);
        SaveSingleLayout();
    }

    private void OnDestroy()
    {
        confirmButton?.onClick.RemoveListener(Close);
    }

    public void Show(GachaResultResponse result, Func<string, string> nameOf, Func<string, Sprite> spriteOf)
    {
        List<GachaDrawResult> results = result.results;
        if (results == null || results.Count <= 1 || slots == null || slots.Length == 0)
        {
            GachaDrawResult first = results != null && results.Count > 0
                ? results[0]
                : new GachaDrawResult { iconId = result.iconId, duplicate = result.duplicate, count = result.count };
            ShowSingle(first, nameOf(first.iconId), spriteOf(first.iconId));
            return;
        }

        ShowMany(results, nameOf, spriteOf);
    }

    // 1회 결과 (에디터 미리보기 UIPopupPreview가 사용)
    public void Show(GachaResultResponse result, string iconName, Sprite icon)
    {
        ShowSingle(new GachaDrawResult { iconId = result.iconId, duplicate = result.duplicate, count = result.count },
            iconName, icon);
    }

    // 아이콘 없이 큰 글씨(message)와 작은 설명(detail)만 보여 준다
    public void ShowNotice(string message, string detail)
    {
        ApplyLayout(multi: false);

        GameObject nameObject = nameText != null ? nameText.gameObject : null;
        GameObject pillObject = resultPill != null ? resultPill.gameObject : null;
        foreach (GameObject target in singleOnly ?? Array.Empty<GameObject>())
            if (target != null) target.SetActive(target == nameObject || target == pillObject);

        if (nameText != null)
        {
            nameText.text = message;
            ((RectTransform)nameText.transform).anchoredPosition = new Vector2(singleNamePosition.x, noticeNameY);
        }
        if (resultText != null) resultText.text = detail;
        if (resultPill != null)
        {
            resultPill.color = new Color32(243, 230, 207, 255);
            resultPill.rectTransform.anchoredPosition = new Vector2(singlePillPosition.x, noticePillY);
        }
    }

    private void ShowSingle(GachaDrawResult result, string iconName, Sprite icon)
    {
        ApplyLayout(multi: false);

        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
        }
        if (nameText != null) nameText.text = iconName ?? result.iconId;
        if (resultText != null)
            resultText.text = result.duplicate ? $"이미 가진 아이콘이에요 (x{result.count})" : "NEW! 새 아이콘을 얻었어요";
        if (resultPill != null) resultPill.color = result.duplicate
            ? new Color32(243, 230, 207, 255) : new Color32(224, 149, 15, 255);

        StartReveal(Reveal());
    }

    private void ShowMany(List<GachaDrawResult> results, Func<string, string> nameOf, Func<string, Sprite> spriteOf)
    {
        ApplyLayout(multi: true);

        int newCount = 0;
        for (int i = 0; i < slots.Length; i++)
        {
            GachaResultSlot slot = slots[i];
            if (slot == null) continue;

            bool used = i < results.Count;
            slot.gameObject.SetActive(used);
            if (!used) continue;

            GachaDrawResult result = results[i];
            if (!result.duplicate) newCount++;
            slot.Show(result, nameOf(result.iconId), spriteOf(result.iconId));
            if (slot.IconRoot != null) slot.IconRoot.localScale = Vector3.zero;
        }

        if (descriptionText != null)
            descriptionText.text = newCount > 0 ? $"{results.Count}연차 결과 · 새 아이콘 {newCount}개!" : $"{results.Count}연차 결과 · 새 아이콘은 없어요";

        StartReveal(RevealSlots());
    }

    protected override void OnClose()
    {
        if (reveal != null) StopCoroutine(reveal);
        reveal = null;
        if (iconRoot != null) iconRoot.localScale = Vector3.one;
        if (slots == null) return;
        foreach (GachaResultSlot slot in slots)
            if (slot != null && slot.IconRoot != null) slot.IconRoot.localScale = Vector3.one;
    }

    private void SaveSingleLayout()
    {
        if (layoutSaved) return;
        layoutSaved = true;
        if (popup != null) singlePopupSize = popup.sizeDelta;
        if (confirmRoot != null) singleConfirmPosition = confirmRoot.anchoredPosition;
        if (descriptionText != null) singleDescription = descriptionText.text;
        if (nameText != null) singleNamePosition = ((RectTransform)nameText.transform).anchoredPosition;
        if (resultPill != null) singlePillPosition = resultPill.rectTransform.anchoredPosition;
    }

    // 10연차는 팝업을 넓히고 확인 버튼을 가운데로 옮긴다. 1회는 프리팹 그대로
    private void ApplyLayout(bool multi)
    {
        SaveSingleLayout();

        foreach (GameObject target in singleOnly ?? Array.Empty<GameObject>())
            if (target != null) target.SetActive(!multi);
        if (multiRoot != null) multiRoot.SetActive(multi);

        if (popup != null) popup.sizeDelta = multi ? multiPopupSize : singlePopupSize;
        if (confirmRoot != null)
        {
            float widthGrowth = multi ? multiPopupSize.x - singlePopupSize.x : 0f;
            float heightGrowth = multi ? multiPopupSize.y - singlePopupSize.y : 0f;
            confirmRoot.anchoredPosition = singleConfirmPosition + new Vector2(widthGrowth / 2f, -heightGrowth);
        }
        if (!multi && descriptionText != null) descriptionText.text = singleDescription;

        // ShowNotice가 옮겨 둔 위치를 되돌린다
        if (nameText != null) ((RectTransform)nameText.transform).anchoredPosition = singleNamePosition;
        if (resultPill != null) resultPill.rectTransform.anchoredPosition = singlePillPosition;
    }

    private void StartReveal(IEnumerator routine)
    {
        if (reveal != null) StopCoroutine(reveal);
        reveal = StartCoroutine(routine);
    }

    private IEnumerator Reveal()
    {
        yield return Pop(iconRoot);
        reveal = null;
    }

    // 칸마다 slotRevealDelay씩 늦게 튀어나온다
    private IEnumerator RevealSlots()
    {
        float time = 0f;
        float total = revealDuration + slotRevealDelay * (slots.Length - 1);
        while (time < total)
        {
            time += Time.unscaledDeltaTime;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null || slots[i].IconRoot == null) continue;
                slots[i].IconRoot.localScale = Vector3.one * PopScale((time - slotRevealDelay * i) / revealDuration);
            }
            yield return null;
        }
        foreach (GachaResultSlot slot in slots)
            if (slot != null && slot.IconRoot != null) slot.IconRoot.localScale = Vector3.one;
        reveal = null;
    }

    private IEnumerator Pop(RectTransform target)
    {
        if (target == null) yield break;

        float time = 0f;
        while (time < revealDuration)
        {
            time += Time.unscaledDeltaTime;
            target.localScale = Vector3.one * PopScale(time / revealDuration);
            yield return null;
        }
        target.localScale = Vector3.one;
    }

    // 0 → 1.15 → 1 로 커지는 간단한 연출
    private static float PopScale(float t)
    {
        t = Mathf.Clamp01(t);
        return t < 0.7f ? Mathf.Lerp(0f, 1.15f, t / 0.7f) : Mathf.Lerp(1.15f, 1f, (t - 0.7f) / 0.3f);
    }
}
