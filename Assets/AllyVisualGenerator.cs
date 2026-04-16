using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 아군 캐릭터 비주얼을 절차적으로 생성한다.
/// 128×128 텍스처 3종(Side/Front/Back)을 런타임에 그린 뒤
/// 파츠별로 잘라 월드 오브젝트와 초상화를 만든다.
/// </summary>
public static class AllyVisualGenerator
{
    // ── 방향 열거형 ─────────────────────────────────────────────────
    public enum CharDirection { Side, Front, Back }

    // ── 파츠 이름 상수 ──────────────────────────────────────────────
    public const string PartBackLeg  = "BackLeg";
    public const string PartFrontLeg = "FrontLeg";
    public const string PartBackArm  = "BackArm";
    public const string PartFrontArm = "FrontArm";
    public const string PartTorso    = "Torso";
    public const string PartHead     = "Head";
    public const string PartBody     = "Body";

    const float WorldHeightUnits = 1.18f;
    const int   TexSize          = 128;
    const string WarriorControllerResourcePath = "Allies/WarriorAnimations/Warrior";
    const string WarriorSheetResourcePath = "Allies/warrior_choco_sheet";

    // ── 내부 구조체 ─────────────────────────────────────────────────
    struct PartSpec
    {
        public string  name;
        public Rect    rect;
        public Vector2 pivot;
        public int     orderOffset;
        public PartSpec(string n, Rect r, Vector2 p, int o)
        { name = n; rect = r; pivot = p; orderOffset = o; }
    }

    struct VisualSpec
    {
        public Rect       portraitCrop;
        public Color      fallbackColor;
        public PartSpec[] parts;
        public VisualSpec(Rect c, Color f, PartSpec[] p)
        { portraitCrop = c; fallbackColor = f; parts = p; }
    }

    struct PixelProfile
    {
        public string prefix;
        public float animationSpeedMultiplier;
        public PixelProfile(string prefix, float animationSpeedMultiplier = 1f)
        {
            this.prefix = prefix;
            this.animationSpeedMultiplier = animationSpeedMultiplier;
        }
    }

    // ── 공통 파츠 레이아웃 (128×128 정규화) ─────────────────────────
    static readonly PartSpec[] CommonParts = new[]
    {
        new PartSpec(PartBackLeg,  new Rect(0.23f, 0.04f, 0.13f, 0.22f), new Vector2(0.50f, 0.95f), 0),
        new PartSpec(PartFrontLeg, new Rect(0.41f, 0.04f, 0.13f, 0.22f), new Vector2(0.50f, 0.95f), 2),
        new PartSpec(PartBackArm,  new Rect(0.11f, 0.27f, 0.13f, 0.19f), new Vector2(0.85f, 0.85f), 1),
        new PartSpec(PartFrontArm, new Rect(0.55f, 0.20f, 0.22f, 0.31f), new Vector2(0.10f, 0.78f), 5),
        new PartSpec(PartTorso,    new Rect(0.23f, 0.27f, 0.32f, 0.19f), new Vector2(0.50f, 0.50f), 3),
        new PartSpec(PartHead,     new Rect(0.20f, 0.42f, 0.44f, 0.53f), new Vector2(0.50f, 0.16f), 4),
    };

    static readonly Dictionary<AllyType, VisualSpec> Specs = new()
    {
        { AllyType.Warrior, new VisualSpec(new Rect(0.12f,0.44f,0.76f,0.50f), new Color(0.72f,0.12f,0.12f), CommonParts) },
        { AllyType.Archer,  new VisualSpec(new Rect(0.12f,0.44f,0.76f,0.50f), new Color(0.12f,0.50f,0.16f), CommonParts) },
        { AllyType.Mage,    new VisualSpec(new Rect(0.12f,0.44f,0.76f,0.50f), new Color(0.14f,0.20f,0.72f), CommonParts) },
        { AllyType.Cleric,  new VisualSpec(new Rect(0.12f,0.44f,0.76f,0.50f), new Color(0.86f,0.80f,0.55f), CommonParts) },
    };

    static readonly Dictionary<AllyType, PixelProfile> PixelProfiles = new()
    {
        { AllyType.Warrior, new PixelProfile("kin1") },
        { AllyType.Archer,  new PixelProfile("thf1") },
        { AllyType.Mage,    new PixelProfile("wmg1") },
        { AllyType.Cleric,  new PixelProfile("pdn1") },
    };

    // ── 캐시 ────────────────────────────────────────────────────────
    static readonly Dictionary<AllyType, Texture2D>                 textureCache  = new();
    static readonly Dictionary<string,  Sprite>                     spriteCache   = new();
    static readonly Dictionary<AllyType, Sprite>                    portraitCache = new();
    static readonly Dictionary<(AllyType,CharDirection), Texture2D> dirTexCache   = new();
    static readonly Dictionary<string,  Sprite>                     dirSprCache   = new();
    static readonly Dictionary<string,  Sprite>                     warriorSheetSpriteCache = new();
    static readonly Dictionary<string,  Texture2D>                  pixelTextureCache = new();
    static readonly Dictionary<string,  Sprite>                     pixelSpriteCache = new();
    static RuntimeAnimatorController warriorController;

    // ════════════════════════════════════════════════════════════════
    //   공개 API
    // ════════════════════════════════════════════════════════════════

    public static Sprite CreatePortraitSprite(AllyType type)
    {
        if (PixelProfiles.TryGetValue(type, out var pixelProfile))
        {
            if (portraitCache.TryGetValue(type, out var pixelPortrait) && pixelPortrait != null) return pixelPortrait;
            return portraitCache[type] = LoadPixelSprite($"{pixelProfile.prefix}_fr1", 20f, new Vector2(0.5f, 0.12f));
        }

        if (type == AllyType.Warrior)
        {
            if (portraitCache.TryGetValue(type, out var warriorPortrait) && warriorPortrait != null) return warriorPortrait;

            var portraitTexture = Resources.Load<Texture2D>("Allies/warrior_portrait_clean");
            if (portraitTexture != null)
            {
                return portraitCache[type] = Sprite.Create(
                    portraitTexture,
                    new Rect(120, 170, 770, 700),
                    new Vector2(0.5f, 0.5f),
                    420f);
            }
        }

        if (portraitCache.TryGetValue(type, out var cached) && cached != null) return cached;
        Texture2D tex  = LoadTexture(type);
        RectInt   r    = ToPixelRect(tex, Specs[type].portraitCrop);
        return portraitCache[type] = CropSprite(tex, r, new Vector2(0.5f,0.5f), tex.height);
    }

    public static bool BuildCharacterVisual(AllyType type, Transform parent, int sortingBaseOrder)
    {
        if (PixelProfiles.ContainsKey(type))
            return BuildPixelCharacterVisual(type, parent, sortingBaseOrder);

        if (type == AllyType.Warrior)
            return BuildWarriorAnimatedVisual(parent, sortingBaseOrder);

        Texture2D tex    = LoadTexture(type);
        float     ppu    = tex.height / WorldHeightUnits;
        float     worldW = WorldHeightUnits * tex.width / tex.height;

        foreach (var part in Specs[type].parts)
        {
            var go = new GameObject(part.name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = CalcLocalPos(part.rect, worldW);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = sortingBaseOrder + part.orderOffset;
            sr.sprite       = GetPartSprite(type, part, ppu);
        }
        return true;
    }

    /// <summary>Walk animation frames — multi-part 시스템에서는 사용하지 않으므로 null 반환.</summary>
    public static Sprite[] GetWalkFrames(AllyType type, AllyFacingDirection dir)
    {
        if (PixelProfiles.TryGetValue(type, out var pixelProfile))
        {
            string dirCode = dir switch
            {
                AllyFacingDirection.Down => "fr",
                AllyFacingDirection.Up => "bk",
                AllyFacingDirection.Left => "lf",
                AllyFacingDirection.Right => "rt",
                _ => "fr"
            };

            return new[]
            {
                LoadPixelSprite($"{pixelProfile.prefix}_{dirCode}1", 28f, new Vector2(0.5f, 0.08f)),
                LoadPixelSprite($"{pixelProfile.prefix}_{dirCode}2", 28f, new Vector2(0.5f, 0.08f)),
            };
        }

        return null;
    }

    /// <summary>이미 생성된 Visual 파츠의 스프라이트를 지정 방향으로 교체한다.</summary>
    public static void ApplyDirectionSprites(AllyType type, Transform visualParent, CharDirection dir)
    {
        if (type == AllyType.Warrior || PixelProfiles.ContainsKey(type))
            return;

        float     ppu = TexSize / WorldHeightUnits;
        Texture2D tex = dir == CharDirection.Side ? LoadTexture(type) : GetDirTexture(type, dir);

        foreach (var part in CommonParts)
        {
            Transform go = visualParent.Find(part.name);
            if (go == null) continue;
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null) continue;

            string key = $"{type}_{dir}_{part.name}";
            if (!dirSprCache.TryGetValue(key, out var spr) || spr == null)
                dirSprCache[key] = spr = CropSprite(tex, ToPixelRect(tex, part.rect), part.pivot, ppu);
            sr.sprite = spr;
        }
    }

    static bool BuildPixelCharacterVisual(AllyType type, Transform parent, int sortingBaseOrder)
    {
        if (!PixelProfiles.TryGetValue(type, out var pixelProfile))
            return false;

        Sprite[] downFrames = GetWalkFrames(type, AllyFacingDirection.Down);
        if (downFrames == null || downFrames.Length == 0 || downFrames[0] == null)
            return false;

        var body = new GameObject(PartBody);
        body.transform.SetParent(parent, false);
        body.transform.localScale = Vector3.one * 1.02f;

        var sr = body.AddComponent<SpriteRenderer>();
        sr.sortingOrder = sortingBaseOrder;
        sr.sprite = downFrames[0];

        var driver = parent.gameObject.AddComponent<AllyDirectionalSprite>();
        driver.Initialize(type, sr, pixelProfile.animationSpeedMultiplier);
        return true;
    }

    static bool BuildWarriorAnimatedVisual(Transform parent, int sortingBaseOrder)
    {
        var body = new GameObject(PartBody);
        body.transform.SetParent(parent, false);
        body.transform.localScale = Vector3.one * 0.76f;

        var sr = body.AddComponent<SpriteRenderer>();
        sr.sortingOrder = sortingBaseOrder;
        sr.sprite = LoadWarriorSheetSprite("warrior_choco_sheet_5") ?? LoadWarriorSheetSprite("warrior_choco_sheet_1");
        if (sr.sprite == null)
            return false;

        var animator = body.AddComponent<Animator>();
        animator.runtimeAnimatorController = LoadWarriorController();

        var driver = parent.gameObject.AddComponent<AllyDirectionalAnimator>();
        driver.Initialize(animator);
        return animator.runtimeAnimatorController != null;
    }

    static RuntimeAnimatorController LoadWarriorController()
    {
        if (warriorController != null)
            return warriorController;

        warriorController = Resources.Load<RuntimeAnimatorController>(WarriorControllerResourcePath);
        return warriorController;
    }

    static Sprite LoadWarriorSheetSprite(string spriteName)
    {
        if (warriorSheetSpriteCache.TryGetValue(spriteName, out var cached) && cached != null)
            return cached;

        foreach (var sprite in Resources.LoadAll<Sprite>(WarriorSheetResourcePath))
        {
            warriorSheetSpriteCache[sprite.name] = sprite;
        }

        warriorSheetSpriteCache.TryGetValue(spriteName, out var found);
        return found;
    }

    static Sprite LoadPixelSprite(string resourceName, float ppu, Vector2 pivot)
    {
        string key = $"{resourceName}_{ppu}_{pivot.x}_{pivot.y}";
        if (pixelSpriteCache.TryGetValue(key, out var cached) && cached != null)
            return cached;

        Texture2D texture = LoadPixelTexture(resourceName);
        if (texture == null)
            return null;

        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        var sprite = Sprite.Create(
            texture,
            new Rect(0, 0, texture.width, texture.height),
            pivot,
            ppu);

        pixelSpriteCache[key] = sprite;
        return sprite;
    }

    static Texture2D LoadPixelTexture(string resourceName)
    {
        string path = $"Allies/pixel_allies/{resourceName}";
        if (pixelTextureCache.TryGetValue(path, out var cached) && cached != null)
            return cached;

        Texture2D texture = Resources.Load<Texture2D>(path);
        pixelTextureCache[path] = texture;
        return texture;
    }

    // ════════════════════════════════════════════════════════════════
    //   내부 관리
    // ════════════════════════════════════════════════════════════════

    static Texture2D LoadTexture(AllyType type)
    {
        if (textureCache.TryGetValue(type, out var c) && c != null) return c;
        return textureCache[type] = BuildProceduralTexture(type);
    }

    static Sprite GetPartSprite(AllyType type, PartSpec part, float ppu)
    {
        string key = $"{type}_{part.name}";
        if (spriteCache.TryGetValue(key, out var c) && c != null) return c;
        return spriteCache[key] = CropSprite(LoadTexture(type), ToPixelRect(LoadTexture(type), part.rect), part.pivot, ppu);
    }

    static Texture2D GetDirTexture(AllyType type, CharDirection dir)
    {
        var key = (type, dir);
        if (dirTexCache.TryGetValue(key, out var c) && c != null) return c;

        var tex = NewTex(TexSize, TexSize);
        bool front = dir == CharDirection.Front;
        switch (type)
        {
            case AllyType.Warrior: if (front) DrawWarriorFront(tex); else DrawWarriorBack(tex); break;
            case AllyType.Archer:  if (front) DrawArcherFront(tex);  else DrawArcherBack(tex);  break;
            case AllyType.Mage:    if (front) DrawMageFront(tex);    else DrawMageBack(tex);    break;
            case AllyType.Cleric:  if (front) DrawClericFront(tex);  else DrawClericBack(tex);  break;
        }
        tex.Apply();
        return dirTexCache[key] = tex;
    }

    static RectInt ToPixelRect(Texture2D tex, Rect n)
    {
        int x = Mathf.Clamp(Mathf.RoundToInt(tex.width  * n.x),     0, tex.width  - 1);
        int y = Mathf.Clamp(Mathf.RoundToInt(tex.height * n.y),     0, tex.height - 1);
        int w = Mathf.Clamp(Mathf.RoundToInt(tex.width  * n.width),  1, tex.width  - x);
        int h = Mathf.Clamp(Mathf.RoundToInt(tex.height * n.height), 1, tex.height - y);
        return new RectInt(x, y, w, h);
    }

    static Sprite CropSprite(Texture2D src, RectInt r, Vector2 pivot, float ppu)
    {
        var crop = new Texture2D(r.width, r.height, TextureFormat.RGBA32, false);
        crop.filterMode = FilterMode.Bilinear;
        crop.wrapMode   = TextureWrapMode.Clamp;
        crop.SetPixels(src.GetPixels(r.x, r.y, r.width, r.height));
        crop.Apply();
        return Sprite.Create(crop, new Rect(0,0,r.width,r.height), pivot, ppu);
    }

    static Vector3 CalcLocalPos(Rect n, float worldW)
    {
        return new Vector3(
            (n.x + n.width  * 0.5f - 0.5f) * worldW,
            (n.y + n.height * 0.5f - 0.5f) * WorldHeightUnits,
            0f);
    }

    // ════════════════════════════════════════════════════════════════
    //   Side View 텍스처 (좌/우 이동)
    // ════════════════════════════════════════════════════════════════

    static Texture2D BuildProceduralTexture(AllyType type)
    {
        var tex = NewTex(TexSize, TexSize);
        switch (type)
        {
            case AllyType.Warrior: DrawWarrior(tex); break;
            case AllyType.Archer:  DrawArcher(tex);  break;
            case AllyType.Mage:    DrawMage(tex);    break;
            case AllyType.Cleric:  DrawCleric(tex);  break;
        }
        tex.Apply();
        return tex;
    }

    // ── 전사 Side ──────────────────────────────────────────────────
    static void DrawWarrior(Texture2D t)
    {
        Color white = Color.white;
        Color red   = new Color(0.80f, 0.10f, 0.10f);
        Color dred  = new Color(0.55f, 0.06f, 0.06f);
        Color gold  = new Color(0.96f, 0.78f, 0.14f);
        Color silv  = new Color(0.80f, 0.82f, 0.90f);
        Color dark  = new Color(0.08f, 0.08f, 0.08f);
        Color boot  = new Color(0.26f, 0.18f, 0.08f);

        // 두 다리
        DrawRect(t, 30, 5, 16, 28, white); DrawRect(t, 52, 5, 16, 28, white);
        DrawRect(t, 30, 5, 16, 10, boot);  DrawRect(t, 52, 5, 16, 10, boot);

        // 몸통 — 빨간 갑옷
        DrawRect(t, 26, 34, 52, 24, red);
        DrawRect(t, 28, 36, 48, 20, new Color(0.88f, 0.14f, 0.14f));
        DrawRect(t, 30, 44, 44,  5, gold);  // 허리띠

        // 뒤팔 — 방패 [14-30]
        DrawRect(t, 14, 34, 16, 24, white);
        DrawRect(t,  4, 28, 24, 32, gold);
        DrawRect(t,  7, 31, 18, 26, red);
        DrawRect(t, 12, 40,  8, 10, gold);  // 마름모

        // 앞팔 — 검 [70-98]
        DrawRect(t, 70, 34, 14, 24, white);
        DrawRect(t, 88, 22,  5, 44, silv);  // 검날
        DrawRect(t, 83, 42, 15,  5, gold);  // 가드
        DrawRect(t, 89, 17,  4,  5, silv);  // 검 끝

        // 머리
        DrawCircle(t, 55, 72, 19, white);
        DrawRect(t, 45, 69, 6, 6, dark);
        DrawRect(t, 57, 69, 6, 6, dark);

        // 투구 — 빨강+금 가로 줄무늬
        DrawCircle(t, 55, 88, 19, red);
        DrawRect(t, 36, 76, 38, 14, red);
        DrawRect(t, 36, 93, 38,  3, gold);
        DrawRect(t, 36, 88, 38,  3, gold);
        DrawRect(t, 36, 83, 38,  3, gold);
        DrawRect(t, 36, 78, 38,  4, gold);  // 하단 밴드
        DrawRect(t, 50, 80,  8,  5, gold);  // 전면 장식
        DrawHorns(t, gold);
    }

    static void DrawHorns(Texture2D t, Color gold)
    {
        for (int i = 0; i < 24; i++)
        {
            int hw = Mathf.Max(2, 8 - i / 3);
            DrawRect(t, 30 - i * 2 / 3, 78 + i, hw, 1, gold);
            DrawRect(t, 80 + i * 2 / 3, 78 + i, hw, 1, gold);
        }
    }

    // ── 궁수 Side ──────────────────────────────────────────────────
    static void DrawArcher(Texture2D t)
    {
        Color white  = Color.white;
        Color green  = new Color(0.16f, 0.56f, 0.18f);
        Color dgreen = new Color(0.06f, 0.30f, 0.08f);
        Color brown  = new Color(0.54f, 0.34f, 0.10f);
        Color tan    = new Color(0.74f, 0.60f, 0.28f);
        Color dark   = new Color(0.08f, 0.08f, 0.08f);
        Color boot   = new Color(0.28f, 0.18f, 0.08f);

        DrawRect(t, 30, 5, 16, 28, white); DrawRect(t, 52, 5, 16, 28, white);
        DrawRect(t, 30, 5, 16, 10, boot);  DrawRect(t, 52, 5, 16, 10, boot);

        // 몸통 — 가죽 하네스
        DrawRect(t, 26, 34, 52, 24, white);
        DrawRect(t, 28, 34, 48, 24, brown);
        DrawRect(t, 31, 36, 42, 20, new Color(0.60f, 0.38f, 0.14f));
        DrawRect(t, 36, 34,  5, 24, tan);  DrawRect(t, 56, 34,  5, 24, tan);
        DrawRect(t, 36, 44, 25,  4, tan);

        // 뒤팔 — 화살통 [14-30]
        DrawRect(t, 14, 34, 16, 24, white);
        DrawRect(t,  7, 28, 15, 30, brown);
        DrawRect(t,  9, 54, 11,  6, tan);
        for (int i = 0; i < 5; i++) DrawRect(t, 10 + i * 2, 55, 2, 12, tan);

        // 앞팔 — 활 [70-98]
        DrawRect(t, 70, 34, 14, 24, white);
        DrawRect(t, 86, 22,  5, 44, brown);
        SetPx(t, 84, 22, brown); SetPx(t, 91, 22, brown);
        SetPx(t, 84, 66, brown); SetPx(t, 91, 66, brown);
        for (int y = 23; y < 66; y++) SetPx(t, 93, y, tan);
        DrawRect(t, 87, 42, 10, 2, tan);

        // 머리 + 후드
        DrawCircle(t, 55, 72, 19, white);
        DrawRect(t, 45, 69, 6, 6, dark); DrawRect(t, 57, 69, 6, 6, dark);
        DrawCircle(t, 55, 90, 21, dgreen);
        DrawRect(t, 34, 74, 42, 18, dgreen);
        DrawCircle(t, 55, 82, 17, green);
        DrawRect(t, 34, 74, 42,  5, tan);   // 테두리
        for (int i = 0; i < 16; i++)        // 후드 꼬리
            DrawRect(t, 72 + i, 82 + i, Mathf.Max(4, 22 - i * 2), 1, dgreen);
    }

    // ── 마법사 Side ─────────────────────────────────────────────────
    static void DrawMage(Texture2D t)
    {
        Color white = Color.white;
        Color blue  = new Color(0.12f, 0.18f, 0.82f);
        Color lblue = new Color(0.26f, 0.40f, 0.94f);
        Color gold  = new Color(0.96f, 0.82f, 0.14f);
        Color lgold = new Color(1.00f, 0.94f, 0.36f);
        Color staff = new Color(0.52f, 0.34f, 0.08f);
        Color orb   = new Color(0.95f, 0.12f, 0.12f);
        Color dark  = new Color(0.08f, 0.08f, 0.08f);

        DrawRect(t, 30, 5, 16, 28, white); DrawRect(t, 52, 5, 16, 28, white);
        DrawRect(t, 24, 34, 56, 24, white);
        DrawRect(t, 26, 36, 52, 20, new Color(0.93f, 0.93f, 0.96f));
        DrawRect(t, 14, 34, 16, 24, white);     // 뒤 소매
        DrawRect(t, 70, 34, 14, 24, white);
        DrawRect(t, 88,  6,  4, 62, staff);
        DrawCircle(t, 90, 68, 11, orb);
        DrawCircle(t, 90, 68,  7, new Color(1.0f, 0.30f, 0.30f));
        DrawCircle(t, 88, 70,  3, new Color(1.0f, 0.78f, 0.78f));

        // 머리 + 넓은 챙 마법사 모자 (왼쪽 기울어짐 = 사이드뷰)
        DrawCircle(t, 55, 72, 19, white);
        DrawRect(t, 45, 69, 6, 6, dark); DrawRect(t, 57, 69, 6, 6, dark);
        DrawRect(t, 24, 76, 66, 7, blue);
        DrawRect(t, 22, 77, 70, 4, lblue);
        DrawRect(t, 34, 83, 44, 6, gold);
        DrawRect(t, 36, 83, 40, 4, lgold);
        for (int row = 0; row < 44; row++)
        {
            int hw = Mathf.Max(1, 18 - row * 2 / 5);
            int cx = 50 - row / 7;
            DrawRect(t, cx - hw, 89 + row, hw * 2, 1, blue);
            DrawRect(t, cx - hw, 89 + row, 3, 1, lblue);
        }
        DrawRect(t, 44, 88, 14, 5, gold);
        DrawStar(t, 49, 126, gold);
    }

    static void DrawStar(Texture2D t, int cx, int cy, Color c)
    {
        for (int dx = -2; dx <= 2; dx++) SetPx(t, cx+dx, cy,    c);
        for (int dy = -2; dy <= 2; dy++) SetPx(t, cx,    cy+dy, c);
        SetPx(t,cx-1,cy-1,c); SetPx(t,cx+1,cy-1,c);
        SetPx(t,cx-1,cy+1,c); SetPx(t,cx+1,cy+1,c);
    }

    // ── 성직자 Side ─────────────────────────────────────────────────
    static void DrawCleric(Texture2D t)
    {
        Color white = Color.white;
        Color ivory = new Color(0.93f, 0.93f, 0.95f);
        Color silv  = new Color(0.82f, 0.84f, 0.90f);
        Color gold  = new Color(0.94f, 0.78f, 0.12f);
        Color lgold = new Color(1.00f, 0.94f, 0.44f);
        Color mace  = new Color(0.62f, 0.44f, 0.14f);
        Color gem   = new Color(0.16f, 0.54f, 0.98f);
        Color dark  = new Color(0.08f, 0.08f, 0.08f);
        Color shoe  = new Color(0.80f, 0.80f, 0.84f);

        DrawRect(t, 30, 5, 16, 28, white); DrawRect(t, 52, 5, 16, 28, white);
        DrawRect(t, 30, 5, 16, 10, shoe);  DrawRect(t, 52, 5, 16, 10, shoe);
        DrawRect(t, 24, 34, 56, 24, white);
        DrawRect(t, 26, 36, 52, 20, ivory);
        DrawRect(t, 34, 36, 30, 18, gold);
        DrawRect(t, 37, 38, 24, 14, ivory);
        DrawRect(t, 42, 36, 14,  4, gold);
        DrawRect(t, 14, 34, 16, 24, white); DrawRect(t, 15, 36, 14, 20, ivory);
        DrawRect(t, 70, 34, 14, 24, white);
        DrawRect(t, 88, 10,  4, 56, mace);
        DrawRect(t, 81, 58, 18, 16, silv);
        DrawCircle(t, 90, 66, 8, gem);
        DrawCircle(t, 90, 66, 5, new Color(0.50f, 0.78f, 1.0f));
        DrawClericHead(t, dark, gold, lgold, silv, white, true);
    }

    static void DrawClericHead(Texture2D t, Color dark, Color gold, Color lgold,
                                Color silv, Color white, bool showEyes)
    {
        DrawCircle(t, 55, 72, 19, white);
        if (showEyes)
        {
            DrawRect(t, 45, 69, 6, 6, dark);
            DrawRect(t, 57, 69, 6, 6, dark);
        }
        DrawRect(t, 34, 78, 38, 6, gold);
        DrawRect(t, 35, 78, 36, 4, lgold);
        for (int row = 0; row < 32; row++)
        {
            int hw = Mathf.Max(1, 12 - row * 12 / 32);
            DrawRect(t, 42 - hw, 84 + row, hw * 2, 1, silv);
            DrawRect(t, 65 - hw, 84 + row, hw * 2, 1, silv);
            if (hw > 2)
            {
                DrawRect(t, 42 - hw, 84 + row, 2, 1, white);
                DrawRect(t, 65 - hw, 84 + row, 2, 1, white);
            }
        }
        DrawRect(t, 53, 84,  6, 12, new Color(0.80f, 0.82f, 0.86f));
        DrawRect(t, 52, 96,  9,  2, gold);
        DrawRect(t, 55, 90,  2, 10, gold);
        DrawRect(t, 34, 84, 38,  4, lgold);
    }

    // ════════════════════════════════════════════════════════════════
    //   Front View (아래/DOWN)
    // ════════════════════════════════════════════════════════════════

    static void DrawWarriorFront(Texture2D t)
    {
        Color white = Color.white;
        Color red   = new Color(0.80f, 0.10f, 0.10f);
        Color gold  = new Color(0.96f, 0.78f, 0.14f);
        Color silv  = new Color(0.80f, 0.82f, 0.90f);
        Color dark  = new Color(0.08f, 0.08f, 0.08f);
        Color boot  = new Color(0.26f, 0.18f, 0.08f);

        DrawRect(t, 30, 5, 16, 28, white); DrawRect(t, 52, 5, 16, 28, white);
        DrawRect(t, 30, 5, 16, 10, boot);  DrawRect(t, 52, 5, 16, 10, boot);
        DrawRect(t, 24, 34, 56, 24, red);
        DrawRect(t, 26, 36, 52, 20, new Color(0.88f, 0.14f, 0.14f));
        DrawRect(t, 28, 44, 48,  5, gold);
        DrawRect(t, 14, 34, 16, 24, white);
        DrawRect(t,  3, 26, 24, 34, gold);
        DrawRect(t,  6, 29, 18, 28, red);
        DrawRect(t, 11, 38,  8, 10, gold);
        DrawRect(t, 70, 34, 14, 24, white);
        DrawRect(t, 88, 22,  5, 44, silv);
        DrawRect(t, 83, 42, 15,  5, gold);
        DrawRect(t, 89, 17,  4,  5, silv);
        DrawCircle(t, 55, 72, 19, white);
        DrawRect(t, 43, 69, 7, 7, dark);   // 왼눈
        DrawRect(t, 59, 69, 7, 7, dark);   // 오른눈
        DrawCircle(t, 55, 88, 19, red);
        DrawRect(t, 36, 76, 38, 14, red);
        DrawRect(t, 36, 93, 38,  3, gold);
        DrawRect(t, 36, 88, 38,  3, gold);
        DrawRect(t, 36, 83, 38,  3, gold);
        DrawRect(t, 36, 78, 38,  4, gold);
        DrawRect(t, 50, 80,  8,  5, gold);
        DrawHorns(t, gold);
    }

    static void DrawWarriorBack(Texture2D t)
    {
        Color white = Color.white;
        Color red   = new Color(0.80f, 0.10f, 0.10f);
        Color dred  = new Color(0.50f, 0.05f, 0.05f);
        Color gold  = new Color(0.96f, 0.78f, 0.14f);
        Color silv  = new Color(0.80f, 0.82f, 0.90f);
        Color boot  = new Color(0.26f, 0.18f, 0.08f);

        DrawRect(t, 30, 5, 16, 28, white); DrawRect(t, 52, 5, 16, 28, white);
        DrawRect(t, 30, 5, 16, 10, boot);  DrawRect(t, 52, 5, 16, 10, boot);
        DrawRect(t, 24, 34, 56, 24, red);
        DrawRect(t, 26, 36, 52, 20, new Color(0.88f, 0.14f, 0.14f));
        DrawRect(t, 28, 44, 48,  5, gold);
        DrawRect(t, 51, 36,  6, 20, dred); // 등 중앙선
        DrawRect(t, 14, 34, 16, 24, white);
        DrawRect(t,  4, 28, 20, 30, gold);
        DrawRect(t,  7, 31, 14, 24, dred);
        DrawRect(t, 70, 34, 14, 24, white);
        DrawRect(t, 88, 24,  4, 38, silv);
        // 머리 뒤 — 눈 없음
        DrawCircle(t, 55, 72, 19, white);
        DrawCircle(t, 55, 88, 19, red);
        DrawRect(t, 36, 76, 38, 14, red);
        DrawRect(t, 36, 93, 38,  3, gold);
        DrawRect(t, 36, 88, 38,  3, gold);
        DrawRect(t, 36, 83, 38,  3, gold);
        DrawRect(t, 36, 78, 38,  4, gold);
        DrawHorns(t, gold);
    }

    static void DrawArcherFront(Texture2D t)
    {
        Color white  = Color.white;
        Color green  = new Color(0.16f, 0.56f, 0.18f);
        Color dgreen = new Color(0.06f, 0.30f, 0.08f);
        Color brown  = new Color(0.54f, 0.34f, 0.10f);
        Color tan    = new Color(0.74f, 0.60f, 0.28f);
        Color dark   = new Color(0.08f, 0.08f, 0.08f);
        Color boot   = new Color(0.28f, 0.18f, 0.08f);

        DrawRect(t, 30, 5, 16, 28, white); DrawRect(t, 52, 5, 16, 28, white);
        DrawRect(t, 30, 5, 16, 10, boot);  DrawRect(t, 52, 5, 16, 10, boot);
        DrawRect(t, 24, 34, 56, 24, white);
        DrawRect(t, 26, 34, 52, 24, brown);
        DrawRect(t, 29, 36, 46, 20, new Color(0.60f, 0.38f, 0.14f));
        DrawRect(t, 35, 34,  5, 24, tan); DrawRect(t, 57, 34, 5, 24, tan);
        DrawRect(t, 35, 44, 28,  4, tan);
        for (int i = 0; i < 18; i++)    // X 형 끈
        { SetPx(t, 39+i, 34+i, tan); SetPx(t, 57-i, 34+i, tan); }
        DrawRect(t, 14, 34, 16, 24, white);
        DrawRect(t,  6, 28, 16, 32, brown);
        DrawRect(t,  8, 56, 12,  6, tan);
        for (int i = 0; i < 5; i++) DrawRect(t, 9+i*2, 57, 2, 12, tan);
        DrawRect(t, 70, 34, 14, 24, white);
        DrawRect(t, 86, 22,  5, 44, brown);
        for (int y = 23; y < 66; y++) SetPx(t, 93, y, tan);
        DrawRect(t, 87, 42, 10, 2, tan);
        DrawCircle(t, 55, 72, 19, white);
        DrawRect(t, 43, 69, 7, 7, dark);
        DrawRect(t, 59, 69, 7, 7, dark);
        DrawCircle(t, 55, 90, 21, dgreen);
        DrawRect(t, 34, 74, 42, 18, dgreen);
        DrawCircle(t, 55, 82, 17, green);
        DrawRect(t, 34, 74, 42,  5, tan);
    }

    static void DrawArcherBack(Texture2D t)
    {
        Color white  = Color.white;
        Color green  = new Color(0.16f, 0.56f, 0.18f);
        Color dgreen = new Color(0.06f, 0.30f, 0.08f);
        Color brown  = new Color(0.54f, 0.34f, 0.10f);
        Color tan    = new Color(0.74f, 0.60f, 0.28f);
        Color boot   = new Color(0.28f, 0.18f, 0.08f);

        DrawRect(t, 30, 5, 16, 28, white); DrawRect(t, 52, 5, 16, 28, white);
        DrawRect(t, 30, 5, 16, 10, boot);  DrawRect(t, 52, 5, 16, 10, boot);
        DrawRect(t, 24, 34, 56, 24, white);
        DrawRect(t, 26, 34, 52, 24, new Color(0.58f, 0.38f, 0.14f));
        // 등에 화살통 크게
        DrawRect(t, 38, 24, 20, 38, brown);
        DrawRect(t, 40, 58, 16,  8, tan);
        for (int i = 0; i < 6; i++) DrawRect(t, 41+i*2, 59, 2, 14, tan);
        DrawRect(t, 14, 34, 16, 24, dgreen);
        DrawRect(t, 70, 34, 14, 24, dgreen);
        DrawRect(t, 87, 22,  5, 36, brown);
        DrawCircle(t, 55, 72, 19, white);
        DrawCircle(t, 55, 90, 21, dgreen);
        DrawRect(t, 34, 74, 42, 18, dgreen);
        DrawRect(t, 34, 74, 42,  5, tan);
        for (int i = 0; i < 20; i++)    // 후드 뒤 꼬리
            DrawRect(t, 55+i, 80+i, Mathf.Max(4, 24-i*2), 1, dgreen);
    }

    static void DrawMageFront(Texture2D t)
    {
        Color white = Color.white;
        Color blue  = new Color(0.12f, 0.18f, 0.82f);
        Color lblue = new Color(0.26f, 0.40f, 0.94f);
        Color gold  = new Color(0.96f, 0.82f, 0.14f);
        Color lgold = new Color(1.00f, 0.94f, 0.36f);
        Color staff = new Color(0.52f, 0.34f, 0.08f);
        Color orb   = new Color(0.95f, 0.12f, 0.12f);
        Color dark  = new Color(0.08f, 0.08f, 0.08f);

        DrawRect(t, 30, 5, 16, 28, white); DrawRect(t, 52, 5, 16, 28, white);
        DrawRect(t, 24, 34, 56, 24, white);
        DrawRect(t, 26, 36, 52, 20, new Color(0.93f, 0.93f, 0.96f));
        DrawRect(t, 14, 34, 16, 24, white);
        DrawRect(t, 70, 34, 14, 24, white);
        DrawRect(t, 88,  6,  4, 62, staff);
        DrawCircle(t, 90, 68, 11, orb);
        DrawCircle(t, 90, 68,  7, new Color(1.0f, 0.30f, 0.30f));
        DrawCircle(t, 88, 70,  3, new Color(1.0f, 0.78f, 0.78f));
        DrawCircle(t, 55, 72, 19, white);
        DrawRect(t, 43, 69, 7, 7, dark);
        DrawRect(t, 59, 69, 7, 7, dark);
        DrawRect(t, 24, 76, 66, 7, blue);
        DrawRect(t, 22, 77, 70, 4, lblue);
        DrawRect(t, 34, 83, 44, 6, gold);
        DrawRect(t, 36, 83, 40, 4, lgold);
        // 정면 모자 (기울지 않음)
        for (int row = 0; row < 44; row++)
        {
            int hw = Mathf.Max(1, 18 - row * 2 / 5);
            DrawRect(t, 55-hw, 89+row, hw*2, 1, blue);
            DrawRect(t, 55-hw, 89+row, 3, 1, lblue);
        }
        DrawRect(t, 44, 88, 14, 5, gold);
        DrawStar(t, 55, 126, gold);
    }

    static void DrawMageBack(Texture2D t)
    {
        Color white = Color.white;
        Color blue  = new Color(0.12f, 0.18f, 0.82f);
        Color lblue = new Color(0.26f, 0.40f, 0.94f);
        Color gold  = new Color(0.96f, 0.82f, 0.14f);
        Color lgold = new Color(1.00f, 0.94f, 0.36f);
        Color staff = new Color(0.52f, 0.34f, 0.08f);
        Color orb   = new Color(0.95f, 0.12f, 0.12f);

        DrawRect(t, 30, 5, 16, 28, white); DrawRect(t, 52, 5, 16, 28, white);
        DrawRect(t, 24, 34, 56, 24, white);
        DrawRect(t, 26, 36, 52, 20, new Color(0.93f, 0.93f, 0.96f));
        DrawRect(t, 50, 36,  6, 20, new Color(0.88f, 0.88f, 0.92f)); // 등 중앙선
        DrawRect(t, 14, 34, 16, 24, white);
        DrawRect(t, 70, 34, 14, 24, white);
        DrawRect(t, 88,  6,  4, 62, staff);
        DrawCircle(t, 90, 68, 11, orb);
        DrawCircle(t, 90, 68,  7, new Color(1.0f, 0.30f, 0.30f));
        DrawCircle(t, 55, 72, 19, white);   // 눈 없음
        DrawRect(t, 24, 76, 66, 7, blue);
        DrawRect(t, 22, 77, 70, 4, lblue);
        DrawRect(t, 34, 83, 44, 6, gold);
        DrawRect(t, 36, 83, 40, 4, lgold);
        // 뒤에서 반대로 기울어짐
        for (int row = 0; row < 44; row++)
        {
            int hw = Mathf.Max(1, 18 - row * 2 / 5);
            int cx = 58 + row / 7;
            DrawRect(t, cx-hw, 89+row, hw*2, 1, blue);
            DrawRect(t, cx+hw-3, 89+row, 3, 1, lblue);
        }
        DrawRect(t, 44, 88, 14, 5, gold);
        DrawStar(t, 61, 126, gold);
    }

    static void DrawClericFront(Texture2D t)
    {
        Color white = Color.white;
        Color ivory = new Color(0.93f, 0.93f, 0.95f);
        Color silv  = new Color(0.82f, 0.84f, 0.90f);
        Color gold  = new Color(0.94f, 0.78f, 0.12f);
        Color lgold = new Color(1.00f, 0.94f, 0.44f);
        Color mace  = new Color(0.62f, 0.44f, 0.14f);
        Color gem   = new Color(0.16f, 0.54f, 0.98f);
        Color dark  = new Color(0.08f, 0.08f, 0.08f);
        Color shoe  = new Color(0.80f, 0.80f, 0.84f);

        DrawRect(t, 30, 5, 16, 28, white); DrawRect(t, 52, 5, 16, 28, white);
        DrawRect(t, 30, 5, 16, 10, shoe);  DrawRect(t, 52, 5, 16, 10, shoe);
        DrawRect(t, 24, 34, 56, 24, white);
        DrawRect(t, 26, 36, 52, 20, ivory);
        DrawRect(t, 34, 36, 30, 18, gold);
        DrawRect(t, 37, 38, 24, 14, ivory);
        DrawRect(t, 42, 36, 14,  4, gold);
        DrawRect(t, 14, 34, 16, 24, white); DrawRect(t, 15, 36, 14, 20, ivory);
        DrawRect(t, 70, 34, 14, 24, white);
        DrawRect(t, 88, 10,  4, 56, mace);
        DrawRect(t, 81, 58, 18, 16, silv);
        DrawCircle(t, 90, 66, 8, gem);
        DrawCircle(t, 90, 66, 5, new Color(0.50f, 0.78f, 1.0f));
        DrawClericHead(t, dark, gold, lgold, silv, white, true);
    }

    static void DrawClericBack(Texture2D t)
    {
        Color white = Color.white;
        Color ivory = new Color(0.93f, 0.93f, 0.95f);
        Color silv  = new Color(0.82f, 0.84f, 0.90f);
        Color gold  = new Color(0.94f, 0.78f, 0.12f);
        Color lgold = new Color(1.00f, 0.94f, 0.44f);
        Color mace  = new Color(0.62f, 0.44f, 0.14f);
        Color gem   = new Color(0.16f, 0.54f, 0.98f);
        Color shoe  = new Color(0.80f, 0.80f, 0.84f);
        Color dark  = Color.clear; // 눈 없음

        DrawRect(t, 30, 5, 16, 28, white); DrawRect(t, 52, 5, 16, 28, white);
        DrawRect(t, 30, 5, 16, 10, shoe);  DrawRect(t, 52, 5, 16, 10, shoe);
        DrawRect(t, 24, 34, 56, 24, white);
        DrawRect(t, 26, 36, 52, 20, ivory);
        DrawRect(t, 50, 36,  6, 20, gold); // 등 세로 십자가
        DrawRect(t, 40, 44, 26,  5, gold); // 등 가로 십자가
        DrawRect(t, 14, 34, 16, 24, white); DrawRect(t, 15, 36, 14, 20, ivory);
        DrawRect(t, 70, 34, 14, 24, white);
        DrawRect(t, 88, 10,  4, 56, mace);
        DrawRect(t, 81, 58, 18, 16, silv);
        DrawCircle(t, 90, 66, 8, gem);
        DrawCircle(t, 90, 66, 5, new Color(0.50f, 0.78f, 1.0f));
        DrawClericHead(t, dark, gold, lgold, silv, white, false);
    }

    // ════════════════════════════════════════════════════════════════
    //   픽셀 드로우 유틸
    // ════════════════════════════════════════════════════════════════

    static Texture2D NewTex(int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode   = TextureWrapMode.Clamp;
        var clear = new Color(0,0,0,0);
        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
            tex.SetPixel(x, y, clear);
        return tex;
    }

    static void DrawRect(Texture2D t, int x, int y, int w, int h, Color c)
    {
        for (int px = x; px < x+w; px++)
        for (int py = y; py < y+h; py++)
            SetPx(t, px, py, c);
    }

    static void DrawCircle(Texture2D t, int cx, int cy, int r, Color c)
    {
        for (int x = cx-r; x <= cx+r; x++)
        for (int y = cy-r; y <= cy+r; y++)
            if ((x-cx)*(x-cx)+(y-cy)*(y-cy) <= r*r)
                SetPx(t, x, y, c);
    }

    static void SetPx(Texture2D t, int x, int y, Color c)
    {
        if (x >= 0 && x < t.width && y >= 0 && y < t.height)
            t.SetPixel(x, y, c);
    }
}
