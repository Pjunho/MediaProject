using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 스테이지 BGM과 공통 효과음을 담당하는 전역 오디오 매니저.
/// BGM은 OpenGameArt CC0 음원을 Resources/Audio/BGM에서 로드한다.
/// </summary>
public class GameAudio : MonoBehaviour
{
    public static GameAudio Instance { get; private set; }

    const float BGM_VOLUME = 0.42f;
    const float SFX_VOLUME = 0.75f;
    const float FADE_SECONDS = 0.65f;

    AudioSource bgmSource;
    AudioSource sfxSource;
    Coroutine fadeRoutine;
    string currentBgmPath = "";

    AudioClip uiClickClip;
    AudioClip confirmClip;
    AudioClip waveClearClip;
    AudioClip failClip;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        Ensure();
    }

    public static GameAudio Ensure()
    {
        if (Instance != null)
            return Instance;

        var go = new GameObject("GameAudio");
        return go.AddComponent<GameAudio>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.loop = true;
        bgmSource.playOnAwake = false;
        bgmSource.volume = 0f;

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.loop = false;
        sfxSource.playOnAwake = false;
        sfxSource.volume = SFX_VOLUME;

        uiClickClip = CreateToneClip("UiClick", 660f, 0.045f, 0.12f, Wave.Sine);
        confirmClip = CreateDualToneClip("Confirm", 523f, 784f, 0.14f, 0.18f);
        waveClearClip = CreateDualToneClip("WaveClear", 659f, 988f, 0.24f, 0.18f);
        failClip = CreateDualToneClip("Fail", 220f, 146f, 0.28f, 0.20f);

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        if (Instance == this)
            SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "MediaProject")
        {
            int stage = StageManager.Instance != null ? StageManager.Instance.currentStageIndex : 1;
            PlayStageBgm(stage);
        }
        else if (scene.name == "MainMenu" || scene.name == "StageSelect")
        {
            FadeOutBgm();
        }
    }

    public static void PlayUiClick() => Ensure().PlaySfx(Ensure().uiClickClip, 0.65f);
    public static void PlayConfirm() => Ensure().PlaySfx(Ensure().confirmClip, 0.80f);
    public static void PlayWaveClear() => Ensure().PlaySfx(Ensure().waveClearClip, 0.82f);
    public static void PlayFail() => Ensure().PlaySfx(Ensure().failClip, 0.86f);

    public void PlayStageBgm(int stageIndex)
    {
        string path = GetStageBgmPath(stageIndex);
        if (string.IsNullOrEmpty(path))
        {
            FadeOutBgm();
            return;
        }

        if (currentBgmPath == path && bgmSource.isPlaying)
            return;

        var clip = Resources.Load<AudioClip>(path);
        if (clip == null)
        {
            Debug.LogWarning($"[GameAudio] BGM 로드 실패: Resources/{path}");
            FadeOutBgm();
            return;
        }

        currentBgmPath = path;
        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeToClip(clip));
    }

    void FadeOutBgm()
    {
        if (bgmSource == null || !bgmSource.isPlaying)
            return;

        currentBgmPath = "";
        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeOut());
    }

    IEnumerator FadeToClip(AudioClip clip)
    {
        float startVolume = bgmSource.volume;
        for (float t = 0f; t < FADE_SECONDS; t += Time.unscaledDeltaTime)
        {
            bgmSource.volume = Mathf.Lerp(startVolume, 0f, t / FADE_SECONDS);
            yield return null;
        }

        bgmSource.clip = clip;
        bgmSource.Play();

        for (float t = 0f; t < FADE_SECONDS; t += Time.unscaledDeltaTime)
        {
            bgmSource.volume = Mathf.Lerp(0f, BGM_VOLUME, t / FADE_SECONDS);
            yield return null;
        }

        bgmSource.volume = BGM_VOLUME;
        fadeRoutine = null;
    }

    IEnumerator FadeOut()
    {
        float startVolume = bgmSource.volume;
        for (float t = 0f; t < FADE_SECONDS; t += Time.unscaledDeltaTime)
        {
            bgmSource.volume = Mathf.Lerp(startVolume, 0f, t / FADE_SECONDS);
            yield return null;
        }

        bgmSource.Stop();
        bgmSource.clip = null;
        bgmSource.volume = 0f;
        fadeRoutine = null;
    }

    void PlaySfx(AudioClip clip, float volumeScale)
    {
        if (clip == null || sfxSource == null)
            return;

        sfxSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
    }

    static string GetStageBgmPath(int stageIndex) => stageIndex switch
    {
        1 => "Audio/BGM/stage_grass_peaceful",
        2 => "Audio/BGM/stage_cave_dark",
        3 => "Audio/BGM/stage_lava_boss",
        _ => ""
    };

    enum Wave { Sine, Square }

    static AudioClip CreateDualToneClip(string name, float freqA, float freqB, float seconds, float gain)
    {
        const int sampleRate = 44100;
        int samples = Mathf.CeilToInt(sampleRate * seconds);
        var data = new float[samples];
        int split = Mathf.Max(1, samples / 2);

        for (int i = 0; i < samples; i++)
        {
            float freq = i < split ? freqA : freqB;
            float t = i / (float)sampleRate;
            float env = 1f - i / (float)samples;
            data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * gain * env;
        }

        var clip = AudioClip.Create(name, samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static AudioClip CreateToneClip(string name, float freq, float seconds, float gain, Wave wave)
    {
        const int sampleRate = 44100;
        int samples = Mathf.CeilToInt(sampleRate * seconds);
        var data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)sampleRate;
            float env = 1f - i / (float)samples;
            float sample = Mathf.Sin(2f * Mathf.PI * freq * t);
            if (wave == Wave.Square)
                sample = sample >= 0f ? 1f : -1f;
            data[i] = sample * gain * env;
        }

        var clip = AudioClip.Create(name, samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
