using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum GameState
{
    Drawing,        // Oyuncu hücre çiziyor
    SpawnSelect,    // Spawn noktası / yön seçiliyor
    Simulating,     // Top(lar) hareket ediyor
    Paused          // Durduruldu
}

/// <summary>
/// Oyun durumu yönetimi, top spawn, yön seçimi göstergeleri.
/// UIManager buradaki public metodları çağırır.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Prefabs")]
    public GameObject ballPrefab;
    public GameObject dirIndicatorPrefab; // Küçük yön göstergesi

    [Header("Settings")]
    public float ballSpeed = 4f;
    public int   maxBalls  = 1; // TEST: geçici olarak 1

    [Header("Ball Colors")]
    public Color[] ballColors =
    {
        new Color(1f, 0.92f, 0.2f),
        new Color(1f, 0.35f, 0.35f),
        new Color(0.35f, 1f, 0.9f)
    };

    // --- State ---
    GameState state;
    List<BallController> balls         = new List<BallController>();
    List<GameObject>     dirIndicators = new List<GameObject>();

    Vector2Int pendingCell;
    bool       hasPendingCell;

    public GameState State     => state;
    public int       BallCount => balls.Count;

    public System.Action<GameState> OnStateChanged;

    // -----------------------------------------------------------------------
    void Awake() => Instance = this;
    void Start()  => SetState(GameState.Drawing);

    // -----------------------------------------------------------------------
    // UIManager → GameManager köprüsü
    // -----------------------------------------------------------------------

    /// <summary>Çizim tamamlandı, spawn moduna geç.</summary>
    public void OnConfirmShape()
    {
        if (state != GameState.Drawing) return;
        if (!GridManager.Instance.HasSelection()) return;
        SetState(GameState.SpawnSelect);
    }

    /// <summary>Simülasyon sırasında ekstra top ekle.</summary>
    public void OnAddBall()
    {
        if (state != GameState.Simulating && state != GameState.Paused) return;
        if (balls.Count >= maxBalls) return;
        SetState(GameState.SpawnSelect);
    }

    /// <summary>Duraklat / Devam et.</summary>
    public void OnTogglePause()
    {
        if (state == GameState.Simulating) SetState(GameState.Paused);
        else if (state == GameState.Paused) SetState(GameState.Simulating);
    }

    /// <summary>Her şeyi sıfırla.</summary>
    public void OnReset()
    {
        ClearBalls();
        ClearDirIndicators();
        GridManager.Instance.Clear();
        hasPendingCell = false;
        SetState(GameState.Drawing);
    }

    public void OnClearShape()
    {
        if (state == GameState.Drawing)
            GridManager.Instance.Clear();
    }

    // -----------------------------------------------------------------------
    // Yön seçimi (DirectionIndicator bileşeni bu metodu çağırır)
    // -----------------------------------------------------------------------

    public void OnDirectionSelected(Vector2Int dir)
    {
        if (!hasPendingCell) return;

        SpawnBall(pendingCell, dir);
        ClearDirIndicators();
        hasPendingCell = false;

        if (balls.Count > 0)
            SetState(GameState.Simulating);
    }

    // -----------------------------------------------------------------------
    // Input — SpawnSelect modunda hücre tıklaması
    // -----------------------------------------------------------------------

    void Update()
    {
        if (state != GameState.SpawnSelect) return;

        var mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;

        Vector2 screenPos = mouse.position.ReadValue();
        var worldPos      = Camera.main.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 0f));

        foreach (var ind in dirIndicators)
        {
            if (ind == null) continue;
            if (Vector2.Distance(worldPos, ind.transform.position) < cellWorldRadius)
            {
                // OnMouseDown yeni Input System ile çalışmaz; tıklamayı burada işle
                var di = ind.GetComponent<DirectionIndicator>();
                if (di != null) OnDirectionSelected(di.direction);
                return;
            }
        }

        // Yeni bir spawn hücresi seç
        var cell = GridManager.Instance.WorldToGrid(worldPos);
        if (GridManager.Instance.IsSelected(cell.x, cell.y))
            ShowDirIndicators(cell);
    }

    const float cellWorldRadius = 0.35f;

    // -----------------------------------------------------------------------
    // Yön Göstergeleri
    // -----------------------------------------------------------------------

    void ShowDirIndicators(Vector2Int cell)
    {
        ClearDirIndicators();
        pendingCell    = cell;
        hasPendingCell = true;

        var dirs = new Vector2Int[]
        {
            new( 1,  1),
            new( 1, -1),
            new(-1,  1),
            new(-1, -1)
        };

        var cellCenter = GridManager.Instance.GridToWorld(cell.x, cell.y);

        foreach (var d in dirs)
        {
            // Yalnızca o yönde geçerli hücre varsa göster
            Vector2Int neighbor = cell + d;
            if (!GridManager.Instance.IsSelected(neighbor.x, neighbor.y)) continue;

            // Göstergeyi hücre merkezi ile hedef arasına yerleştir
            var indicatorPos = cellCenter + new Vector3(d.x * 0.55f, d.y * 0.55f, -0.5f);

            var go = Instantiate(dirIndicatorPrefab, indicatorPos, Quaternion.identity);
            go.name = $"DirInd_{d.x}_{d.y}";

            // DirectionIndicator bileşenini ekle
            var di = go.AddComponent<DirectionIndicator>();
            di.direction = d;

            // Collider2D gerekli (OnMouseDown için)
            if (go.GetComponent<Collider2D>() == null)
            {
                var col = go.AddComponent<CircleCollider2D>();
                col.radius = 0.28f;
            }

            var sr = go.GetComponent<SpriteRenderer>();
            if (sr) sr.color = new Color(1f, 0.9f, 0.3f, 0.85f);

            dirIndicators.Add(go);
        }

        // Seçilen hücrede hiç geçerli yön yoksa (çok küçük şekil) tüm 4 yönü göster
        if (dirIndicators.Count == 0)
        {
            foreach (var d in dirs)
            {
                var indicatorPos = cellCenter + new Vector3(d.x * 0.55f, d.y * 0.55f, -0.5f);
                var go = Instantiate(dirIndicatorPrefab, indicatorPos, Quaternion.identity);
                var di = go.AddComponent<DirectionIndicator>();
                di.direction = d;

                if (go.GetComponent<Collider2D>() == null)
                {
                    var col = go.AddComponent<CircleCollider2D>();
                    col.radius = 0.28f;
                }

                var sr = go.GetComponent<SpriteRenderer>();
                if (sr) sr.color = new Color(1f, 0.5f, 0.3f, 0.85f); // turuncu = bounce gerekecek

                dirIndicators.Add(go);
            }
        }
    }

    void ClearDirIndicators()
    {
        foreach (var g in dirIndicators)
            if (g != null) Destroy(g);
        dirIndicators.Clear();
        hasPendingCell = false;
    }

    // -----------------------------------------------------------------------
    // Spawn
    // -----------------------------------------------------------------------

    void SpawnBall(Vector2Int cell, Vector2Int dir)
    {
        if (balls.Count >= maxBalls) return;

        var go = Instantiate(ballPrefab);
        go.name = $"Ball_{balls.Count}";
        go.transform.position = new Vector3(0, 0, -1f);

        var bc = go.GetComponent<BallController>();
        bc.speed = ballSpeed;
        bc.Init(cell, dir, ballColors[balls.Count % ballColors.Length]);

        if (state == GameState.Paused)
            bc.SetPaused(true);

        balls.Add(bc);
    }

    void ClearBalls()
    {
        foreach (var b in balls)
            if (b != null) Destroy(b.gameObject);
        balls.Clear();
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

            case GameState.SpawnSelect:
                GridManager.Instance.SetDrawEnabled(false);
                // Toplar önceki duraklama durumunu korur
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
        foreach (var b in balls)
            if (b != null) b.SetPaused(p);
    }

    void LateUpdate()
    {
        // Destroy edilmiş topları listeden temizle
        balls.RemoveAll(b => b == null);
    }
}
