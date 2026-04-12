using UnityEngine;

/// <summary>
/// Topun 45° köşegen hareketini ve sekme (bounce) mantığını yönetir.
/// Hareket adım tabanlıdır: her adım bir hücreden diğerine smooth lerp ile geçer.
/// Sekme kuralları GridManager.Bounce() tarafından çözümlenir.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class BallController : MonoBehaviour
{
    public float speed    = 4f;  // saniyede kaç hücre
    public int   maxSteps = 200; // sonsuz döngü koruması

    Vector2Int  gridPos;
    Vector2Int  dir;
    Vector3     fromPos;
    Vector3     toPos;
    float       t;
    float       stepDur;
    bool        moving;
    bool        paused;
    int         stepCount;

    SpriteRenderer sr;

    public Vector2Int GridPosition => gridPos;
    public bool       IsMoving     => moving;

    // -----------------------------------------------------------------------
    void Awake() => sr = GetComponent<SpriteRenderer>();

    /// <summary>
    /// Topu başlatır. Init() çağrıldıktan sonra hareket başlar.
    /// </summary>
    public void Init(Vector2Int startCell, Vector2Int startDir, Color color)
    {
        gridPos   = startCell;
        dir       = startDir;
        sr.color  = color;
        stepDur   = 1f / Mathf.Max(speed, 0.1f);
        t         = 0f;
        moving    = true;
        paused    = false;
        stepCount = 0;

        fromPos = GridManager.Instance.GridToWorld(startCell.x, startCell.y);
        transform.position = fromPos;

        // Başlangıç yönü geçersizse hemen sektir
        ResolveDirection();
        if (moving)
            toPos = GridManager.Instance.GridToWorld((gridPos + dir).x, (gridPos + dir).y);
    }

    public void SetPaused(bool value) => paused = value;

    public void Stop() => moving = false;

    // -----------------------------------------------------------------------
    void Update()
    {
        if (!moving || paused) return;

        t += Time.deltaTime;
        float lerp = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / stepDur));
        transform.position = Vector3.Lerp(fromPos, toPos, lerp);

        if (t >= stepDur)
        {
            t       -= stepDur;
            gridPos += dir;
            fromPos  = toPos;

            ResolveDirection();
            if (!moving) return;

            toPos = GridManager.Instance.GridToWorld((gridPos + dir).x, (gridPos + dir).y);
        }
    }

    // -----------------------------------------------------------------------
    /// <summary>
    /// Bounce() çağırır, loglama yapar, step limitini kontrol eder.
    /// Sonuç hâlâ geçersizse tam geri dener; o da geçersizse topu durdurur.
    /// </summary>
    void ResolveDirection()
    {
        stepCount++;

        // --- Step limiti ---
        if (stepCount > maxSteps)
        {
            Debug.LogWarning($"[Ball] ⚠ Step limiti ({maxSteps}) aşıldı! Pos:{gridPos} — simülasyon durduruluyor.");
            moving = false;
            return;
        }

        Vector2Int target = gridPos + dir;
        var (newDir, bounceType) = GridManager.Instance.Bounce(gridPos, dir);

        // --- Adım logu ---
        string typeStr = bounceType switch
        {
            BounceType.Free          => "İlerledi",
            BounceType.FlatWallH     => "Düz duvar-H (Y yansıdı)",
            BounceType.FlatWallV     => "Düz duvar-V (X yansıdı)",
            BounceType.ConvexCorner  => "Çıkıntı köşe (%33 rastgele)",
            BounceType.ConcaveCorner => "Çukur köşe (geri döndü)",
            _                        => "?"
        };
        Debug.Log($"[Ball] #{stepCount:D3} | Pos:{gridPos} | Yön:{dir} | Hedef:{target} | {typeStr} | Yeni yön:{newDir}");

        dir = newDir;

        // --- Güvenlik: bounce sonrası hedef hâlâ geçersizse ---
        Vector2Int next = gridPos + dir;
        if (GridManager.Instance.IsSelected(next.x, next.y)) return;

        // İkinci şans: tam geri
        Debug.LogWarning($"[Ball] #{stepCount} Bounce sonrası hedef {next} hâlâ geçersiz — tam geri deneniyor.");
        dir  = -dir;
        next = gridPos + dir;
        if (GridManager.Instance.IsSelected(next.x, next.y)) return;

        // Sıkışık
        Debug.LogWarning($"[Ball] #{stepCount} Top sıkışık Pos:{gridPos} — durduruluyor.");
        moving = false;
    }
}
