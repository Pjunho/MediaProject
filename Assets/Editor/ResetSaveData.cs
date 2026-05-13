using UnityEditor;
using UnityEngine;

public static class ResetSaveData
{
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
