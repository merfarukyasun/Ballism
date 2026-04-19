using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// -----------------------------------------------------------------------
public enum BounceType
{
    Free,
    FlatWallH,
    FlatWallV,
    ConvexCorner,
    ConcaveCorner
}

/// <summary>
/// Kenar-tabanlı (edge-based) grid editörü.
/// Faz 2: İç/dış bölge hover preview, exterior bölge seçimi,
///         akıllı kenar görselleştirme, köşe (corner) tabanlı spawn desteği.
/// </summary>
public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }

    [Header("Grid")]
    public int   width    = 10;
    public int   height   = 10;
    public float cellSize = 1f;

    [Header("Prefab")]
    public GameObject cellPrefab;

    [Header("Renkler — Hücre")]
    public Color colorBackground  = new Color(0.08f, 0.09f, 0.12f);
    public Color colorPlayable    = new Color(0.16f, 0.24f, 0.42f);
    public Color colorRegionGreen = new Color(0.20f, 0.90f, 0.35f, 0.55f);

    [Header("Renkler — Kenar (Drawing modu)")]
    public Color colorEdgeInactive = new Color(0.28f, 0.28f, 0.32f, 0.30f);
    public Color colorEdgeActive   = new Color(0.95f, 0.15f, 0.15f);

    [Header("Renkler — Kenar (Oyun alanı)")]
    public Color colorBorderEdge = new Color(0.80f, 0.85f, 1.00f, 0.90f); // belirgin dış sınır
    public Color colorInnerEdge  = new Color(0.30f, 0.38f, 0.55f, 0.30f); // hafif iç grid çizgisi

    // -----------------------------------------------------------------------
    // Edge veri modeli
    bool[,] hEdges;      // [height+1, width]
    bool[,] vEdges;      // [height,   width+1]
    GameObject[,] hEdgeObjs;
    GameObject[,] vEdgeObjs;

    // -----------------------------------------------------------------------
    // Hücre renderers
    SpriteRenderer[,] cellRenderers;

    // -----------------------------------------------------------------------
    // Bölge verileri
    bool[,]                  playableCells;
    List<List<Vector2Int>>   closedRegions  = new List<List<Vector2Int>>();
    List<Vector2Int>         exteriorRegion = new List<Vector2Int>();
    List<List<Vector2Int>>   allRegions     = new List<List<Vector2Int>>();
    int                      hoveredRegionIdx = -1;

    // -----------------------------------------------------------------------
    // Input
    bool drawEnabled = true;
    bool isDragging;
    bool dragValue;
    bool dragIsH;
    int  dragA, dragB;

    // -----------------------------------------------------------------------
    public bool HasClosedRegion => closedRegions.Count > 0;

    // -----------------------------------------------------------------------
    void Awake()
    {
        Instance      = this;
        hEdges        = new bool[height + 1, width];
        vEdges        = new bool[height,     width + 1];
        hEdgeObjs     = new GameObject[height + 1, width];
        vEdgeObjs     = new GameObject[height,     width + 1];
        playableCells = new bool[width, height];
        cellRenderers = new SpriteRenderer[width, height];
    }

    void Start()
    {
        BuildCells();
        BuildEdges();
    }

    // -----------------------------------------------------------------------
    // İnşa
    // -----------------------------------------------------------------------

    void BuildCells()
    {
        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
        {
            var pos = new Vector3(x * cellSize + cellSize * 0.5f,
                                  y * cellSize + cellSize * 0.5f, 0.05f);
            var go = Instantiate(cellPrefab, pos, Quaternion.identity, transform);
            go.name = $"Cell_{x}_{y}";
            go.transform.localScale = new Vector3(cellSize * 0.98f, cellSize * 0.98f, 1f);

            var gc = go.AddComponent<GridCell>();
            gc.gridX = x; gc.gridY = y;

            var sr = go.GetComponent<SpriteRenderer>();
            sr.sortingOrder = 0;
            sr.color = colorBackground;
            cellRenderers[x, y] = sr;
        }
    }

    void BuildEdges()
    {
        for (int yr = 0; yr <= height; yr++)
        for (int xc = 0; xc < width; xc++)
        {
            float wx = xc * cellSize + cellSize * 0.5f;
            float wy = yr * cellSize;
            hEdgeObjs[yr, xc] = CreateEdgeObject($"HEdge_{yr}_{xc}",
                new Vector3(wx, wy, -0.05f),
                new Vector3(cellSize * 0.96f, cellSize * 0.07f, 1f));
        }

        for (int yc = 0; yc < height; yc++)
        for (int xr = 0; xr <= width; xr++)
        {
            float wx = xr * cellSize;
            float wy = yc * cellSize + cellSize * 0.5f;
            vEdgeObjs[yc, xr] = CreateEdgeObject($"VEdge_{yc}_{xr}",
                new Vector3(wx, wy, -0.05f),
                new Vector3(cellSize * 0.07f, cellSize * 0.96f, 1f));
        }
    }

    GameObject CreateEdgeObject(string objName, Vector3 pos, Vector3 scale)
    {
        var go = Instantiate(cellPrefab, pos, Quaternion.identity, transform);
        go.name = objName;
        go.transform.localScale = scale;
        var sr = go.GetComponent<SpriteRenderer>();
        sr.sortingOrder = 1;
        sr.color = colorEdgeInactive;
        return go;
    }

    // -----------------------------------------------------------------------
    // Update — Çizim modu
    // -----------------------------------------------------------------------

    void Update()
    {
        if (!drawEnabled) return;

        var mouse = Mouse.current;
        if (mouse == null) return;

        Vector2 screenPos  = mouse.position.ReadValue();
        Vector3 world3     = Camera.main.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 0f));
        Vector2 mouseWorld = new Vector2(world3.x, world3.y);

        const float snapThreshold = 0.22f;

        if (mouse.leftButton.wasPressedThisFrame)
        {
            var (isH, a, b, dist) = FindNearestEdge(mouseWorld);
            if (dist < cellSize * snapThreshold)
            {
                isDragging = true;
                dragIsH    = isH;
                dragA      = a;
                dragB      = b;
                dragValue  = isH ? !hEdges[a, b] : !vEdges[a, b];
                SetEdge(isH, a, b, dragValue);
            }
        }

        if (mouse.leftButton.isPressed && isDragging)
        {
            var (isH, a, b, dist) = FindNearestEdge(mouseWorld, lockIsH: dragIsH);
            bool notSame = !(a == dragA && b == dragB);
            if (dist < cellSize * snapThreshold && notSame)
            {
                dragA = a; dragB = b;
                SetEdge(dragIsH, a, b, dragValue);
            }
        }

        if (mouse.leftButton.wasReleasedThisFrame) isDragging = false;
    }

    // -----------------------------------------------------------------------
    // Kenar işlemleri
    // -----------------------------------------------------------------------

    void SetEdge(bool isH, int a, int b, bool value)
    {
        if (isH) hEdges[a, b] = value;
        else     vEdges[a, b] = value;
        RefreshEdgeVisual(isH, a, b);
    }

    void RefreshEdgeVisual(bool isH, int a, int b)
    {
        bool active = isH ? hEdges[a, b] : vEdges[a, b];
        var  obj    = isH ? hEdgeObjs[a, b] : vEdgeObjs[a, b];
        if (obj == null) return;
        var sr = obj.GetComponent<SpriteRenderer>();
        if (sr) sr.color = active ? colorEdgeActive : colorEdgeInactive;
    }

    (bool isH, int a, int b, float dist) FindNearestEdge(Vector2 p, bool? lockIsH = null)
    {
        float best = float.MaxValue;
        bool bestH = true;
        int  bestA = 0, bestB = 0;

        if (lockIsH == null || lockIsH == true)
        {
            for (int yr = 0; yr <= height; yr++)
            for (int xc = 0; xc < width; xc++)
            {
                float d = DistToSegment(p,
                    new Vector2(xc * cellSize,           yr * cellSize),
                    new Vector2((xc + 1) * cellSize, yr * cellSize));
                if (d < best) { best = d; bestH = true; bestA = yr; bestB = xc; }
            }
        }

        if (lockIsH == null || lockIsH == false)
        {
            for (int yc = 0; yc < height; yc++)
            for (int xr = 0; xr <= width; xr++)
            {
                float d = DistToSegment(p,
                    new Vector2(xr * cellSize, yc * cellSize),
                    new Vector2(xr * cellSize, (yc + 1) * cellSize));
                if (d < best) { best = d; bestH = false; bestA = yc; bestB = xr; }
            }
        }

        return (bestH, bestA, bestB, best);
    }

    static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab    = b - a;
        float   sqLen = Vector2.Dot(ab, ab);
        if (sqLen < 1e-6f) return Vector2.Distance(p, a);
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / sqLen);
        return Vector2.Distance(p, a + t * ab);
    }

    // -----------------------------------------------------------------------
    // Bölge hesaplama — Flood Fill
    // -----------------------------------------------------------------------

    public List<List<Vector2Int>> ComputeRegions()
    {
        // ---- Adım 1: Dışarıdan erişilebilen hücreleri işaretle ----
        var reachable = new bool[width, height];
        var queue     = new Queue<Vector2Int>();

        void TryAdd(int x, int y)
        {
            if (x < 0 || x >= width || y < 0 || y >= height) return;
            if (reachable[x, y]) return;
            reachable[x, y] = true;
            queue.Enqueue(new Vector2Int(x, y));
        }

        for (int x = 0; x < width; x++)
        {
            if (!hEdges[0,      x]) TryAdd(x, 0);
            if (!hEdges[height, x]) TryAdd(x, height - 1);
        }
        for (int y = 0; y < height; y++)
        {
            if (!vEdges[y, 0])     TryAdd(0, y);
            if (!vEdges[y, width]) TryAdd(width - 1, y);
        }

        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            int x = c.x, y = c.y;
            if (x + 1 < width  && !vEdges[y, x + 1]) TryAdd(x + 1, y);
            if (x - 1 >= 0     && !vEdges[y, x])     TryAdd(x - 1, y);
            if (y + 1 < height && !hEdges[y + 1, x]) TryAdd(x, y + 1);
            if (y - 1 >= 0     && !hEdges[y, x])     TryAdd(x, y - 1);
        }

        // ---- Adım 2: Kapalı iç bölgeleri bul ----
        closedRegions = new List<List<Vector2Int>>();
        var visited   = new bool[width, height];

        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
        {
            if (reachable[x, y] || visited[x, y]) continue;

            var region = new List<Vector2Int>();
            var q2     = new Queue<Vector2Int>();
            visited[x, y] = true;
            q2.Enqueue(new Vector2Int(x, y));

            while (q2.Count > 0)
            {
                var c = q2.Dequeue();
                region.Add(c);
                int cx = c.x, cy = c.y;

                void TryExpand(int nx, int ny, bool blocked)
                {
                    if (nx < 0 || nx >= width || ny < 0 || ny >= height) return;
                    if (blocked || reachable[nx, ny] || visited[nx, ny]) return;
                    visited[nx, ny] = true;
                    q2.Enqueue(new Vector2Int(nx, ny));
                }

                TryExpand(cx + 1, cy,     vEdges[cy, cx + 1]);
                TryExpand(cx - 1, cy,     vEdges[cy, cx]);
                TryExpand(cx,     cy + 1, hEdges[cy + 1, cx]);
                TryExpand(cx,     cy - 1, hEdges[cy, cx]);
            }

            closedRegions.Add(region);
        }

        // ---- Adım 3: Dış bölge (exterior) — hem seçilebilir ----
        exteriorRegion = new List<Vector2Int>();
        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
            if (reachable[x, y]) exteriorRegion.Add(new Vector2Int(x, y));

        // allRegions: iç bölgeler + dış bölge (seçim ve hover için)
        allRegions = new List<List<Vector2Int>>(closedRegions);
        if (exteriorRegion.Count > 0) allRegions.Add(exteriorRegion);

        hoveredRegionIdx = -1;
        return closedRegions;
    }

    // -----------------------------------------------------------------------
    // Bölge görselleştirme — Hover
    // -----------------------------------------------------------------------

    /// <summary>
    /// Mouse pozisyonundaki hücreye göre hover bölgesini günceller.
    /// Her karede çağrılabilir; değişiklik yoksa erken çıkar.
    /// </summary>
    public void HoverRegionAtCell(int x, int y)
    {
        int idx = -1;
        if (x >= 0 && x < width && y >= 0 && y < height)
            idx = FindRegionIndex(x, y);

        if (idx == hoveredRegionIdx) return; // değişiklik yok
        hoveredRegionIdx = idx;

        if (idx < 0)
            ShowRegionPreviews(); // hiçbir bölge hover'da değil → tümünü göster
        else
            ShowSingleRegionHover(idx);
    }

    int FindRegionIndex(int x, int y)
    {
        for (int i = 0; i < allRegions.Count; i++)
            foreach (var cell in allRegions[i])
                if (cell.x == x && cell.y == y) return i;
        return -1;
    }

    void ShowSingleRegionHover(int idx)
    {
        var dimColor = new Color(0.05f, 0.05f, 0.07f, 0.5f);
        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
        {
            cellRenderers[x, y].enabled = true;
            cellRenderers[x, y].color   = dimColor;
        }

        if (idx < 0 || idx >= allRegions.Count) return;

        Color hoverColor = colorRegionGreen; // a=0.55 zaten ayarlı
        foreach (var cell in allRegions[idx])
            cellRenderers[cell.x, cell.y].color = hoverColor;
    }

    /// <summary>
    /// Tüm kapalı bölgeleri yeşil preview ile gösterir (onay sonrası ilk görüntü).
    /// </summary>
    public void ShowRegionPreviews()
    {
        var outsideColor = new Color(0.05f, 0.05f, 0.07f, 0.5f);
        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
        {
            cellRenderers[x, y].enabled = true;
            cellRenderers[x, y].color   = outsideColor;
        }

        for (int i = 0; i < closedRegions.Count; i++)
        {
            float hue = (0.35f + i * 0.12f) % 1f;
            Color rc  = i == 0
                ? colorRegionGreen
                : Color.HSVToRGB(hue, 0.7f, 0.85f);
            rc.a = 0.55f;
            foreach (var cell in closedRegions[i])
                cellRenderers[cell.x, cell.y].color = rc;
        }
        // Not: dış bölge (exterior) hover ile gösterilir, başlangıçta karanlık kalır.
    }

    // -----------------------------------------------------------------------
    // Bölge seçimi ve uygulama
    // -----------------------------------------------------------------------

    /// <summary>
    /// Tıklanan hücreyi içeren bölgeyi (iç veya dış) oyun alanı yapar.
    /// </summary>
    public bool SelectRegionAtCell(int x, int y)
    {
        foreach (var region in allRegions)
        {
            foreach (var cell in region)
            {
                if (cell.x == x && cell.y == y)
                {
                    ApplyPlayableRegion(region);
                    hoveredRegionIdx = -1;
                    return true;
                }
            }
        }
        return false;
    }

    void ApplyPlayableRegion(List<Vector2Int> region)
    {
        // Tüm hücreleri gizle, maskesi temizle
        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
        {
            playableCells[x, y]         = false;
            cellRenderers[x, y].enabled = false;
        }

        // Seçilen bölge hücrelerini aktifleştir
        foreach (var cell in region)
        {
            playableCells[cell.x, cell.y]        = true;
            cellRenderers[cell.x, cell.y].color   = colorPlayable;
            cellRenderers[cell.x, cell.y].enabled = true;
        }

        // Akıllı kenar görselleştirmesi
        RefreshPlayableEdges();
    }

    /// <summary>
    /// Her kenarı analiz eder:
    /// - Her iki taraf playable   → hafif iç grid çizgisi
    /// - Tek taraf playable       → belirgin sınır
    /// - Hiçbiri playable değil   → gizle
    /// </summary>
    void RefreshPlayableEdges()
    {
        // Yatay kenarlar: hEdges[yr, xc] — cell(xc, yr-1) ile (xc, yr) arası
        for (int yr = 0; yr <= height; yr++)
        for (int xc = 0; xc < width; xc++)
        {
            var obj = hEdgeObjs[yr, xc];
            if (obj == null) continue;

            bool lowerSel = yr > 0      && playableCells[xc, yr - 1];
            bool upperSel = yr < height && playableCells[xc, yr];

            UpdateEdgeObject(obj, lowerSel, upperSel);
        }

        // Dikey kenarlar: vEdges[yc, xr] — cell(xr-1, yc) ile (xr, yc) arası
        for (int yc = 0; yc < height; yc++)
        for (int xr = 0; xr <= width; xr++)
        {
            var obj = vEdgeObjs[yc, xr];
            if (obj == null) continue;

            bool leftSel  = xr > 0     && playableCells[xr - 1, yc];
            bool rightSel = xr < width && playableCells[xr,     yc];

            UpdateEdgeObject(obj, leftSel, rightSel);
        }
    }

    void UpdateEdgeObject(GameObject obj, bool sideA, bool sideB)
    {
        var sr = obj.GetComponent<SpriteRenderer>();
        if (sr == null) return;

        if (sideA && sideB)
        {
            obj.SetActive(true);
            sr.color = colorInnerEdge;
        }
        else if (sideA || sideB)
        {
            obj.SetActive(true);
            sr.color = colorBorderEdge;
        }
        else
        {
            obj.SetActive(false);
        }
    }

    public void SetEdgesVisible(bool visible)
    {
        for (int yr = 0; yr <= height; yr++)
        for (int xc = 0; xc < width; xc++)
            if (hEdgeObjs[yr, xc] != null) hEdgeObjs[yr, xc].SetActive(visible);

        for (int yc = 0; yc < height; yc++)
        for (int xr = 0; xr <= width; xr++)
            if (vEdgeObjs[yc, xr] != null) vEdgeObjs[yc, xr].SetActive(visible);
    }

    // -----------------------------------------------------------------------
    // Köşe (Corner) API — Faz 2
    // -----------------------------------------------------------------------

    /// <summary>Grid köşesinin dünya konumu. Köşe (0,0) = world (0,0).</summary>
    public Vector3 CornerToWorld(int cx, int cy)
        => new Vector3(cx * cellSize, cy * cellSize, 0f);

    /// <summary>
    /// Dünya konumunu en yakın geçerli köşeye snap'ler.
    /// Geçerli = en az bir komşu hücre oyun alanında.
    /// </summary>
    public (bool valid, Vector2Int corner) WorldToNearestPlayableCorner(Vector2 world)
    {
        int cx = Mathf.RoundToInt(world.x / cellSize);
        int cy = Mathf.RoundToInt(world.y / cellSize);
        cx = Mathf.Clamp(cx, 0, width);
        cy = Mathf.Clamp(cy, 0, height);

        bool valid =
            (cx > 0     && cy > 0      && playableCells[cx - 1, cy - 1]) ||
            (cx < width && cy > 0      && playableCells[cx,     cy - 1]) ||
            (cx > 0     && cy < height && playableCells[cx - 1, cy])     ||
            (cx < width && cy < height && playableCells[cx,     cy]);

        return (valid, new Vector2Int(cx, cy));
    }

    /// <summary>
    /// Bir köşeden hangi yönlerin geçerli olduğunu döner
    /// (o yöndeki traversal hücresi seçili olan yönler).
    /// </summary>
    public List<Vector2Int> GetValidCornerDirections(Vector2Int corner)
    {
        var result = new List<Vector2Int>();
        var allDirs = new Vector2Int[] {
            new( 1,  1), new( 1, -1), new(-1,  1), new(-1, -1)
        };
        foreach (var d in allDirs)
        {
            int tx = d.x > 0 ? corner.x : corner.x - 1;
            int ty = d.y > 0 ? corner.y : corner.y - 1;
            if (IsSelected(tx, ty)) result.Add(d);
        }
        return result;
    }

    // -----------------------------------------------------------------------
    // Bounce — BallController için
    // -----------------------------------------------------------------------

    /// <summary>Hücre merkezli (eski) bounce. Geriye dönük uyumluluk için korundu.</summary>
    public (Vector2Int newDir, BounceType type) Bounce(Vector2Int pos, Vector2Int dir)
    {
        Vector2Int target = pos + dir;
        if (IsSelected(target.x, target.y))
            return (dir, BounceType.Free);

        bool hOpen = IsSelected(pos.x + dir.x, pos.y);
        bool vOpen = IsSelected(pos.x, pos.y + dir.y);

        Vector2Int newDir;
        BounceType type;

        if (hOpen && vOpen)
        {
            float r = Random.value;
            if      (r < 1f / 3f) newDir = new Vector2Int(-dir.x,  dir.y);
            else if (r < 2f / 3f) newDir = new Vector2Int( dir.x, -dir.y);
            else                  newDir = -dir;
            type = BounceType.ConvexCorner;
        }
        else if (hOpen)
        {
            newDir = new Vector2Int(dir.x, -dir.y);
            type   = BounceType.FlatWallH;
        }
        else if (vOpen)
        {
            newDir = new Vector2Int(-dir.x, dir.y);
            type   = BounceType.FlatWallV;
        }
        else
        {
            newDir = -dir;
            type   = BounceType.ConcaveCorner;
        }

        return (newDir, type);
    }

    /// <summary>
    /// Köşe tabanlı bounce (Faz 2).
    /// corner = köşe koordinatı (0..width, 0..height), dir = (±1, ±1).
    ///
    /// Traversal hücre convention (corner cx,cy, dir dx,dy):
    ///   fwd_x = dx > 0 ? cx : cx-1
    ///   fwd_y = dy > 0 ? cy : cy-1
    /// </summary>
    public (Vector2Int newDir, BounceType type) BounceFromCorner(Vector2Int corner, Vector2Int dir)
    {
        // İleri traversal hücresi
        int fwd_x = dir.x > 0 ? corner.x : corner.x - 1;
        int fwd_y = dir.y > 0 ? corner.y : corner.y - 1;

        if (IsSelected(fwd_x, fwd_y))
            return (dir, BounceType.Free);

        // Y yönü yansıması için traversal hücresi (aynı X bandı, ters Y)
        int hor_y = dir.y < 0 ? corner.y : corner.y - 1;
        bool hOpen = IsSelected(fwd_x, hor_y);

        // X yönü yansıması için traversal hücresi (ters X, aynı Y bandı)
        int ver_x = dir.x < 0 ? corner.x : corner.x - 1;
        bool vOpen = IsSelected(ver_x, fwd_y);

        Vector2Int newDir;
        BounceType type;

        if (hOpen && vOpen)
        {
            // 4. kadran hücresi: -dir seçeneğinin geçerliliği kontrol et
            bool revOpen = IsSelected(ver_x, hor_y);

            if (revOpen)
            {
                float r = Random.value;
                if      (r < 1f / 3f) newDir = new Vector2Int(-dir.x,  dir.y);
                else if (r < 2f / 3f) newDir = new Vector2Int( dir.x, -dir.y);
                else                  newDir = -dir;
            }
            else
            {
                // Tam geri geçersiz — sadece iki geçerli yöne yönlendir
                newDir = Random.value < 0.5f
                    ? new Vector2Int(-dir.x,  dir.y)
                    : new Vector2Int( dir.x, -dir.y);
            }
            type = BounceType.ConvexCorner;
        }
        else if (hOpen)
        {
            newDir = new Vector2Int(dir.x, -dir.y);
            type   = BounceType.FlatWallH;
        }
        else if (vOpen)
        {
            newDir = new Vector2Int(-dir.x, dir.y);
            type   = BounceType.FlatWallV;
        }
        else
        {
            newDir = -dir;
            type   = BounceType.ConcaveCorner;
        }

        return (newDir, type);
    }

    // -----------------------------------------------------------------------
    // Public API — BallController / GameManager
    // -----------------------------------------------------------------------

    public bool IsSelected(int x, int y)
    {
        if (x < 0 || x >= width || y < 0 || y >= height) return false;
        return playableCells[x, y];
    }

    public void SetDrawEnabled(bool v) => drawEnabled = v;

    public bool HasSelection()
    {
        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
            if (playableCells[x, y]) return true;
        return false;
    }

    public void Clear()
    {
        for (int yr = 0; yr <= height; yr++)
        for (int xc = 0; xc < width; xc++)
            SetEdge(true, yr, xc, false);

        for (int yc = 0; yc < height; yc++)
        for (int xr = 0; xr <= width; xr++)
            SetEdge(false, yc, xr, false);

        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
        {
            playableCells[x, y]         = false;
            cellRenderers[x, y].color   = colorBackground;
            cellRenderers[x, y].enabled = true;
        }

        closedRegions.Clear();
        exteriorRegion.Clear();
        allRegions.Clear();
        hoveredRegionIdx = -1;
        SetEdgesVisible(true);
    }

    public Vector3 GridToWorld(int x, int y)
        => new Vector3(x * cellSize + cellSize * 0.5f,
                       y * cellSize + cellSize * 0.5f, 0f);

    public Vector2Int WorldToGrid(Vector3 world)
        => new Vector2Int(Mathf.FloorToInt(world.x / cellSize),
                          Mathf.FloorToInt(world.y / cellSize));
}
