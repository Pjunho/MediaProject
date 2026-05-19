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

            // Unity는 레지스트리에 키 이름 뒤에 해시를 붙여 저장 (예: Stage_1_Stars_h904089719)
            // 정확한 이름 대신 접두사로 패턴 매칭하여 삭제
            string[] prefixes = { "Stage_", "Gem_" };
            foreach (string valueName in key.GetValueNames())
            {
                foreach (string prefix in prefixes)
                {
                    if (valueName.StartsWith(prefix))
                    {
                        key.DeleteValue(valueName, throwOnMissingValue: false);
                        break;
                    }
                }
            }
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
