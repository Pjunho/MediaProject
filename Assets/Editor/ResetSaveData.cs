using Microsoft.Win32;
using UnityEditor;
using UnityEngine;

public static class ResetSaveData
{
    // 빌드된 게임의 PlayerPrefs는 Editor와 다른 레지스트리 경로에 저장됨
    // HKCU\Software\[CompanyName]\[ProductName]
    [MenuItem("Tools/Reset All Stage Data (Build)")]
    public static void ResetAllBuild()
    {
        string companyName = PlayerSettings.companyName;
        string productName = PlayerSettings.productName;
        string keyPath = $@"Software\{companyName}\{productName}";

        using (var key = Registry.CurrentUser.OpenSubKey(keyPath, writable: true))
        {
            if (key == null)
            {
                EditorUtility.DisplayDialog("초기화", "빌드 게임의 저장 데이터가 없습니다.", "확인");
                return;
            }

            string[] stageKeys = { "Stage_1_Stars", "Stage_2_Stars", "Stage_3_Stars", "Stage_4_Stars", "Stage_5_Stars",
                                   "Gem_1_Unlocked", "Gem_2_Unlocked", "Gem_3_Unlocked", "Gem_4_Unlocked", "Gem_5_Unlocked",
                                   "Gem_1_Active",   "Gem_2_Active",   "Gem_3_Active",   "Gem_4_Active",   "Gem_5_Active" };
            foreach (var k in stageKeys)
                key.DeleteValue(k, throwOnMissingValue: false);
        }

        Debug.Log($"[Build] 스테이지 데이터 초기화 완료: HKCU\\{keyPath}");
        EditorUtility.DisplayDialog("초기화 완료", "빌드된 게임의 스테이지 클리어 기록이 초기화되었습니다.", "확인");
    }


    [MenuItem("Tools/Reset All Stage Data")]
    public static void ResetAll()
    {
        for (int i = 1; i <= 5; i++)
        {
            PlayerPrefs.DeleteKey("Stage_" + i + "_Stars");
            PlayerPrefs.DeleteKey("Gem_" + i + "_Unlocked");
            PlayerPrefs.DeleteKey("Gem_" + i + "_Active");
        }
        PlayerPrefs.Save();
        Debug.Log("All stage clear data has been reset.");
        EditorUtility.DisplayDialog("초기화 완료", "모든 스테이지 클리어 기록이 초기화되었습니다.", "확인");
    }

    [MenuItem("Tools/Set All Stages 3 Stars")]
    public static void SetAll3Stars()
    {
        for (int i = 1; i <= 5; i++)
        {
            PlayerPrefs.SetInt("Stage_" + i + "_Stars", 3);
            PlayerPrefs.SetInt("Gem_" + i + "_Unlocked", 1);
            PlayerPrefs.SetInt("Gem_" + i + "_Active", 1);
        }
        PlayerPrefs.Save();
        Debug.Log("All stages set to 3 stars.");
        EditorUtility.DisplayDialog("설정 완료", "스테이지 1~5 모두 별 3개 클리어 상태로 설정되었습니다.", "확인");
    }
}
