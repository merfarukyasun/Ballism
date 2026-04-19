using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum GameState
{
    Drawing,        // Oyuncu kenar çiziyor
    RegionSelect,   // Kapalı bölgeler gösterildi, oyuncu birini seçiyor
    SpawnSelect,    // Top yerleştirme (max 3, köşe tabanlı)
    Simulating,     // Toplar hareket ediyor
    Paused          // Durduruldu
}

/// <summary>
/// Oyun durumu, köşe tabanlı top spawn (Faz 2.5), çarpışma tespiti.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Prefabs")]
    public GameObject ballPrefab;
    public GameObject dirIndicatorPrefab;

    [Header("Ayarlar")]
    public float ballSpeed = 4f;
    public int   maxBalls  = 3;

    [Header("Top Renkleri")]
    public Color[] ballColors =
    {
        new Color(0f,   0.90f, 1.0f),  // #1 turkuaz
        new Color(1f,   0.82f, 0.0f),  // #2 sarı
        new Color(1f,   0.40f, 0.1f),  // #3 turuncu
    };

    // -----------------------------------------------------------------------
    // SpawnSelect iki fazlı: önce köşe, sonra yön
    enum SpawnPhase { CornerPick, DirectionPick }

    GameState            state;
    List<BallController> balls         = new List<BallController>();
    List<GameObject>     dirIndicators = new List<GameObject>();

    Vector2Int pendingSpawn;  // köşe koordinatı (corner space)
    bool       hasPending;

    // -----------------------------------------------------------------------
    // Spawn UX (intermediate pass)
    SpawnPhase     spawnPhase     = SpawnPhase.CornerPick;
    Vector2Int?    selectedCorner = null;
    GameObject     hoverHalo;      // reused hover sprite
    GameObject     selectedHalo;   // ayrı "seçili köşe" sprite
    HashSet<Vector2Int> occupiedCorners = new HashSet<Vector2Int>();

    // Pause orchestration (safe-corner-stop)
    bool pauseRequested;
    int  ballsAwaitingStop;

    public GameState State     => state;
    public int       BallCount => balls.Count;
    public System.Action<GameState> OnStateChanged;

    /// <summary>UIManager yönergesi için — SpawnSelect + CornerPick fazında mı?</summary>
    public bool IsPickingCorner => state == GameState.SpawnSelect
                                   && spawnPhase == SpawnPhase.CornerPick;
    /// <summary>UIManager etiketi için — "Durduruluyor..." durumu.</summary>
    public bool IsStopping      => state == GameState.Simulating && pauseRequested;

    // -----------------------------------------------------------------------
    void Awake() => Instance = this;
    void Start()  => SetState(GameState.Drawing);

    // -----------------------------------------------------------------------
    // UIManager → GameManager
    // -----------------------------------------------------------------------

    public void OnConfirmShape()
    {
        if (state != GameState.Drawing) return;
        var regions = GridManager.Instance.ComputeRegions();
        if (regions.Count == 0)
        {
            Debug.Log("[GM] Kapalı bölge yok. Kenarları birleştirerek kapalı alan oluştur.");
            return;
        }
        GridManager.Instance.ShowRegionPreviews();
        SetState(GameState.RegionSelect);
    }

    /// <summary>Tüm toplar yerleştirildikten sonra simülasyonu başlatır.</summary>
    public void OnStartSimulation()
    {
        if (state != GameState.SpawnSelect) return;
        if (balls.Count == 0) return;
        SetState(GameState.Simulating);
    }

    public void OnAddBall()
    {
        if (state != GameState.Simulating && state != GameState.Paused) return;
        if (pauseRequested) return; // Durdurma in-flight → spawn akışı açılmasın
        if (balls.Count >= maxBalls) return;
        SetState(GameState.SpawnSelect);
    }

    public void OnTogglePause()
    {
        // Simulating → "safe-corner-stop" isteği
        if (state == GameState.Simulating)
        {
            if (pauseRequested) return; // çift Durdur no-op

            ballsAwaitingStop = 0;
            foreach (var b in balls)
            {
                if (b == null || !b.IsMoving || b.IsPaused) continue;
                b.RequestPauseAtNextCorner();
                ballsAwaitingStop++;
            }

            if (ballsAwaitingStop == 0)
            {
                // Hareketli top yok — doğrudan Paused
                SetState(GameState.Paused);
                return;
            }

            pauseRequested = true;
            OnStateChanged?.Invoke(state); // UI "DURDURULUYOR..." etiketini güncellesin
            return;
        }

        // Paused → Resume: occupied köşeler artık boşalacak
        if (state == GameState.Paused)
        {
            occupiedCorners.Clear();
            SetState(GameState.Simulating);
        }
    }

    /// <summary>BallController callback — bir top tam köşeye oturdu.</summary>
    void OnBallStoppedAtCorner(BallController b, Vector2Int corner)
    {
        // Reset sonrası gecikmiş callback veya alakasız durum → ignore
        if (state != GameState.Simulating || !pauseRequested) return;

        occupiedCorners.Add(corner);
        ballsAwaitingStop--;

        if (ballsAwaitingStop <= 0)
        {
            pauseRequested    = false;
            ballsAwaitingStop = 0;
            SetState(GameState.Paused);
        }
    }

    public void OnReset()
    {
        ClearBalls();
        ClearIndicators();
        HideHoverHalo();
        HideSelectedHalo();
        GridManager.Instance.Clear();

        // Tüm UX/pause sayaçlarını güvenli sıfırla — gecikmiş callback'ler
        // OnBallStoppedAtCorner guard'ı tarafından yutulacak.
        occupiedCorners.Clear();
        pauseRequested    = false;
        ballsAwaitingStop = 0;
        spawnPhase        = SpawnPhase.CornerPick;
        selectedCorner    = null;
        hasPending        = false;

        SetState(GameState.Drawing);
    }

    public void OnClearShape()
    {
        if (state == GameState.Drawing) GridManager.Instance.Clear();
    }

    // -----------------------------------------------------------------------
    // Yön seçimi (DirectionIndicator tıklandığında çağrılır)
    // -----------------------------------------------------------------------

    public void OnDirectionSelected(Vector2Int dir)
    {
        if (!hasPending) return;
        SpawnBall(pendingSpawn, dir);
        ClearIndicators();
        HideSelectedHalo();
        hasPending     = false;
        selectedCorner = null;
        spawnPhase     = SpawnPhase.CornerPick;
        OnStateChanged?.Invoke(state); // Başlat butonu görünürlüğünü güncelle
    }

    // -----------------------------------------------------------------------
    // Input
    // -----------------------------------------------------------------------

    void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        // ---- RegionSelect: hover her kare; tıkla → seç ----
        if (state == GameState.RegionSelect)
        {
            Vector3    world = ScreenToWorld(mouse.position.ReadValue());
            Vector2Int cell  = GridManager.Instance.WorldToGrid(world);

            GridManager.Instance.HoverRegionAtCell(cell.x, cell.y);

            if (mouse.leftButton.wasPressedThisFrame &&
                GridManager.Instance.SelectRegionAtCell(cell.x, cell.y))
            {
                SetState(GameState.SpawnSelect);
            }
            return;
        }

        // ---- SpawnSelect: iki fazlı — önce köşe, sonra yön ----
        if (state == GameState.SpawnSelect)
        {
            Vector3 world = ScreenToWorld(mouse.position.ReadValue());
            var (valid, corner) = GridManager.Instance.WorldToNearestPlayableCorner(world);

            // --- Faz A: CornerPick (hover + click-to-pick) ---
            if (spawnPhase == SpawnPhase.CornerPick)
            {
                // Top sayısı dolduysa hover'ı gizle; yine de tıklama no-op
                if (balls.Count >= maxBalls) { HideHoverHalo(); return; }

                if (valid)
                {
                    bool occupied = occupiedCorners.Contains(corner);
                    ShowHoverHalo(corner, occupied);

                    if (mouse.leftButton.wasPressedThisFrame && !occupied)
                    {
                        // Köşe seçildi → DirectionPick fazına geç
                        selectedCorner = corner;
                        spawnPhase     = SpawnPhase.DirectionPick;
                        HideHoverHalo();
                        ShowSelectedHalo(corner);
                        ShowCornerIndicators(corner);
                    }
                }
                else
                {
                    HideHoverHalo();
                }
                return;
            }

            // --- Faz B: DirectionPick (indicator click → spawn; boşluk → iptal) ---
            if (!mouse.leftButton.wasPressedThisFrame) return;

            foreach (var ind in dirIndicators)
            {
                if (ind == null) continue;
                if (Vector2.Distance(world, ind.transform.position) < clickRadius)
                {
                    var di = ind.GetComponent<DirectionIndicator>();
                    if (di != null) OnDirectionSelected(di.direction);
                    return;
                }
            }

            // Indicator'a değilse: iptal, CornerPick'e dön
            CancelDirectionPick();
        }
    }

    void CancelDirectionPick()
    {
        ClearIndicators();
        HideSelectedHalo();
        selectedCorner = null;
        spawnPhase     = SpawnPhase.CornerPick;
    }

    const float clickRadius = 0.40f;

    Vector3 ScreenToWorld(Vector2 screen)
        => Camera.main.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 0f));

    // -----------------------------------------------------------------------
    // Köşe Yön Göstergeleri
    // -----------------------------------------------------------------------

    void ShowCornerIndicators(Vector2Int corner)
    {
        ClearIndicators();
        pendingSpawn = corner;
        hasPending   = true;

        var validDirs = GridManager.Instance.GetValidCornerDirections(corner);
        if (validDirs.Count == 0) { hasPending = false; return; }

        Vector3 cw = GridManager.Instance.CornerToWorld(corner.x, corner.y);
        // Turuncu ton — selected halo'nun (mavi) zıt eşi, net ayrışsın.
        foreach (var d in validDirs)
            SpawnIndicatorAt(cw, d, new Color(1f, 0.55f, 0.15f, 0.95f));
    }

    void SpawnIndicatorAt(Vector3 center, Vector2Int d, Color color)
    {
        var pos = center + new Vector3(d.x * 0.52f, d.y * 0.52f, -0.5f);
        var go  = Instantiate(dirIndicatorPrefab, pos, Quaternion.identity);
        go.name = $"Ind_{d.x}_{d.y}";

        var di = go.AddComponent<DirectionIndicator>();
        di.direction = d;

        if (go.GetComponent<Collider2D>() == null)
            go.AddComponent<CircleCollider2D>().radius = 0.26f;

        var s = go.GetComponent<SpriteRenderer>();
        if (s != null) s.color = color;

        dirIndicators.Add(go);
    }

    void ClearIndicators()
    {
        foreach (var g in dirIndicators)
            if (g != null) Destroy(g);
        dirIndicators.Clear();
        hasPending = false;
    }

    // -----------------------------------------------------------------------
    // Hover / Selected köşe halo'ları
    // (Yeni sprite asset yok — dirIndicatorPrefab'ı reuse ederiz.)
    // -----------------------------------------------------------------------

    static readonly Color HaloValidColor    = new Color(1f,    0.90f, 0.25f, 0.55f); // sarı
    static readonly Color HaloOccupiedColor = new Color(1f,    0.25f, 0.25f, 0.55f); // kırmızı
    static readonly Color HaloSelectedColor = new Color(0.25f, 0.70f, 1f,    0.90f); // mavi

    GameObject EnsureHalo(GameObject existing, string name, float scale, int sortOrder)
    {
        if (existing != null) return existing;
        if (dirIndicatorPrefab == null) return null;

        var go = Instantiate(dirIndicatorPrefab);
        go.name = name;

        // Etkileşimsiz görsel: DirectionIndicator + Collider varsa kaldır
        var di = go.GetComponent<DirectionIndicator>();
        if (di != null) Destroy(di);
        var col = go.GetComponent<Collider2D>();
        if (col != null) Destroy(col);

        go.transform.localScale = new Vector3(scale, scale, 1f);

        var s = go.GetComponent<SpriteRenderer>();
        if (s != null) s.sortingOrder = sortOrder;

        return go;
    }

    void ShowHoverHalo(Vector2Int corner, bool occupied)
    {
        hoverHalo = EnsureHalo(hoverHalo, "HoverHalo", 0.45f, -1);
        if (hoverHalo == null) return;

        Vector3 cw = GridManager.Instance.CornerToWorld(corner.x, corner.y);
        hoverHalo.transform.position = new Vector3(cw.x, cw.y, -0.3f);

        var s = hoverHalo.GetComponent<SpriteRenderer>();
        if (s != null) s.color = occupied ? HaloOccupiedColor : HaloValidColor;
    }

    void HideHoverHalo()
    {
        if (hoverHalo != null) Destroy(hoverHalo);
        hoverHalo = null;
    }

    void ShowSelectedHalo(Vector2Int corner)
    {
        selectedHalo = EnsureHalo(selectedHalo, "SelectedHalo", 0.60f, 0);
        if (selectedHalo == null) return;

        Vector3 cw = GridManager.Instance.CornerToWorld(corner.x, corner.y);
        selectedHalo.transform.position = new Vector3(cw.x, cw.y, -0.4f);

        var s = selectedHalo.GetComponent<SpriteRenderer>();
        if (s != null) s.color = HaloSelectedColor;
    }

    void HideSelectedHalo()
    {
        if (selectedHalo != null) Destroy(selectedHalo);
        selectedHalo = null;
    }

    // -----------------------------------------------------------------------
    // Spawn — köşe tabanlı (BallController.InitAtCorner)
    // -----------------------------------------------------------------------

    void SpawnBall(Vector2Int corner, Vector2Int dir)
    {
        if (balls.Count >= maxBalls) return;

        // Geçersiz corner+dir kombinasyonu → hayalet top eklenmesin
        var validDirs = GridManager.Instance.GetValidCornerDirections(corner);
        if (!validDirs.Contains(dir))
        {
            Debug.LogWarning($"[GM] Spawn reddedildi — köşe:{corner} yön:{dir} geçersiz.");
            return;
        }

        var go = Instantiate(ballPrefab);
        go.name = $"Ball_{balls.Count}";
        go.transform.localScale = new Vector3(0.38f, 0.38f, 1f);

        var bc = go.GetComponent<BallController>();
        bc.speed = ballSpeed;
        bc.InitAtCorner(corner, dir, ballColors[balls.Count % ballColors.Length]);
        bc.SetPaused(true); // Başlat'a kadar bekle
        bc.OnStoppedAtCorner = OnBallStoppedAtCorner;

        balls.Add(bc);
        occupiedCorners.Add(corner); // Başlangıç köşesi artık dolu
        Debug.Log($"[GM] Top #{balls.Count} köşe:{corner} yön:{dir}");
    }

    void ClearBalls()
    {
        // Önce bilinen listeyi imha et
        foreach (var b in balls)
            if (b != null) Destroy(b.gameObject);
        balls.Clear();

        // Sonra orphan BallController'ları süpür (spawn sırası kesilmiş olabilir)
        foreach (var orphan in Object.FindObjectsByType<BallController>(FindObjectsSortMode.None))
            if (orphan != null) Destroy(orphan.gameObject);
    }

    // -----------------------------------------------------------------------
    // Çarpışma Tespiti — discrete pozisyon karşılaştırması
    // -----------------------------------------------------------------------

    void CheckBallCollisions()
    {
        for (int i = 0; i < balls.Count; i++)
        {
            if (balls[i] == null || !balls[i].IsMoving) continue;
            for (int j = i + 1; j < balls.Count; j++)
            {
                if (balls[j] == null || !balls[j].IsMoving) continue;
                if (balls[i].CurrentPosition != balls[j].CurrentPosition) continue;

                // Aynı konumdalar → yönleri swap et (elastik çarpışma)
                var dA = balls[i].Direction;
                var dB = balls[j].Direction;
                balls[i].Direction = dB;
                balls[j].Direction = dA;
                Debug.Log($"[Collision] Ball{i} ↔ Ball{j} @ {balls[i].CurrentPosition}");
            }
        }
    }

    // -----------------------------------------------------------------------
    // State Machine
    // -----------------------------------------------------------------------

    void SetState(GameState s)
    {
        state = s;
        switch (s)
        {
            case GameState.Drawing:
                GridManager.Instance.SetDrawEnabled(true);
                PauseAllBalls(true);
                break;
            case GameState.RegionSelect:
                GridManager.Instance.SetDrawEnabled(false);
                break;
            case GameState.SpawnSelect:
                GridManager.Instance.SetDrawEnabled(false);
                PauseAllBalls(true);
                // Her girişte temiz başla
                spawnPhase     = SpawnPhase.CornerPick;
                selectedCorner = null;
                HideSelectedHalo();
                break;
            case GameState.Simulating:
                GridManager.Instance.SetDrawEnabled(false);
                PauseAllBalls(false);
                // Toplar köşeleri terk ediyor — tek kaynak hakikat burada.
                occupiedCorners.Clear();
                break;
            case GameState.Paused:
                GridManager.Instance.SetDrawEnabled(false);
                PauseAllBalls(true);
                break;
        }
        OnStateChanged?.Invoke(s);
    }

    void PauseAllBalls(bool p)
    {
        foreach (var b in balls) if (b != null) b.SetPaused(p);
    }

    void LateUpdate()
    {
        balls.RemoveAll(b => b == null);
        // Ball-ball çarpışma Faz 1'de devre dışı — discrete pozisyon eşitliği unreliable.
        // Faz 5'te continuous modelde (pairwise swept sphere-sphere) yeniden yazılacak.
    }
}
