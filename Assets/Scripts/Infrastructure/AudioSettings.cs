using System;
using UnityEngine;

/// <summary>로비와 게임 씬이 함께 사용하는 음량 설정 저장소.</summary>
public static class AudioSettings
{
    private const string MasterKey = "audio.master";
    private const string BgmKey = "audio.bgm";
    private const string SfxKey = "audio.sfx";

    public static float Master { get; private set; } = 1f;
    public static float Bgm { get; private set; } = 1f;
    public static float Sfx { get; private set; } = 1f;
    public static event Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Load()
    {
        Master = PlayerPrefs.GetFloat(MasterKey, 1f);
        Bgm = PlayerPrefs.GetFloat(BgmKey, 1f);
        Sfx = PlayerPrefs.GetFloat(SfxKey, 1f);
        ApplyMaster();
    }

    public static void Set(float master, float bgm, float sfx)
    {
        Master = Mathf.Clamp01(master);
        Bgm = Mathf.Clamp01(bgm);
        Sfx = Mathf.Clamp01(sfx);
        PlayerPrefs.SetFloat(MasterKey, Master);
        PlayerPrefs.SetFloat(BgmKey, Bgm);
        PlayerPrefs.SetFloat(SfxKey, Sfx);
        PlayerPrefs.Save();
        ApplyMaster();

        // 사운드 담당 연결 지점:
        // SoundManager/AudioMixer가 추가되면 Changed를 구독해 BGM과 SFX Mixer 파라미터를 각각 적용한다.
        Changed?.Invoke();
    }

    private static void ApplyMaster() => AudioListener.volume = Master;
}
