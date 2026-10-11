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
    [SerializeField] private TMP_Text statusText;

    public event Action PlayRequested;
    public event Action GachaRequested;
    public event Action CollectionRequested;
    public event Action ProfileRequested;
    public event Action AchievementRequested;
    public event Action SettingsRequested;

    public override void Init()
    {
        playButton?.onClick.AddListener(OnPlayClicked);
        gachaButton?.onClick.AddListener(OnGachaClicked);
        collectionButton?.onClick.AddListener(OnCollectionClicked);
        profileButton?.onClick.AddListener(OnProfileClicked);
        achievementButton?.onClick.AddListener(OnAchievementClicked);
        settingsButton?.onClick.AddListener(OnSettingsClicked);
    }

    private void OnDestroy()
    {
        playButton?.onClick.RemoveListener(OnPlayClicked);
        gachaButton?.onClick.RemoveListener(OnGachaClicked);
        collectionButton?.onClick.RemoveListener(OnCollectionClicked);
        profileButton?.onClick.RemoveListener(OnProfileClicked);
        achievementButton?.onClick.RemoveListener(OnAchievementClicked);
        settingsButton?.onClick.RemoveListener(OnSettingsClicked);
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
    private void OnSettingsClicked() => SettingsRequested?.Invoke();
}
