using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>로그인 후 표시되는 아웃게임 기능 허브.</summary>
public sealed class MainMenuPanel : UIBase
{
    [SerializeField] private Button playButton;
    [SerializeField] private Button gachaButton;
    [SerializeField] private Button collectionButton;
    [SerializeField] private Button profileButton;
    [SerializeField] private Button achievementButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button tutorialButton;
    [SerializeField] private TMP_Text statusText;

    [Header("프로필 표시")]
    [SerializeField] private Image profileIcon;
    [SerializeField] private TMP_Text nicknameText;
    [SerializeField] private TMP_Text goldText;

    public void SetProfile(string nickname, string iconId)
    {
        if (nicknameText != null)
            nicknameText.text = string.IsNullOrWhiteSpace(nickname) ? "닉네임 미설정" : nickname;
        if (profileIcon != null)
        {
            var icon = string.IsNullOrWhiteSpace(iconId) ? null : Resources.Load<Sprite>(ProfileIconCatalog.SpritePath(iconId));
            profileIcon.sprite = icon != null ? icon : profileIconFallback;
            profileIcon.preserveAspect = true;
        }
    }
    private Sprite profileIconFallback;
    private LobbyNetworkCoordinator coordinator;
    private int profileRequestVersion;
    public void SetGold(int gold)
    {
        if (goldText != null) goldText.text = Mathf.Max(0, gold).ToString("N0");
    }

    public event Action PlayRequested;
    public event Action GachaRequested;
    public event Action CollectionRequested;
    public event Action ProfileRequested;
    public event Action AchievementRequested;
    public event Action SettingsRequested;
    public event Action TutorialRequested;

    public override void Init()
    {
        profileIconFallback = profileIcon != null ? profileIcon.sprite : null;
        playButton?.onClick.AddListener(OnPlayClicked);
        gachaButton?.onClick.AddListener(OnGachaClicked);
        collectionButton?.onClick.AddListener(OnCollectionClicked);
        profileButton?.onClick.AddListener(OnProfileClicked);
        achievementButton?.onClick.AddListener(OnAchievementClicked);
        settingsButton?.onClick.AddListener(OnSettingsClicked);
        tutorialButton?.onClick.AddListener(OnTutorialClicked);
    }

    private void OnDestroy()
    {
        profileRequestVersion++;
        UnsubscribeDisplay();
        playButton?.onClick.RemoveListener(OnPlayClicked);
        gachaButton?.onClick.RemoveListener(OnGachaClicked);
        collectionButton?.onClick.RemoveListener(OnCollectionClicked);
        profileButton?.onClick.RemoveListener(OnProfileClicked);
        achievementButton?.onClick.RemoveListener(OnAchievementClicked);
        settingsButton?.onClick.RemoveListener(OnSettingsClicked);
        tutorialButton?.onClick.RemoveListener(OnTutorialClicked);
    }

    protected override async void OnOpen()
    {
        UnsubscribeDisplay();
        int version = ++profileRequestVersion;
        coordinator = FindFirstObjectByType<LobbyNetworkCoordinator>();
        if (coordinator == null) return;
        coordinator.LocalDisplayChanged += RefreshCachedDisplay;
        RefreshCachedDisplay();
        try
        {
            await coordinator.GetProfileAsync();
            if (this != null && IsOpen && version == profileRequestVersion) RefreshCachedDisplay();
        }
        catch (Exception)
        {
            // 표시 조회 실패는 현재 캐시를 유지한다. 로그인·방 입장 상태 문구를 덮어쓰지 않는다.
        }
    }

    protected override void OnClose()
    {
        profileRequestVersion++;
        UnsubscribeDisplay();
    }

    private void UnsubscribeDisplay()
    {
        if (!ReferenceEquals(coordinator, null)) coordinator.LocalDisplayChanged -= RefreshCachedDisplay;
    }

    private void RefreshCachedDisplay()
    {
        if (this == null || !IsOpen || coordinator == null) return;
        SetProfile(coordinator.LocalNickname, coordinator.LocalIconId);
        SetGold(coordinator.LocalGold);
    }

    public void ShowStatus(string message)
    {
        if (statusText != null) statusText.text = message ?? string.Empty;
    }

    private void OnPlayClicked() => PlayRequested?.Invoke();
    private void OnGachaClicked() => GachaRequested?.Invoke();
    private void OnCollectionClicked() => CollectionRequested?.Invoke();
    private void OnProfileClicked() => ProfileRequested?.Invoke();
    private void OnAchievementClicked() => AchievementRequested?.Invoke();
    private void OnTutorialClicked() => TutorialRequested?.Invoke();
    private void OnSettingsClicked() => SettingsRequested?.Invoke();
}
