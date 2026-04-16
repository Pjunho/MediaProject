using UnityEngine;

/// <summary>
/// 적 캐릭터 스프라이트 절차적 생성기
/// </summary>
public static class EnemyVisualGenerator
{
    static readonly System.Collections.Generic.Dictionary<string, Sprite> pixelEnemySpriteCache = new();

    // ── 저격수 (Sniper) - 빨간 망토, 길쭉한 활 ─────────────────────
    public static Sprite CreateSniperSprite()
    {
        Sprite pixel = LoadPixelEnemySprite("scr1_fr1");
        if (pixel != null) return pixel;

        int size = 32;
        Texture2D tex = NewTex(size);

        Color robe    = new Color(0.55f, 0.05f, 0.05f); // 진한 빨강
        Color dark    = new Color(0.30f, 0.02f, 0.02f);
        Color skin    = new Color(0.95f, 0.80f, 0.65f);
        Color bowCol  = new Color(0.20f, 0.20f, 0.20f); // 검은 활

        // 다리
        DrawRect(tex, 12, 2, 3, 7, robe);
        DrawRect(tex, 17, 2, 3, 7, robe);

        // 망토 몸통
        DrawRect(tex, 9, 9, 14, 11, robe);
        DrawRect(tex, 7, 9, 3, 13, dark);   // 왼쪽 망토 자락
        DrawRect(tex, 22, 9, 3, 13, dark);  // 오른쪽 망토 자락

        // 팔
        DrawRect(tex, 5, 10, 5, 4, robe);
        DrawRect(tex, 22, 10, 5, 4, robe);

        // 머리
        DrawCircle(tex, 16, 22, 5, skin);

        // 두건
        DrawRect(tex, 10, 22, 12, 3, dark);
        DrawCircle(tex, 16, 24, 5, dark);

        // 눈 (날카롭게)
        DrawRect(tex, 12, 21, 3, 1, Color.red);
        DrawRect(tex, 17, 21, 3, 1, Color.red);

        // 긴 활 (왼쪽)
        for (int i = 5; i < 25; i++) SetPx(tex, 4, i, bowCol);
        SetPx(tex, 5, 5,  bowCol); SetPx(tex, 5, 24, bowCol);
        SetPx(tex, 6, 6,  bowCol); SetPx(tex, 6, 23, bowCol);
        // 시위
        for (int i = 6; i < 23; i++) SetPx(tex, 7, i, new Color(0.8f, 0.8f, 0.6f));

        tex.Apply();
        return ToSprite(tex);
    }

    // ── 창병 (Spearman) - 초록/금 갑옷, 긴 창 ──────────────────────
    public static Sprite CreateSpearmanSprite()
    {
        Sprite pixel = LoadPixelEnemySprite("nja1_fr1");
        if (pixel != null) return pixel;

        int size = 32;
        Texture2D tex = NewTex(size);

        Color armor  = new Color(0.10f, 0.40f, 0.15f); // 초록 갑옷
        Color gold   = new Color(0.80f, 0.65f, 0.10f); // 금색 장식
        Color skin   = new Color(0.95f, 0.80f, 0.65f);
        Color helmet = new Color(0.08f, 0.30f, 0.10f);
        Color spear  = new Color(0.55f, 0.40f, 0.15f); // 창대
        Color tip    = new Color(0.75f, 0.75f, 0.80f); // 창촉

        // 다리
        DrawRect(tex, 11, 2, 4, 7, armor);
        DrawRect(tex, 17, 2, 4, 7, armor);

        // 몸통
        DrawRect(tex, 9, 9, 14, 10, armor);

        // 금색 흉갑 장식
        DrawRect(tex, 11, 11, 10, 6, gold);
        DrawRect(tex, 13, 12,  6, 4, armor);

        // 팔
        DrawRect(tex, 5,  10, 5, 6, armor);
        DrawRect(tex, 22, 10, 5, 6, armor);

        // 머리
        DrawCircle(tex, 16, 22, 5, skin);
        // 투구
        DrawRect(tex, 10, 22, 12, 4, helmet);
        DrawRect(tex, 14, 26,  4, 2, helmet);
        // 투구 장식 (금색)
        DrawRect(tex, 15, 26,  2, 3, gold);

        // 눈
        DrawRect(tex, 13, 21, 2, 2, Color.black);
        DrawRect(tex, 17, 21, 2, 2, Color.black);

        // 창 (오른쪽에 세로로)
        for (int i = 1; i < 28; i++) SetPx(tex, 26, i, spear);
        SetPx(tex, 25, 27, tip); SetPx(tex, 26, 28, tip); SetPx(tex, 27, 27, tip);
        SetPx(tex, 26, 29, tip); SetPx(tex, 26, 30, tip);

        tex.Apply();
        return ToSprite(tex);
    }

    // ── 근접병 (Brawler) - 검정/주황 헤비 아머, 큰 체형 ───────────
    public static Sprite CreateBrawlerSprite()
    {
        Sprite pixel = LoadPixelEnemySprite("dvl1_fr1");
        if (pixel != null) return pixel;

        int size = 32;
        Texture2D tex = NewTex(size);

        Color heavy  = new Color(0.15f, 0.15f, 0.15f); // 검정 갑옷
        Color orange = new Color(0.90f, 0.45f, 0.05f); // 주황 포인트
        Color skin   = new Color(0.95f, 0.80f, 0.65f);
        Color spike  = new Color(0.60f, 0.60f, 0.65f); // 가시/날

        // 다리 (굵게)
        DrawRect(tex, 10, 2, 5, 7, heavy);
        DrawRect(tex, 17, 2, 5, 7, heavy);

        // 몸통 (넓게)
        DrawRect(tex, 7, 9, 18, 12, heavy);

        // 주황 포인트
        DrawRect(tex, 9,  11, 14, 6, orange);
        DrawRect(tex, 11, 12, 10, 4, heavy);

        // 팔 (두껍게)
        DrawRect(tex, 3,  9, 6, 8, heavy);
        DrawRect(tex, 23, 9, 6, 8, heavy);

        // 주먹
        DrawRect(tex, 3,  17, 6, 5, orange);
        DrawRect(tex, 23, 17, 6, 5, orange);

        // 머리 (크게)
        DrawCircle(tex, 16, 23, 6, skin);

        // 헬멧 (풀페이스)
        DrawRect(tex, 9,  22, 14, 5, heavy);
        DrawRect(tex, 10, 19, 12, 4, heavy);
        // 바이저 슬릿 (주황)
        DrawRect(tex, 11, 22, 10, 2, orange);

        // 어깨 가시
        SetPx(tex, 3, 9, spike); SetPx(tex, 4, 8, spike); SetPx(tex, 5, 7, spike);
        SetPx(tex, 28, 9, spike); SetPx(tex, 27, 8, spike); SetPx(tex, 26, 7, spike);

        tex.Apply();
        return ToSprite(tex);
    }

    // ── 유틸 ───────────────────────────────────────────────────────
    static Texture2D NewTex(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        var clear = new Color(0, 0, 0, 0);
        for (int x = 0; x < size; x++)
        for (int y = 0; y < size; y++)
            tex.SetPixel(x, y, clear);
        return tex;
    }

    static void DrawRect(Texture2D t, int x, int y, int w, int h, Color c)
    {
        for (int px = x; px < x + w; px++)
        for (int py = y; py < y + h; py++)
            SetPx(t, px, py, c);
    }

    static void DrawCircle(Texture2D t, int cx, int cy, int r, Color c)
    {
        for (int x = cx - r; x <= cx + r; x++)
        for (int y = cy - r; y <= cy + r; y++)
            if ((x-cx)*(x-cx)+(y-cy)*(y-cy) <= r*r)
                SetPx(t, x, y, c);
    }

    static void SetPx(Texture2D t, int x, int y, Color c)
    {
        if (x >= 0 && x < t.width && y >= 0 && y < t.height)
            t.SetPixel(x, y, c);
    }

    static Sprite ToSprite(Texture2D tex)
        => Sprite.Create(tex, new Rect(0,0,tex.width,tex.height),
                         new Vector2(0.5f,0.5f), tex.width);

    static Sprite LoadPixelEnemySprite(string resourceName)
    {
        if (pixelEnemySpriteCache.TryGetValue(resourceName, out var cached) && cached != null)
            return cached;

        Texture2D texture = Resources.Load<Texture2D>($"Allies/pixel_enemies/{resourceName}");
        if (texture == null)
            return null;

        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        var sprite = Sprite.Create(
            texture,
            new Rect(0, 0, texture.width, texture.height),
            new Vector2(0.5f, 0.08f),
            28f);

        pixelEnemySpriteCache[resourceName] = sprite;
        return sprite;
    }
}
