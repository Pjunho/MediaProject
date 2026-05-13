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

    const float BGM_VOLUME = 0.34f;
    const float SFX_VOLUME = 0.60f;
    const float FADE_SECONDS = 0.65f;

    AudioSource bgmSource;
    AudioSource sfxSource;
    Coroutine fadeRoutine;
    string currentBgmPath = "";

    AudioClip[] uiClickClips;
    AudioClip confirmClip;
    AudioClip waveClearClip;
    AudioClip failClip;

    // ── 전투 SFX ──────────────────────────────────────────────────────────
    AudioClip[] hitDamageClips;
    AudioClip[] allyDeathClips;
    AudioClip[] bluntSwingClips;
    AudioClip[] arrowShootClips;
    AudioClip[] shieldBlockClips;
    AudioClip   sniperShotClip;

    // ── 스킬 SFX ──────────────────────────────────────────────────────────
    AudioClip healClip;
    AudioClip smokeBombClip;
    AudioClip paralysisClip;
    AudioClip paralysisShootClip;
    AudioClip barrierClip;

    // ── UI SFX (파일 기반) ─────────────────────────────────────────────────
    AudioClip uiSelectClip;
    AudioClip uiBackClip;
    AudioClip uiErrorClip;
    AudioClip uiOpenClip;
    AudioClip uiCloseClip;
    AudioClip uiToggleClip;

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

        // 씬 전환과 무관하게 항상 오디오 출력을 보장하는 영구 AudioListener
        gameObject.AddComponent<AudioListener>();

        bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.loop = true;
        bgmSource.playOnAwake = false;
        bgmSource.volume = 0f;

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.loop = false;
        sfxSource.playOnAwake = false;
        sfxSource.volume = SFX_VOLUME;

        LoadSfxAssets();

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        if (Instance == this)
            SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 씬 카메라 등에 붙은 중복 AudioListener 비활성화 (GameAudio 것만 유지)
        foreach (var al in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
        {
            if (al.gameObject != gameObject)
                al.enabled = false;
        }

        if (scene.name == "MediaProject")
        {
            int stage = StageManager.Instance != null ? StageManager.Instance.currentStageIndex : 1;
            PlayStageBgm(stage);
        }
        else if (scene.name == "MainMenu" || scene.name == "StageSelect")
        {
            PlayMenuBgm();
        }
    }

    public static void PlayUiClick()   => Ensure().PlayRandom(Ensure().uiClickClips, 0.85f);
    public static void PlayConfirm()   => Ensure().PlaySfx(Ensure().confirmClip,   0.95f);
    public static void PlayWaveClear() => Ensure().PlaySfx(Ensure().waveClearClip, 1.00f);
    public static void PlayFail()      => Ensure().PlaySfx(Ensure().failClip,      1.00f);
    public static void StopBgm()          => Ensure().FadeOutBgm();
    public static void SetBgmPitch(float pitch) => Ensure().bgmSource.pitch = pitch;

    // ── 전투 SFX ──────────────────────────────────────────────────────────
    public static void PlayHitDamage()  => Ensure().PlayRandom(Ensure().hitDamageClips,  0.90f);
    public static void PlayAllyDeath()  => Ensure().PlayRandom(Ensure().allyDeathClips,  1.00f);
    public static void PlayBluntSwing() => Ensure().PlayRandom(Ensure().bluntSwingClips, 0.85f);
    public static void PlayArrowShoot() => Ensure().PlayRandom(Ensure().arrowShootClips, 0.90f);
    public static void PlayShieldBlock()=> Ensure().PlayRandom(Ensure().shieldBlockClips, 1.00f);
    public static void PlaySniperShot() => Ensure().PlaySfx(Ensure().sniperShotClip,    0.80f);

    // ── 스킬 SFX ──────────────────────────────────────────────────────────
    public static void PlayHeal()      => Ensure().PlaySfx(Ensure().healClip,      0.80f);
    public static void PlaySmokeBomb() => Ensure().PlaySfx(Ensure().smokeBombClip, 0.90f);
    public static void PlayParalysis()      => Ensure().PlaySfx(Ensure().paralysisClip,      0.65f);
    public static void PlayParalysisShoot() => Ensure().PlaySfx(Ensure().paralysisShootClip, 0.80f);
    public static void PlayBarrier()   => Ensure().PlaySfx(Ensure().barrierClip,   0.85f);

    // ── UI SFX (파일 기반) ─────────────────────────────────────────────────
    public static void PlayUiSelect()  => Ensure().PlaySfx(Ensure().uiSelectClip, 0.90f);
    public static void PlayUiBack()    => Ensure().PlaySfx(Ensure().uiBackClip,   0.90f);
    public static void PlayUiError()   => Ensure().PlaySfx(Ensure().uiErrorClip,  0.90f);
    public static void PlayUiOpen()    => Ensure().PlaySfx(Ensure().uiOpenClip,   0.80f);
    public static void PlayUiClose()   => Ensure().PlaySfx(Ensure().uiCloseClip,  0.80f);
    public static void PlayUiToggle()  => Ensure().PlaySfx(Ensure().uiToggleClip, 0.85f);

    public void PlayMenuBgm()
    {
        PlayBgm("Audio/BGM/main_menu_peaceful");
    }

    public void PlayStageBgm(int stageIndex)
    {
        string path = GetStageBgmPath(stageIndex);
        if (string.IsNullOrEmpty(path))
        {
            FadeOutBgm();
            return;
        }

        PlayBgm(path);
    }

    void PlayBgm(string path)
    {
        if (currentBgmPath == path)
        {
            // 같은 BGM이 요청됨 — 재시작 금지
            // Unity 씬 전환 직후 isPlaying이 순간적으로 false가 될 수 있으므로
            // fadeRoutine이 없고 실제로 멈춰 있을 때만 무음 복구
            if (!bgmSource.isPlaying && fadeRoutine == null)
            {
                bgmSource.volume = BGM_VOLUME;
                bgmSource.Play();
            }
            return;
        }

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

    void PlayRandom(AudioClip[] clips, float volumeScale)
    {
        if (clips == null || clips.Length == 0) return;
        PlaySfx(clips[UnityEngine.Random.Range(0, clips.Length)], volumeScale);
    }

    void LoadSfxAssets()
    {
        hitDamageClips  = new AudioClip[]
        {
            Resources.Load<AudioClip>("Audio/SFX/kenney_impact-sounds/Audio/impactGeneric_light_000"),
            Resources.Load<AudioClip>("Audio/SFX/kenney_impact-sounds/Audio/impactGeneric_light_001"),
            Resources.Load<AudioClip>("Audio/SFX/kenney_impact-sounds/Audio/impactGeneric_light_002"),
            Resources.Load<AudioClip>("Audio/SFX/kenney_impact-sounds/Audio/impactGeneric_light_003"),
            Resources.Load<AudioClip>("Audio/SFX/kenney_impact-sounds/Audio/impactGeneric_light_004"),
        };
        allyDeathClips  = LoadClipArray("Audio/SFX/Combat/ally_death",  2);
        bluntSwingClips = LoadClipArray("Audio/SFX/Combat/blunt_swing", 3);
        arrowShootClips = LoadClipArray("Audio/SFX/Combat/arrow_shoot", 2);
        shieldBlockClips = new AudioClip[]
        {
            Resources.Load<AudioClip>("Audio/SFX/kenney_impact-sounds/Audio/impactPlate_heavy_000"),
            Resources.Load<AudioClip>("Audio/SFX/kenney_impact-sounds/Audio/impactPlate_heavy_001"),
            Resources.Load<AudioClip>("Audio/SFX/kenney_impact-sounds/Audio/impactPlate_heavy_002"),
            Resources.Load<AudioClip>("Audio/SFX/kenney_impact-sounds/Audio/impactPlate_heavy_003"),
            Resources.Load<AudioClip>("Audio/SFX/kenney_impact-sounds/Audio/impactPlate_heavy_004"),
        };
        sniperShotClip  = Resources.Load<AudioClip>("Audio/SFX/Combat/sniper_shot");

        healClip      = Resources.Load<AudioClip>("Audio/SFX/Skill/sound_healing");
        smokeBombClip = Resources.Load<AudioClip>("Audio/SFX/Skill/sound_smoke");
        paralysisClip      = Retrofy(Resources.Load<AudioClip>("Audio/SFX/Skill/sound_paralized"), 4, 4);
        paralysisShootClip = Resources.Load<AudioClip>("Audio/SFX/Combat/arrow_shoot_1");
        barrierClip   = Resources.Load<AudioClip>("Audio/SFX/Skill/sound_forcefield");

        uiClickClips = new AudioClip[]
        {
            Resources.Load<AudioClip>("Audio/SFX/kenney_ui-audio/Audio/click1"),
            Resources.Load<AudioClip>("Audio/SFX/kenney_ui-audio/Audio/click2"),
            Resources.Load<AudioClip>("Audio/SFX/kenney_ui-audio/Audio/click3"),
            Resources.Load<AudioClip>("Audio/SFX/kenney_ui-audio/Audio/click4"),
            Resources.Load<AudioClip>("Audio/SFX/kenney_ui-audio/Audio/click5"),
        };
        confirmClip   = Resources.Load<AudioClip>("Audio/SFX/kenney_interface-sounds/Audio/confirmation_001");
        waveClearClip = Resources.Load<AudioClip>("Audio/SFX/kenney_digital-audio/Audio/phaserUp5");
        failClip      = Resources.Load<AudioClip>("Audio/SFX/kenney_digital-audio/Audio/lowDown");

        uiSelectClip = Resources.Load<AudioClip>("Audio/SFX/UI/ui_select");
        uiBackClip   = Resources.Load<AudioClip>("Audio/SFX/UI/ui_back");
        uiErrorClip  = Resources.Load<AudioClip>("Audio/SFX/UI/ui_error");
        uiOpenClip   = Resources.Load<AudioClip>("Audio/SFX/kenney_interface-sounds/Audio/open_001");
        uiCloseClip  = Resources.Load<AudioClip>("Audio/SFX/kenney_interface-sounds/Audio/close_001");
        uiToggleClip = Resources.Load<AudioClip>("Audio/SFX/kenney_interface-sounds/Audio/toggle_001");
    }

    static AudioClip[] LoadClipArray(string basePath, int count)
    {
        var clips = new AudioClip[count];
        for (int i = 0; i < count; i++)
            clips[i] = Resources.Load<AudioClip>($"{basePath}_{i + 1}");
        return clips;
    }

    static string GetStageBgmPath(int stageIndex) => stageIndex switch
    {
        1 => "Audio/BGM/stage_grass_peaceful",
        2 => "Audio/BGM/stage_cave_dark",
        3 => "Audio/BGM/stage_lava_boss",
        _ => ""
    };


    // 비트 크러싱 + 샘플 홀드로 레트로(8비트) 느낌 생성
    static AudioClip Retrofy(AudioClip source, int bits = 4, int sampleRateDiv = 4)
    {
        if (source == null) return null;
        int total = source.samples * source.channels;
        var data  = new float[total];
        source.GetData(data, 0);

        // 샘플 홀드: sampleRateDiv 샘플마다 값 고정 (저해상도 DAC 시뮬레이션)
        float held = 0f;
        for (int i = 0; i < total; i++)
        {
            if (i % sampleRateDiv == 0) held = data[i];
            data[i] = held;
        }

        // 비트 크러싱: bits 비트 해상도로 양자화
        float steps = Mathf.Pow(2f, bits) - 1f;
        for (int i = 0; i < total; i++)
            data[i] = Mathf.Round(data[i] * steps) / steps;

        var clip = AudioClip.Create(source.name + "_retro", source.samples, source.channels, source.frequency, false);
        clip.SetData(data, 0);
        return clip;
    }
}
