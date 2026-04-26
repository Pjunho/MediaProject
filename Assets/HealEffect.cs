using UnityEngine;
using System.Collections;

public class HealEffect : MonoBehaviour
{
    public static void Spawn(Vector3 position)
    {
        GameObject go = new GameObject("HealEffect");
        go.transform.position = position;
        go.AddComponent<HealEffect>().Play();
    }

    void Play() => StartCoroutine(Animate());

    IEnumerator Animate()
    {
        int count = 4;
        for (int i = 0; i < count; i++)
        {
            GameObject p = new GameObject($"HealParticle_{i}");
            p.transform.SetParent(transform, false);
            p.AddComponent<HealParticle>().Init();
        }
        yield return new WaitForSeconds(1.0f);
        Destroy(gameObject);
    }
}

public class HealParticle : MonoBehaviour
{
    SpriteRenderer sr;
    Vector3 velocity;
    float lifetime;
    float elapsed;
    float startSize;

    public void Init()
    {
        sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = CreatePlusSprite();
        sr.sortingOrder = 30;
        sr.color = new Color(0.2f, 0.9f, 0.3f, 0f);

        velocity = new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(0.8f, 1.6f), 0f);
        lifetime = Random.Range(0.5f, 0.85f);
        elapsed = 0f;
        startSize = Random.Range(0.09f, 0.16f);
        transform.localScale = Vector3.one * startSize;
        transform.localPosition = new Vector3(
            Random.Range(-0.25f, 0.25f),
            Random.Range(-0.15f, 0.1f), 0f);

        StartCoroutine(Animate());
    }

    IEnumerator Animate()
    {
        while (elapsed < lifetime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / lifetime;
            transform.position += velocity * Time.deltaTime;
            velocity = Vector3.Lerp(velocity, Vector3.up * 0.5f, Time.deltaTime * 1.5f);

            float alpha = t < 0.25f
                ? Mathf.Lerp(0f, 0.9f, t / 0.25f)
                : Mathf.Lerp(0.9f, 0f, (t - 0.25f) / 0.75f);
            sr.color = new Color(0.2f, 0.9f, 0.3f, alpha);
            yield return null;
        }
        Destroy(gameObject);
    }

    Sprite CreatePlusSprite()
    {
        int size = 16;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;

        for (int x = 0; x < size; x++)
        for (int y = 0; y < size; y++)
            tex.SetPixel(x, y, Color.clear);

        int cx = size / 2;
        int thickness = 3;
        int barStart = 2;
        int barEnd = size - 2;

        for (int y = barStart; y < barEnd; y++)
        for (int x = cx - thickness / 2; x <= cx + thickness / 2; x++)
            if (x >= 0 && x < size)
                tex.SetPixel(x, y, Color.white);

        for (int x = barStart; x < barEnd; x++)
        for (int y = cx - thickness / 2; y <= cx + thickness / 2; y++)
            if (y >= 0 && y < size)
                tex.SetPixel(x, y, Color.white);

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
