using UnityEngine;
using System.Collections;

/// <summary>
/// 아군 사망 시 연기 이펙트 (절차적 생성)
/// </summary>
public class SmokeEffect : MonoBehaviour
{
    public static void Spawn(Vector3 position)
    {
        GameObject go = new GameObject("SmokeEffect");
        go.transform.position = position;
        go.AddComponent<SmokeEffect>().Play();
    }

    void Play()
    {
        StartCoroutine(Animate());
    }

    IEnumerator Animate()
    {
        int particleCount = 8;
        SmokeParticle[] particles = new SmokeParticle[particleCount];

        for (int i = 0; i < particleCount; i++)
        {
            GameObject p = new GameObject($"Smoke_{i}");
            p.transform.SetParent(transform, false);
            particles[i] = p.AddComponent<SmokeParticle>();
            particles[i].Init(i, particleCount);
        }

        // 전체 이펙트 지속 시간
        yield return new WaitForSeconds(1.2f);
        Destroy(gameObject);
    }
}

/// <summary>
/// 연기 파티클 하나
/// </summary>
public class SmokeParticle : MonoBehaviour
{
    private SpriteRenderer sr;
    private Vector3 velocity;
    private float lifetime;
    private float elapsed;
    private float startSize;
    private Color smokeColor;

    public void Init(int index, int total)
    {
        sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite       = CreateCircleSprite();
        sr.sortingOrder = 30;

        // 각도를 고르게 분산 + 랜덤 변화
        float angle     = ((float)index / total) * Mathf.PI * 2f + Random.Range(-0.3f, 0.3f);
        float speed     = Random.Range(0.8f, 2.2f);
        velocity        = new Vector3(Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed + 1.5f, 0f);

        lifetime        = Random.Range(0.6f, 1.1f);
        elapsed         = 0f;
        startSize       = Random.Range(0.15f, 0.35f);
        transform.localScale = Vector3.one * startSize;

        // 연기 색상: 회색 ~ 진한 회색
        float gray      = Random.Range(0.3f, 0.65f);
        smokeColor      = new Color(gray, gray, gray, 0.85f);
        sr.color        = smokeColor;

        StartCoroutine(Animate());
    }

    IEnumerator Animate()
    {
        while (elapsed < lifetime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / lifetime;

            // 위로 올라가면서 퍼짐
            transform.position += velocity * Time.deltaTime;
            velocity            = Vector3.Lerp(velocity, Vector3.up * 0.5f, Time.deltaTime * 2f);

            // 크기: 처음엔 커졌다가 사라질수록 작아짐
            float sizeCurve    = Mathf.Sin(t * Mathf.PI);
            transform.localScale = Vector3.one * (startSize * (1f + sizeCurve * 0.8f));

            // 투명도: 후반부에 서서히 사라짐
            float alpha        = t < 0.4f ? 0.85f : Mathf.Lerp(0.85f, 0f, (t - 0.4f) / 0.6f);
            sr.color           = new Color(smokeColor.r, smokeColor.g, smokeColor.b, alpha);

            yield return null;
        }
        Destroy(gameObject);
    }

    // 원형 스프라이트 생성
    Sprite CreateCircleSprite()
    {
        int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        int cx = size / 2, cy = size / 2, r = size / 2;
        for (int x = 0; x < size; x++)
        for (int y = 0; y < size; y++)
        {
            float dx   = x - cx, dy = y - cy;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            // 가장자리를 부드럽게
            float alpha = Mathf.Clamp01(1f - (dist / r));
            alpha = Mathf.Pow(alpha, 0.6f); // 중심이 더 진하게
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
