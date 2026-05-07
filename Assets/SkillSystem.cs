using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 아군 타입별 스킬 데이터 및 해금 상태 관리.
/// 코인으로 해금 가능하며 스테이지 재시작 시 초기화된다.
/// </summary>
public static class SkillSystem
{
    public struct SkillData
    {
        public AllyType allyType;
        public string   skillName;
        public string   description;
        public int      cost;
        public string   iconResource;
        public float    cooldownSeconds; // 발동 후 재사용 대기 시간
    }

    static readonly SkillData[] skills = new SkillData[]
    {
        new SkillData
        {
            allyType        = AllyType.Warrior,
            skillName       = "불굴의 의지",
            description     = "2초간 모든 공격 피해를\n받지 않습니다.",
            cost            = 3,
            iconResource    = "Icon/skill_knight",
            cooldownSeconds = 15f
        },
        new SkillData
        {
            allyType        = AllyType.Archer,
            skillName       = "마비 화살",
            description     = "적 하나를 지정해 3초간\n공격할 수 없게 만듭니다.",
            cost            = 2,
            iconResource    = "Icon/skill_archer",
            cooldownSeconds = 12f
        },
        new SkillData
        {
            allyType        = AllyType.Mage,
            skillName       = "순간 보호막",
            description     = "1.5초간 주변 아군을\n파란 보호막으로 지킵니다.",
            cost            = 3,
            iconResource    = "Icon/skill_magician",
            cooldownSeconds = 15f
        },
        new SkillData
        {
            allyType        = AllyType.Cleric,
            skillName       = "치유의 빛",
            description     = "5초간 생존 아군을 매초\n최대 HP의 3%만큼 회복합니다.",
            cost            = 4,
            iconResource    = "Icon/skill_priest",
            cooldownSeconds = 22f
        },
        new SkillData
        {
            allyType        = AllyType.Rogue,
            skillName       = "연막탄",
            description     = "5초간 생존 아군의 공격 회피율을\n50%까지 높입니다.",
            cost            = 2,
            iconResource    = "Icon/skill_thief",
            cooldownSeconds = 18f
        },
        new SkillData
        {
            allyType        = AllyType.Paladin,
            skillName       = "수호의 맹세",
            description     = "4초간 아군 피해의 80%를 대신 받고\n초당 최대 HP의 5%를 회복합니다.",
            cost            = 6,
            iconResource    = "Icon/skill_paladin",
            cooldownSeconds = 20f
        }
    };

    static readonly bool[]  unlockedSkills      = new bool[6];
    static readonly float[] skillActivationTimes;   // Time.time 기준 마지막 발동 시각

    static SkillSystem()
    {
        skillActivationTimes = new float[6];
        for (int i = 0; i < skillActivationTimes.Length; i++)
            skillActivationTimes[i] = float.NegativeInfinity;
    }

    static AllyBase  pendingArcher;
    static EnemyBase pendingMouseDownEnemy;   // 마우스 누름 시 대상으로 지정한 적
    static bool      pendingMouseDownStarted;
    static float previousTimeScale = 1f;
    static int targetingStartFrame = -1;
    static GameObject archerAimVisualRoot;

    const float ArcherTargetRange = 6f;
    const float ArcherAimDimWorldRadius = 28f;

    public static SkillData[] GetAllSkills() => skills;
    public static bool IsTargeting => pendingArcher != null;

    public static SkillData GetSkillForAlly(AllyType allyType)
    {
        for (int i = 0; i < skills.Length; i++)
            if (skills[i].allyType == allyType) return skills[i];
        return skills[0];
    }

    public static bool IsUnlocked(AllyType allyType)
    {
        for (int i = 0; i < skills.Length; i++)
            if (skills[i].allyType == allyType) return unlockedSkills[i];
        return false;
    }

    /// <summary>웨이브 종료 시 호출 — 모든 스킬 쿨다운을 즉시 초기화</summary>
    public static void ResetCooldowns()
    {
        for (int i = 0; i < skillActivationTimes.Length; i++)
            skillActivationTimes[i] = float.NegativeInfinity;
    }

    /// <summary>스킬 쿨다운 전체 시간(초)</summary>
    public static float GetCooldownTotal(AllyType allyType)
    {
        for (int i = 0; i < skills.Length; i++)
            if (skills[i].allyType == allyType) return skills[i].cooldownSeconds;
        return 0f;
    }

    /// <summary>스킬 쿨다운 남은 시간(초). 0이면 사용 가능.</summary>
    public static float GetCooldownRemaining(AllyType allyType)
    {
        for (int i = 0; i < skills.Length; i++)
        {
            if (skills[i].allyType != allyType) continue;
            float elapsed = Time.time - skillActivationTimes[i];
            return Mathf.Max(0f, skills[i].cooldownSeconds - elapsed);
        }
        return 0f;
    }

    /// <summary>스테이지 시작/재시작 시 호출 — 모든 스킬 잠금 상태로 초기화</summary>
    public static void ResetForStage()
    {
        if (IsTargeting)
            CancelTargeting();
        pendingMouseDownEnemy = null;
        for (int i = 0; i < unlockedSkills.Length; i++)
        {
            unlockedSkills[i] = false;
            skillActivationTimes[i] = float.NegativeInfinity;
        }
    }

    /// <summary>
    /// 스킬 해금 시도. 성공 시 true 반환 및 costSpent에 차감 코인 수 기록.
    /// 코인 차감은 호출자(GameManager)가 직접 수행한다.
    /// </summary>
    public static bool TryUnlock(AllyType allyType, int availableCoins, out int costSpent)
    {
        costSpent = 0;
        for (int i = 0; i < skills.Length; i++)
        {
            if (skills[i].allyType != allyType) continue;
            if (unlockedSkills[i])              return false;
            if (availableCoins < skills[i].cost) return false;
            unlockedSkills[i] = true;
            costSpent         = skills[i].cost;
            return true;
        }
        return false;
    }

    /// <summary>AllyPlacer에서 스폰 직후 호출. 현재 스킬은 전부 발동형이므로 패시브 적용은 없다.</summary>
    public static void ApplyToAlly(AllyBase ally, AllyType allyType) { }

    public static Sprite GetIconSprite(AllyType allyType)
    {
        var data = GetSkillForAlly(allyType);
        Sprite sprite = Resources.Load<Sprite>(data.iconResource);
        if (sprite != null) return sprite;

        Texture2D tex = Resources.Load<Texture2D>(data.iconResource);
        if (tex == null) return null;
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
    }

    public static bool ActivateSkill(AllyType allyType)
    {
        if (!IsUnlocked(allyType))
        {
            GameManager.Instance?.ShowToast("먼저 스킬을 해금해야 합니다!", new Color(1f, 0.35f, 0.35f));
            return false;
        }

        float cdRemaining = GetCooldownRemaining(allyType);
        if (cdRemaining > 0f)
        {
            GameManager.Instance?.ShowToast(
                $"쿨다운 중입니다! ({Mathf.CeilToInt(cdRemaining)}초)",
                new Color(1f, 0.35f, 0.35f));
            return false;
        }

        AllyBase caster = FindAliveAlly(allyType);
        if (caster == null)
        {
            GameManager.Instance?.ShowToast("전투 중 생존한 아군이 필요합니다!", new Color(1f, 0.65f, 0.25f));
            return false;
        }

        bool activated = false;
        switch (allyType)
        {
            case AllyType.Warrior:
                caster.ActivateWarriorWill();
                GameManager.Instance?.ShowToast("불굴의 의지 발동!", new Color(1f, 0.85f, 0.2f));
                activated = true; break;
            case AllyType.Archer:
                BeginArcherTargeting(caster);
                activated = true; break;
            case AllyType.Mage:
                caster.ActivateMageBarrier();
                GameManager.Instance?.ShowToast("순간 보호막 발동!", new Color(0.45f, 0.75f, 1f));
                activated = true; break;
            case AllyType.Cleric:
                caster.ActivateClericHeal();
                GameManager.Instance?.ShowToast("치유의 빛 발동!", new Color(1f, 0.92f, 0.35f));
                activated = true; break;
            case AllyType.Rogue:
                caster.ActivateRogueSmoke();
                GameManager.Instance?.ShowToast("연막탄 발동!", new Color(0.75f, 0.35f, 1f));
                activated = true; break;
            case AllyType.Paladin:
                caster.ActivatePaladinOath();
                GameManager.Instance?.ShowToast("수호의 맹세 발동!", new Color(1f, 0.88f, 0.35f));
                activated = true; break;
        }

        if (activated)
        {
            // 쿨다운 타이머 기록
            for (int i = 0; i < skills.Length; i++)
                if (skills[i].allyType == allyType)
                { skillActivationTimes[i] = Time.time; break; }
        }
        return activated;
    }

    public static bool HandleTargetingInput(Vector2 screenPos, Mouse mouse)
    {
        if (pendingArcher == null) return false;
        if (pendingArcher.isDead)
        {
            CancelTargeting();
            return true;
        }

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            CancelTargeting();
            GameManager.Instance?.ShowToast("마비 화살 조준 취소", new Color(0.8f, 0.8f, 0.8f));
            return true;
        }

        if (mouse != null && mouse.rightButton.wasPressedThisFrame)
        {
            CancelTargeting();
            GameManager.Instance?.ShowToast("마비 화살 조준 취소", new Color(0.8f, 0.8f, 0.8f));
            return true;
        }

        if (mouse == null) return true;

        // ── 마우스 버튼 눌림: 대상 예약 ─────────────────────────────────
        if (mouse.leftButton.wasPressedThisFrame && Time.frameCount != targetingStartFrame)
        {
            pendingMouseDownStarted = true;
            pendingMouseDownEnemy = FindEnemyAtScreenPosition(screenPos);
            return true;
        }

        // ── 마우스 버튼 뗌: 같은 적 위에서 뗐을 때만 발사 ──────────────
        if (mouse.leftButton.wasReleasedThisFrame)
        {
            EnemyBase downEnemy = pendingMouseDownEnemy;
            pendingMouseDownEnemy = null;
            bool hadMouseDown = pendingMouseDownStarted;
            pendingMouseDownStarted = false;

            if (!hadMouseDown) return true;

            if (downEnemy == null)
            {
                if (FindEnemyAtScreenPosition(screenPos) == null)
                    GameManager.Instance?.ShowToast("마비시킬 적을 클릭하세요!", new Color(1f, 0.65f, 0.25f));
                return true;
            }

            EnemyBase upEnemy = FindEnemyAtScreenPosition(screenPos);
            if (upEnemy == null || upEnemy != downEnemy)
            {
                // 다른 곳에서 뗌 → 발사 취소, 토스트 없이 조준 유지
                return true;
            }

            float dist = Vector3.Distance(pendingArcher.transform.position, downEnemy.transform.position);
            if (dist > ArcherTargetRange)
            {
                GameManager.Instance?.ShowToast("대상이 사거리 밖입니다!", new Color(1f, 0.35f, 0.35f));
                return true;
            }

            pendingArcher.FireParalysisArrow(downEnemy);
            CancelTargeting();
            GameManager.Instance?.ShowToast("마비 화살 적중!", new Color(0.65f, 1f, 0.45f));
            return true;
        }

        return true;
    }

    static void BeginArcherTargeting(AllyBase archer)
    {
        ClearArcherAimVisual();
        pendingArcher = archer;
        targetingStartFrame = Time.frameCount;
        previousTimeScale = Mathf.Max(Time.timeScale, 0.0001f);
        Time.timeScale = 0.1f;
        CreateArcherAimVisual(archer);
        GameManager.Instance?.ShowToast("적을 클릭해 마비 화살을 발사하세요", new Color(0.65f, 1f, 0.45f));
    }

    public static void CancelTargeting()
    {
        ClearArcherAimVisual();
        pendingArcher = null;
        pendingMouseDownEnemy = null;
        pendingMouseDownStarted = false;
        targetingStartFrame = -1;
        Time.timeScale = previousTimeScale;
    }

    static void CreateArcherAimVisual(AllyBase archer)
    {
        if (archer == null) return;

        archerAimVisualRoot = new GameObject("ArcherAimRange");
        archerAimVisualRoot.transform.SetParent(archer.transform, false);
        archerAimVisualRoot.transform.localPosition = Vector3.zero;

        var dimGo = new GameObject("DimOutsideRange");
        dimGo.transform.SetParent(archerAimVisualRoot.transform, false);
        var dimSr = dimGo.AddComponent<SpriteRenderer>();
        dimSr.sprite = CreateRangeDimSprite(ArcherAimDimWorldRadius, ArcherTargetRange);
        dimSr.color = Color.white;
        dimSr.sortingOrder = 54;

        var ringGo = new GameObject("RangeRing");
        ringGo.transform.SetParent(archerAimVisualRoot.transform, false);
        var ring = ringGo.AddComponent<LineRenderer>();
        ring.useWorldSpace = false;
        ring.loop = true;
        ring.positionCount = 96;
        ring.widthMultiplier = 0.08f;
        ring.sortingOrder = 56;
        ring.material = new Material(Shader.Find("Sprites/Default"));
        Color ringColor = new Color(0.58f, 1f, 0.35f, 0.96f);
        ring.startColor = ringColor;
        ring.endColor = ringColor;
        for (int i = 0; i < ring.positionCount; i++)
        {
            float angle = (float)i / ring.positionCount * Mathf.PI * 2f;
            ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * ArcherTargetRange,
                                            Mathf.Sin(angle) * ArcherTargetRange,
                                            0f));
        }
    }

    static void ClearArcherAimVisual()
    {
        if (archerAimVisualRoot == null) return;
        Object.Destroy(archerAimVisualRoot);
        archerAimVisualRoot = null;
    }

    static Sprite CreateRangeDimSprite(float worldRadius, float clearRadius)
    {
        int size = 768;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float center = (size - 1) * 0.5f;
        float pixelsPerWorld = center / worldRadius;
        float clearPixels = clearRadius * pixelsPerWorld;
        float feather = Mathf.Max(8f, pixelsPerWorld * 0.35f);

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = x - center;
            float dy = y - center;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            float outside = Mathf.InverseLerp(clearPixels - feather, clearPixels + feather, dist);
            float alpha = Mathf.Clamp01(outside) * 0.45f;
            tex.SetPixel(x, y, new Color(0f, 0f, 0f, alpha));
        }

        tex.Apply();
        float pixelsPerUnit = size / (worldRadius * 2f);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), pixelsPerUnit);
    }

    static AllyBase FindAliveAlly(AllyType type)
    {
        AllyBase[] allies = Object.FindObjectsByType<AllyBase>(FindObjectsSortMode.None);
        foreach (var ally in allies)
            if (ally != null && !ally.isDead && ally.GetAllyType() == type)
                return ally;
        return null;
    }

    static EnemyBase FindEnemyAtScreenPosition(Vector2 screenPos)
    {
        if (Camera.main == null) return null;

        Vector3 world = Camera.main.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 10f));
        world.z = 0f;

        EnemyBase[] enemies = Object.FindObjectsByType<EnemyBase>(FindObjectsSortMode.None);
        EnemyBase bestEnemy = null;
        float bestDist = float.MaxValue;

        foreach (var enemy in enemies)
        {
            if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;
            Vector3 pos = enemy.transform.position;
            pos.z = 0f;
            float dist = Vector2.Distance(world, pos);
            if (dist > Mathf.Max(enemy.GetClickRadius(), 0.9f)) continue;
            if (dist < bestDist)
            {
                bestDist = dist;
                bestEnemy = enemy;
            }
        }
        return bestEnemy;
    }
}
