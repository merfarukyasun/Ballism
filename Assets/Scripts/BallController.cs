using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Top hareketi, bounce, görsel efektler ve çarpışma desteği.
///
/// İki mod:
///   • Hücre modu (Init)        — eski API, korundu
///   • Köşe modu (InitAtCorner) — Faz 2.5, GridManager.BounceFromCorner kullanır
///
/// Görsel: SpriteRenderer (ana) + Glow child + TrailRenderer
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class BallController : MonoBehaviour
{
    // -----------------------------------------------------------------------
    [Header("Simülasyon")]
    public float speed    = 4f;
    public int   maxSteps = 200;

    // -----------------------------------------------------------------------
    // Hareket — her iki mod için ortak
    Vector2Int dir;
    Vector3    fromPos;
    Vector3    toPos;
    float      t;
    float      stepDur;
    bool       moving;
    bool       paused;
    int        stepCount;

    // -----------------------------------------------------------------------
    // Hücre modu
    Vector2Int gridPos;

    // -----------------------------------------------------------------------
    // Köşe modu
    bool       useCornerMode;
    Vector2Int cornerPos;

    // -----------------------------------------------------------------------
    // Görsel
    SpriteRenderer sr;
    SpriteRenderer glowRenderer;

    // -----------------------------------------------------------------------
    // Safe-corner-stop (intermediate UX pass)
    bool pauseAtNextCorner;
    /// <summary>GM'nin "top bir sonraki köşeye oturdu" bildirimi için callback.</summary>
    public System.Action<BallController, Vector2Int> OnStoppedAtCorner;

    // -----------------------------------------------------------------------
    // Public API
    public bool       IsMoving       => moving;
    public bool       IsPaused       => paused;
    public Vector2Int GridPosition   => gridPos;   // hücre modu
    public Vector2Int CornerPosition => cornerPos; // köşe modu

    /// <summary>Her iki modda da doğru pozisyonu döner (çarpışma kontrolü için).</summary>
    public Vector2Int CurrentPosition => useCornerMode ? cornerPos : gridPos;

    /// <summary>Çarpışma yön swap'ı için getter/setter.</summary>
    public Vector2Int Direction { get => dir; set => dir = value; }

    /// <summary>Top bir sonraki köşeye tam oturduğunda kendini pause'lar.</summary>
    public void RequestPauseAtNextCorner() => pauseAtNextCorner = true;

    // -----------------------------------------------------------------------
    void Awake() => sr = GetComponent<SpriteRenderer>();

    // -----------------------------------------------------------------------
    // HÜCRE MODU — eski API (korundu, değişmedi)
    // -----------------------------------------------------------------------

    /// <summary>Hücre merkezli başlangıç. Eski kod için geriye dönük uyumluluk.</summary>
    public void Init(Vector2Int startCell, Vector2Int startDir, Color color)
    {
        useCornerMode = false;
        gridPos    = startCell;
        dir        = startDir;
        stepDur    = 1f / Mathf.Max(speed, 0.1f);
        t          = 0f;
        moving     = true;
        paused     = false;
        stepCount  = 0;

        SetupVisuals(color);

        fromPos = GridManager.Instance.GridToWorld(startCell.x, startCell.y);
        transform.position = fromPos;

        ResolveDirection();
        if (moving)
            toPos = GridManager.Instance.GridToWorld(gridPos.x + dir.x, gridPos.y + dir.y);
    }

    // -----------------------------------------------------------------------
    // KÖŞE MODU — Faz 2.5
    // -----------------------------------------------------------------------

    /// <summary>
    /// Köşe tabanlı başlangıç.
    /// corner = grid kesişimi (0..width, 0..height)
    /// startDir = (±1, ±1) — köşeden köşeye yön
    /// </summary>
    public void InitAtCorner(Vector2Int corner, Vector2Int startDir, Color color)
    {
        useCornerMode = true;
        cornerPos  = corner;
        dir        = startDir;
        stepDur    = 1f / Mathf.Max(speed, 0.1f);
        t          = 0f;
        moving     = true;
        paused     = false;
        stepCount  = 0;

        SetupVisuals(color);

        fromPos = GridManager.Instance.CornerToWorld(corner.x, corner.y);
        transform.position = fromPos;

        // Başlangıç yönü geçerliliği: traversal hücresi seçili mi?
        int tx = dir.x > 0 ? corner.x : corner.x - 1;
        int ty = dir.y > 0 ? corner.y : corner.y - 1;
        if (!GridManager.Instance.IsSelected(tx, ty))
        {
            Debug.LogWarning($"[Ball] Köşe {corner} yön {dir} → traversal ({tx},{ty}) geçersiz!");
            moving = false;
            return;
        }

        toPos = GridManager.Instance.CornerToWorld(corner.x + dir.x, corner.y + dir.y);
    }

    // -----------------------------------------------------------------------
    public void SetPaused(bool value) => paused = value;
    public void Stop()                => moving = false;

    // -----------------------------------------------------------------------
    // Update
    // -----------------------------------------------------------------------

    void Update()
    {
        if (!moving || paused) return;

        t += Time.deltaTime;
        float lerp = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / stepDur));
        transform.position = Vector3.Lerp(fromPos, toPos, lerp);

        if (t >= stepDur)
        {
            t -= stepDur;

            if (useCornerMode)
            {
                cornerPos += dir;
                fromPos    = toPos;
                ResolveDirectionCorner();
                if (!moving) return;
                toPos = GridManager.Instance.CornerToWorld(cornerPos.x + dir.x, cornerPos.y + dir.y);

                // Safe-corner-stop: GM pause isteği bekliyorsa, tam köşeye oturmuş
                // halde (fromPos==cornerPos, t=0, toPos=next) kendimizi pause'la.
                if (pauseAtNextCorner)
                {
                    pauseAtNextCorner = false;
                    paused = true;
                    OnStoppedAtCorner?.Invoke(this, cornerPos);
                }
            }
            else
            {
                gridPos += dir;
                fromPos  = toPos;
                ResolveDirection();
                if (!moving) return;
                toPos = GridManager.Instance.GridToWorld(gridPos.x + dir.x, gridPos.y + dir.y);
            }
        }
    }

    // -----------------------------------------------------------------------
    // BOUNCE — hücre modu (değişmedi)
    // -----------------------------------------------------------------------

    void ResolveDirection()
    {
        stepCount++;
        if (stepCount > maxSteps)
        {
            Debug.LogWarning($"[Ball] ⚠ Step limiti ({maxSteps}) — durduruluyor.");
            moving = false;
            return;
        }

        Vector2Int target = gridPos + dir;
        var (newDir, bounceType) = GridManager.Instance.Bounce(gridPos, dir);

        Debug.Log($"[Ball] #{stepCount:D3} Hücre:{gridPos} {BounceLabel(bounceType)} Yön:{dir}→{newDir}");
        dir = newDir;

        Vector2Int next = gridPos + dir;
        if (GridManager.Instance.IsSelected(next.x, next.y)) return;

        Debug.LogWarning($"[Ball] #{stepCount} Bounce sonrası {next} geçersiz — tam geri.");
        dir  = -dir;
        next = gridPos + dir;
        if (GridManager.Instance.IsSelected(next.x, next.y)) return;

        Debug.LogWarning($"[Ball] #{stepCount} Sıkışık — durduruluyor.");
        moving = false;
    }

    // -----------------------------------------------------------------------
    // BOUNCE — köşe modu (Faz 2.5)
    // -----------------------------------------------------------------------

    void ResolveDirectionCorner()
    {
        stepCount++;
        if (stepCount > maxSteps)
        {
            Debug.LogWarning($"[Ball] ⚠ Step limiti ({maxSteps}) — durduruluyor.");
            moving = false;
            return;
        }

        var (newDir, bounceType) = GridManager.Instance.BounceFromCorner(cornerPos, dir);

        Debug.Log($"[Ball] #{stepCount:D3} Köşe:{cornerPos} {BounceLabel(bounceType)} Yön:{dir}→{newDir}");
        dir = newDir;

        // Güvenlik: yeni yönün traversal hücresi seçili mi?
        int tx = dir.x > 0 ? cornerPos.x : cornerPos.x - 1;
        int ty = dir.y > 0 ? cornerPos.y : cornerPos.y - 1;
        if (GridManager.Instance.IsSelected(tx, ty)) return;

        Debug.LogWarning($"[Ball] #{stepCount} Traversal ({tx},{ty}) geçersiz — tam geri.");
        dir = -dir;
        tx  = dir.x > 0 ? cornerPos.x : cornerPos.x - 1;
        ty  = dir.y > 0 ? cornerPos.y : cornerPos.y - 1;
        if (GridManager.Instance.IsSelected(tx, ty)) return;

        Debug.LogWarning($"[Ball] #{stepCount} Sıkışık köşe — durduruluyor.");
        moving = false;
    }

    static string BounceLabel(BounceType t) => t switch
    {
        BounceType.Free          => "→",
        BounceType.FlatWallH     => "⤡H",
        BounceType.FlatWallV     => "⤡V",
        BounceType.ConvexCorner  => "↗Çıkıntı",
        BounceType.ConcaveCorner => "↙Çukur",
        _                        => "?"
    };

    // -----------------------------------------------------------------------
    // GÖRSEL: Glow + Trail
    // -----------------------------------------------------------------------

    void SetupVisuals(Color color)
    {
        // --- Ana sprite rengi ---
        sr.color = color;

        // --- Glow child (yumuşak halo) ---
        var glowT = transform.Find("Glow");
        if (glowT == null)
        {
            var g = new GameObject("Glow");
            g.transform.SetParent(transform);
            g.transform.localPosition = Vector3.zero;
            g.transform.localScale    = new Vector3(2.8f, 2.8f, 1f);
            glowRenderer = g.AddComponent<SpriteRenderer>();
        }
        else
        {
            glowRenderer = glowT.GetComponent<SpriteRenderer>();
        }

        if (glowRenderer != null)
        {
            glowRenderer.sprite       = sr.sprite;
            glowRenderer.color        = new Color(color.r, color.g, color.b, 0.15f);
            glowRenderer.sortingOrder = sr.sortingOrder - 1;
            glowRenderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        // --- Trail (soluk iz) ---
        // Unity fake-null: ?? operatörü GetComponent'in null'unu yakalamaz → explicit kontrol
        var trail = GetComponent<TrailRenderer>();
        if (trail == null) trail = gameObject.AddComponent<TrailRenderer>();
        trail.time        = 0.40f;
        trail.startWidth  = 0.28f;
        trail.endWidth    = 0.0f;
        trail.startColor  = new Color(color.r, color.g, color.b, 0.80f);
        trail.endColor    = new Color(color.r, color.g, color.b, 0.0f);
        trail.sortingOrder           = sr.sortingOrder;
        trail.shadowCastingMode      = ShadowCastingMode.Off;
        trail.receiveShadows         = false;
        trail.generateLightingData   = false;

        // Shader — Sprites/Default URP'de de çalışır; bulamazsa varsayılan kullanılır
        var sh = Shader.Find("Sprites/Default");
        if (sh != null) trail.material = new Material(sh);
    }
}
