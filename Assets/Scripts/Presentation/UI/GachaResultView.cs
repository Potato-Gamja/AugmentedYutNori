using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>뽑기 결과 팝업. 뽑힌 아이콘을 작게 시작해 튀어나오듯 키우며 보여 준다.</summary>
public sealed class GachaResultView : UIBase
{
    [SerializeField] private RectTransform iconRoot;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private float revealDuration = 0.35f;

    private Coroutine reveal;

    public override void Init()
    {
        confirmButton?.onClick.AddListener(Close);
    }

    private void OnDestroy()
    {
        confirmButton?.onClick.RemoveListener(Close);
    }

    public void Show(GachaResultResponse result, string iconName, Sprite icon)
    {
        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
        }
        if (nameText != null) nameText.text = iconName ?? result.iconId;
        if (resultText != null)
            resultText.text = result.duplicate ? $"이미 가진 아이콘이에요 (x{result.count})" : "NEW! 새 아이콘을 얻었어요";

        if (reveal != null) StopCoroutine(reveal);
        reveal = StartCoroutine(Reveal());
    }

    protected override void OnClose()
    {
        if (reveal != null) StopCoroutine(reveal);
        reveal = null;
        if (iconRoot != null) iconRoot.localScale = Vector3.one;
    }

    // 0 → 1.15 → 1 로 커지는 간단한 연출
    private IEnumerator Reveal()
    {
        if (iconRoot == null) yield break;

        float time = 0f;
        while (time < revealDuration)
        {
            time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(time / revealDuration);
            float scale = t < 0.7f ? Mathf.Lerp(0f, 1.15f, t / 0.7f) : Mathf.Lerp(1.15f, 1f, (t - 0.7f) / 0.3f);
            iconRoot.localScale = Vector3.one * scale;
            yield return null;
        }
        iconRoot.localScale = Vector3.one;
        reveal = null;
    }
}
