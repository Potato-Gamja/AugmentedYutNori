using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>공통 음량 설정 패널. 프리팹은 UIManager의 prefabs 목록에 등록한다.</summary>
public sealed class SettingsPanel : UIBase
{
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private Button closeButton;
    [SerializeField] private TMP_Text masterValueText;
    [SerializeField] private TMP_Text bgmValueText;
    [SerializeField] private TMP_Text sfxValueText;

    public override void Init()
    {
        if (masterSlider != null) masterSlider.SetValueWithoutNotify(AudioSettings.Master);
        if (bgmSlider != null) bgmSlider.SetValueWithoutNotify(AudioSettings.Bgm);
        if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(AudioSettings.Sfx);
        masterSlider?.onValueChanged.AddListener(OnVolumeChanged);
        bgmSlider?.onValueChanged.AddListener(OnVolumeChanged);
        sfxSlider?.onValueChanged.AddListener(OnVolumeChanged);
        closeButton?.onClick.AddListener(Close);
        RefreshValues();
    }

    protected override void OnOpen() => RefreshValues();

    private void OnDestroy()
    {
        masterSlider?.onValueChanged.RemoveListener(OnVolumeChanged);
        bgmSlider?.onValueChanged.RemoveListener(OnVolumeChanged);
        sfxSlider?.onValueChanged.RemoveListener(OnVolumeChanged);
        closeButton?.onClick.RemoveListener(Close);
    }

    private void OnVolumeChanged(float _)
    {
        AudioSettings.Set(
            masterSlider != null ? masterSlider.value : AudioSettings.Master,
            bgmSlider != null ? bgmSlider.value : AudioSettings.Bgm,
            sfxSlider != null ? sfxSlider.value : AudioSettings.Sfx);
        RefreshValueLabels();
    }

    private void RefreshValues()
    {
        masterSlider?.SetValueWithoutNotify(AudioSettings.Master);
        bgmSlider?.SetValueWithoutNotify(AudioSettings.Bgm);
        sfxSlider?.SetValueWithoutNotify(AudioSettings.Sfx);
        RefreshValueLabels();
    }

    private void RefreshValueLabels()
    {
        if (masterValueText != null) masterValueText.text = $"{Mathf.RoundToInt(AudioSettings.Master * 100f)}%";
        if (bgmValueText != null) bgmValueText.text = $"{Mathf.RoundToInt(AudioSettings.Bgm * 100f)}%";
        if (sfxValueText != null) sfxValueText.text = $"{Mathf.RoundToInt(AudioSettings.Sfx * 100f)}%";
    }
}
