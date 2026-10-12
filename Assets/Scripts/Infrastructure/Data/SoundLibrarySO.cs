using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

// 소리 종류(SoundId)별 오디오 파일과 믹서 연결을 모아 둔 에셋
// 만들기: Project 창 우클릭 → Create → YutNori → Sound Library
// 위치: Assets/Resources/SoundLibrary.asset (이 이름·위치여야 SoundManager가 자동으로 찾는다)
[CreateAssetMenu(fileName = "SoundLibrary", menuName = "YutNori/Sound Library")]
public class SoundLibrarySO : ScriptableObject
{
    [Serializable]
    public class SoundEntry
    {
        public SoundId id;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;   // 클립별 기본 음량 (설정 음량과 곱해짐)
    }

    [Serializable]
    public class SceneBgm
    {
        public string sceneName;   // 예: "Lobby", "Game"
        public SoundId bgm;
    }

    [Header("믹서 (비워 두면 AudioSource 음량으로만 조절)")]
    [SerializeField] private AudioMixer mixer;
    [SerializeField] private AudioMixerGroup bgmGroup;
    [SerializeField] private AudioMixerGroup sfxGroup;
    [Tooltip("믹서에서 Expose한 BGM 음량 파라미터 이름")]
    [SerializeField] private string bgmVolumeParam = "BGM";
    [Tooltip("믹서에서 Expose한 SFX 음량 파라미터 이름")]
    [SerializeField] private string sfxVolumeParam = "SFX";

    [Header("소리 목록")]
    [SerializeField] private List<SoundEntry> sounds = new List<SoundEntry>();

    [Header("씬별 BGM (씬이 열리면 자동 재생)")]
    [SerializeField] private List<SceneBgm> sceneBgms = new List<SceneBgm>();

    public AudioMixer Mixer => mixer;
    public AudioMixerGroup BgmGroup => bgmGroup;
    public AudioMixerGroup SfxGroup => sfxGroup;
    public string BgmVolumeParam => bgmVolumeParam;
    public string SfxVolumeParam => sfxVolumeParam;

    // 없으면 null
    public SoundEntry Find(SoundId id)
    {
        if (id == SoundId.None) return null;
        return sounds.Find(entry => entry.id == id && entry.clip != null);
    }

    // 씬에 지정된 BGM. 없으면 None
    public SoundId GetSceneBgm(string sceneName)
    {
        SceneBgm found = sceneBgms.Find(entry => entry.sceneName == sceneName);
        return found != null ? found.bgm : SoundId.None;
    }
}
