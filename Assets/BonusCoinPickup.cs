using System.Collections;
using UnityEngine;

/// <summary>
/// 웨이브 중 낮은 확률로 경로 위에 등장하는 보너스 코인.
/// 아군이 가까이 지나가면 코인을 지급하고, 짤랑 소리와 함께 위로 튀며 사라진다.
/// </summary>
public class BonusCoinPickup : MonoBehaviour
{
    const float PICKUP_RADIUS = 0.34f;
    const float CHECK_INTERVAL = 0.05f;

    static Sprite coinSprite;
    static AudioClip jingleClip;

    SpriteRenderer sr;
    bool collected;
    Vector3 basePosition;

    public void Init(Vector3 position)
    {
        transform.position = position;
        basePosition = position;

        sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = GetCoinSprite();
        sr.sortingOrder = 9;
        sr.color = Color.white;

        transform.localScale = Vector3.one * 0.58f;
        StartCoroutine(CheckPickupLoop());
    }

    void Update()
    {
        if (collected) return;

        float bob = Mathf.Sin(Time.time * 5.5f) * 0.055f;
        float pulse = 1f + Mathf.Sin(Time.time * 7.5f) * 0.055f;
        transform.position = basePosition + Vector3.up * bob;
        transform.localScale = Vector3.one * (0.58f * pulse);
    }

    IEnumerator CheckPickupLoop()
    {
        var wait = new WaitForSeconds(CHECK_INTERVAL);
        while (!collected)
        {
            var allies = FindObjectsByType<AllyBase>(FindObjectsSortMode.None);
            for (int i = 0; i < allies.Length; i++)
            {
                AllyBase ally = allies[i];
                if (ally == null || ally.isDead || !ally.gameObject.activeInHierarchy) continue;

                if (Vector2.Distance(transform.position, ally.transform.position) <= PICKUP_RADIUS)
                {
                    Collect();
                    yield break;
                }
            }
            yield return wait;
        }
    }

    void Collect()
    {
        if (collected) return;
        collected = true;

        GameManager.Instance?.CollectBonusCoin(transform.position);
        AudioSource.PlayClipAtPoint(GetJingleClip(), transform.position, 0.85f);
        StartCoroutine(CollectAnimation());
    }

    IEnumerator CollectAnimation()
    {
        float elapsed = 0f;
        const float duration = 0.42f;
        Vector3 start = transform.position;
        Vector3 startScale = transform.localScale;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float hop = Mathf.Sin(t * Mathf.PI) * 0.34f + t * 0.28f;

            transform.position = start + Vector3.up * hop;
            transform.localScale = Vector3.Lerp(startScale * 1.22f, Vector3.zero, t * t);
            if (sr != null)
                sr.color = new Color(1f, 1f, 1f, Mathf.Lerp(1f, 0f, t));

            yield return null;
        }

        Destroy(gameObject);
    }

    static Sprite GetCoinSprite()
    {
        if (coinSprite != null) return coinSprite;

        const int size = 32;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        for (int x = 0; x < size; x++)
        for (int y = 0; y < size; y++)
        {
            float d = Vector2.Distance(new Vector2(x, y), center);
            Color c = Color.clear;

            if (d <= 13f)
            {
                float shade = Mathf.Clamp01(1f - d / 13f);
                c = Color.Lerp(new Color(0.95f, 0.52f, 0.08f), new Color(1f, 0.92f, 0.25f), shade);
                if (d > 10.5f) c = new Color(0.70f, 0.36f, 0.04f);
                if (x >= 10 && x <= 21 && y >= 14 && y <= 17)
                    c = new Color(1f, 0.78f, 0.12f);
                if (x >= 12 && x <= 19 && y >= 19 && y <= 21)
                    c = Color.Lerp(c, Color.white, 0.38f);
            }

            tex.SetPixel(x, y, c);
        }

        tex.Apply();
        coinSprite = Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, size);
        return coinSprite;
    }

    static AudioClip GetJingleClip()
    {
        if (jingleClip != null) return jingleClip;

        const int sampleRate = 44100;
        const float duration = 0.32f;
        int samples = Mathf.CeilToInt(sampleRate * duration);
        var data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            float env = Mathf.Exp(-t * 8.2f);
            float toneA = Mathf.Sin(2f * Mathf.PI * 1046.5f * t);
            float toneB = Mathf.Sin(2f * Mathf.PI * 1568.0f * Mathf.Max(0f, t - 0.035f));
            float toneC = Mathf.Sin(2f * Mathf.PI * 2093.0f * Mathf.Max(0f, t - 0.07f));
            data[i] = (toneA * 0.45f + toneB * 0.32f + toneC * 0.22f) * env * 0.35f;
        }

        jingleClip = AudioClip.Create("BonusCoinJingle", samples, 1, sampleRate, false);
        jingleClip.SetData(data, 0);
        return jingleClip;
    }
}
