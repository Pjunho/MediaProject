using UnityEngine;

/// <summary>
/// 게임 전역 설정 (볼륨, 전체화면) 저장/적용
/// PlayerPrefs에 저장되어 씬 간 유지됨
/// </summary>
public static class SettingsManager
{
    const string KEY_VOLUME     = "Settings_Volume";
    const string KEY_FULLSCREEN = "Settings_Fullscreen";

    public static float Volume
    {
        get => PlayerPrefs.GetFloat(KEY_VOLUME, 1f);
        set
        {
            float v = Mathf.Round(Mathf.Clamp01(value) * 10f) / 10f;
            PlayerPrefs.SetFloat(KEY_VOLUME, v);
            PlayerPrefs.Save();
            AudioListener.volume = v;
        }
    }

    public static bool IsFullscreen
    {
        get => PlayerPrefs.GetInt(KEY_FULLSCREEN, Screen.fullScreen ? 1 : 0) == 1;
        set
        {
            PlayerPrefs.SetInt(KEY_FULLSCREEN, value ? 1 : 0);
            PlayerPrefs.Save();
            Screen.fullScreen = value;
        }
    }

    /// <summary>씬 로드 시 저장된 설정을 적용</summary>
    public static void Apply()
    {
        AudioListener.volume = Volume;
        if (PlayerPrefs.HasKey(KEY_FULLSCREEN))
            Screen.fullScreen = IsFullscreen;
    }
}
