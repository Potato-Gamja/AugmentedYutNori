using UnityEngine;
using UnityEngine.SceneManagement;

// BGM·SFX 재생 담당. 씬이 바뀌어도 유지된다 (DontDestroyOnLoad)
//
// - Resources/SoundLibrary 에셋이 있으면 게임이 시작될 때 자동으로 만들어진다 → 씬에 따로 배치할 필요 없음
// - 씬이 열리면 SoundLibrary에 지정된 씬 BGM을 재생한다
// - 음량은 AudioSettings(설정 창)를 따른다. Master는 AudioSettings가 AudioListener로 처리하고,
//   BGM·SFX는 여기서 믹서 파라미터(없으면 AudioSource 음량)로 적용한다
//
// 사용: SoundManager.Play(SoundId.YutThrow);   (SoundManager가 없어도 에러 없이 무시)
public class SoundManager : MonoBehaviour
{
    private const string LibraryResourcePath = "SoundLibrary";
    private const float MinDb = -80f;

    public static SoundManager Instance { get; private set; }

    [SerializeField] private SoundLibrarySO library;

    private AudioSource bgmSource;
    private AudioSource sfxSource;
    private SoundId currentBgm = SoundId.None;
    private float currentBgmClipVolume = 1f;
    private bool initialized;

    // 어느 씬에서 시작해도 사운드가 동작하도록 자동 생성
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;

        SoundLibrarySO loaded = Resources.Load<SoundLibrarySO>(LibraryResourcePath);
        if (loaded == null)
        {
            Debug.LogWarning("[Sound] Resources/SoundLibrary 에셋이 없어 사운드를 재생하지 않습니다.");
            return;
        }

        var go = new GameObject(nameof(SoundManager));
        go.AddComponent<SoundManager>().Initialize(loaded);
    }

    // ── 어디서든 부르는 짧은 함수 ──
    public static void Play(SoundId id) => Instance?.PlaySfx(id);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);   // 씬에 직접 배치한 것이 이미 있으면 하나만 남긴다
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (library != null) Initialize(library);   // 씬에 직접 배치하고 Inspector에서 연결한 경우
    }

    private void Initialize(SoundLibrarySO soundLibrary)
    {
        if (initialized) return;
        initialized = true;
        library = soundLibrary;

        bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.loop = true;
        bgmSource.playOnAwake = false;
        bgmSource.outputAudioMixerGroup = library.BgmGroup;

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.outputAudioMixerGroup = library.SfxGroup;

        AudioSettings.Changed += ApplyVolume;
        SceneManager.sceneLoaded += OnSceneLoaded;

        ApplyVolume();
        PlaySceneBgm(SceneManager.GetActiveScene().name);
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        AudioSettings.Changed -= ApplyVolume;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single) PlaySceneBgm(scene.name);
    }

    private void PlaySceneBgm(string sceneName)
    {
        SoundId bgm = library.GetSceneBgm(sceneName);
        if (bgm == SoundId.None) StopBgm();
        else PlayBgm(bgm);
    }

    // ── BGM ──

    public void PlayBgm(SoundId id)
    {
        if (!initialized) return;
        if (id == currentBgm && bgmSource.isPlaying) return;   // 같은 곡이면 이어서 재생

        SoundLibrarySO.SoundEntry entry = library.Find(id);
        if (entry == null)
        {
            StopBgm();
            return;
        }

        currentBgm = id;
        currentBgmClipVolume = entry.volume;
        bgmSource.clip = entry.clip;
        ApplyVolume();
        bgmSource.Play();
    }

    public void StopBgm()
    {
        if (!initialized) return;
        currentBgm = SoundId.None;
        bgmSource.Stop();
        bgmSource.clip = null;
    }

    // ── SFX ──

    public void PlaySfx(SoundId id)
    {
        if (!initialized) return;
        SoundLibrarySO.SoundEntry entry = library.Find(id);
        if (entry == null) return;

        // 믹서가 없으면 설정 음량을 직접 곱한다
        float volume = library.Mixer != null ? entry.volume : entry.volume * AudioSettings.Sfx;
        sfxSource.PlayOneShot(entry.clip, volume);
    }

    // ── 음량 ──

    private void ApplyVolume()
    {
        if (!initialized) return;

        if (library.Mixer != null)
        {
            library.Mixer.SetFloat(library.BgmVolumeParam, ToDecibel(AudioSettings.Bgm));
            library.Mixer.SetFloat(library.SfxVolumeParam, ToDecibel(AudioSettings.Sfx));
            bgmSource.volume = currentBgmClipVolume;
        }
        else
        {
            bgmSource.volume = currentBgmClipVolume * AudioSettings.Bgm;
        }
    }

    // 슬라이더 0~1 → 믹서 dB. 0이면 log10(0)이 -무한대가 되므로 -80dB(무음)로 막는다
    private static float ToDecibel(float volume)
    {
        return volume <= 0.0001f ? MinDb : Mathf.Log10(volume) * 20f;
    }
}
