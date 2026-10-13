using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 업적 목록 한 줄. 이름·설명·진행도(3/5)·보상 골드와 상태 버튼
//   진행 중 → 버튼 꺼짐 "진행 중" / 달성 → "받기" / 받음 → 버튼 꺼짐 "받음"
public class AchievementItemView : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text progressText;
    [SerializeField] private Image progressFill;      // Image Type = Filled (없어도 됨)
    [SerializeField] private TMP_Text rewardText;
    [SerializeField] private Button claimButton;
    [SerializeField] private TMP_Text claimButtonLabel;

    public event Action<AchievementItemView> ClaimClicked;

    public AchievementEntry Entry { get; private set; }
    private bool busy;

    private void Awake()
    {
        claimButton?.onClick.AddListener(OnClaimClicked);
    }

    private void OnDestroy()
    {
        claimButton?.onClick.RemoveListener(OnClaimClicked);
    }

    public void Bind(AchievementEntry entry)
    {
        Entry = entry;
        if (entry == null) return;
        SoundManager.BindButtonClick(claimButton);

        int goal = Mathf.Max(1, entry.goal);
        int progress = Mathf.Clamp(entry.progress, 0, goal);

        if (nameText != null) nameText.text = entry.name ?? entry.id;
        if (descriptionText != null) descriptionText.text = entry.description ?? string.Empty;
        if (progressText != null) progressText.text = $"{progress} / {goal}";
        if (progressFill != null) progressFill.fillAmount = (float)progress / goal;
        if (rewardText != null) rewardText.text = $"{entry.rewardGold:N0} 골드";

        SetBusy(false);
    }

    // 받는 중에는 버튼을 막는다
    public void SetBusy(bool busy)
    {
        this.busy = busy;
        if (Entry == null) return;

        bool canClaim = Entry.achieved && !Entry.claimed;
        if (claimButton != null) claimButton.interactable = canClaim && !busy;
        if (claimButtonLabel != null)
        {
            claimButtonLabel.text = Entry.claimed ? "받음" : canClaim ? (busy ? "받는 중" : "받기") : "진행 중";
            claimButtonLabel.color = canClaim ? Color.white : new Color(0.35f, 0.28f, 0.18f);
        }
        if (claimButton?.targetGraphic != null)
            claimButton.targetGraphic.color = canClaim
                ? new Color(0.12f, 0.58f, 0.36f)
                : Entry.claimed ? new Color(0.88f, 0.75f, 0.5f) : new Color(0.9f, 0.86f, 0.76f);
    }

    // 받기 성공 후 상태만 바꿔 다시 그린다
    public void MarkClaimed()
    {
        if (Entry == null) return;
        Entry.claimed = true;
        SetBusy(false);
    }

    private void OnClaimClicked()
    {
        if (!busy && Entry != null && Entry.achieved && !Entry.claimed) ClaimClicked?.Invoke(this);
    }
}
