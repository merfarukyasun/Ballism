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
    GameState            state;
    List<BallController> balls         = new List<BallController>();
    List<GameObject>     dirIndicators = new List<GameObject>();

    Vector2Int pendingSpawn;  // köşe koordinatı (corner space)
    bool       hasPending;

    public GameState State     => state;
    public int       BallCount => balls.Count;
    public System.Action<GameState> OnStateChanged;

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
        if (balls.Count >= maxBalls) return;
        SetState(GameState.SpawnSelect);
    }

    public void OnTogglePause()
    {
        if (state == GameState.Simulating) SetState(GameState.Paused);
        else if (state == GameState.Paused) SetState(GameState.Simulating);
    }

    public void OnReset()
    {
        ClearBalls();
        ClearIndicators();
        GridManager.Instance.Clear();
        hasPending = false;
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
        hasPending = false;
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

        // ---- SpawnSelect: köşeye tıkla → yön göstergeleri; oka tıkla → top ----
        if (state == GameState.SpawnSelect)
        {
            if (!mouse.leftButton.wasPressedThisFrame) return;

            Vector3 world = ScreenToWorld(mouse.position.ReadValue());

            // Önce yön göstergelerine bak
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

            // Top sayısı dolduysa sadece var olan toplara giriş kabul et
            if (balls.Count >= maxBalls) return;

            // Köşeye snap
            var (valid, corner) = GridManager.Instance.WorldToNearestPlayableCorner(world);
            if (valid) ShowCornerIndicators(corner);
        }
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
        foreach (var d in validDirs)
            SpawnIndicatorAt(cw, d, new Color(1f, 0.92f, 0.28f, 0.9f));
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

        balls.Add(bc);
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
                break;
            case GameState.Simulating:
                GridManager.Instance.SetDrawEnabled(false);
                PauseAllBalls(false);
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
