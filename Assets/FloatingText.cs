using UnityEngine;
using System.Collections;

/// <summary>
/// 골 도달 시 "+1 코인" 등 플로팅 텍스트를 월드 공간에 표시하는 유틸리티.
/// TextMesh를 이용해 위로 떠오르며 페이드 아웃한다.
/// </summary>
public class FloatingText : MonoBehaviour
{
    public static void Spawn(Vector3 worldPosition, string text, Color color)
    {
        var go = new GameObject("FloatingText");
        go.transform.position = worldPosition;
        go.AddComponent<FloatingText>().Play(text, color);
    }

    void Play(string text, Color color)
    {
        var tm = gameObject.AddComponent<TextMesh>();
        tm.text          = text;
        tm.fontSize      = 32;
        tm.characterSize = 0.09f;
        tm.color         = new Color(color.r, color.g, color.b, 0f);
        tm.alignment     = TextAlignment.Center;
        tm.anchor        = TextAnchor.MiddleCenter;
        tm.fontStyle     = FontStyle.Bold;
        GetComponent<MeshRenderer>().sortingOrder = 50;
        StartCoroutine(Animate(tm, color));
    }

    IEnumerator Animate(TextMesh tm, Color baseColor)
    {
        float elapsed  = 0f;
        float duration = 1.0f;
        Vector3 startPos = transform.position;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            transform.position = startPos + Vector3.up * (t * 1.0f);

            float alpha = t < 0.25f
                ? Mathf.Lerp(0f, 1f, t / 0.25f)
                : Mathf.Lerp(1f, 0f, (t - 0.25f) / 0.75f);
            tm.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);

            yield return null;
        }
        Destroy(gameObject);
    }
}
