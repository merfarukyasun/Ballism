using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum BounceType
{
    Free,          // Engel yok, düz ilerledi
    FlatWallH,     // Yatay duvar → Y yansıdı  (hOpen && !vOpen)
    FlatWallV,     // Dikey duvar  → X yansıdı  (!hOpen && vOpen)
    ConvexCorner,  // Çıkıntı köşe → %33 üç yol (hOpen && vOpen)
    ConcaveCorner  // Çukur köşe  → tam geri    (!hOpen && !vOpen)
}

/// <summary>
/// 10x10 grid oluşturur. Çizim modunda drag-to-paint ile hücre seçimini yönetir.
/// Ayrıca BallController için sekme (bounce) hesabı sağlar.
/// </summary>
public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }

    [Header("Grid")]
    public int width  = 10;
    public int height = 10;
    public float cellSize = 1f;

    [Header("Prefab")]
    public GameObject cellPrefab;

    [Header("Colors")]
    public Color colorEmpty    = new Color(0.10f, 0.12f, 0.16f);
    public Color colorSelected = new Color(0.25f, 0.45f, 0.85f);

    // --- Private state ---
    bool[,]           cells;
    SpriteRenderer[,] renderers;

    bool      drawEnabled = true;
    bool      isDragging;
    bool      dragValue;
    Vector2Int lastDrag = new Vector2Int(-1, -1);

    // -----------------------------------------------------------------------
    void Awake()
    {
        Instance  = this;
        cells     = new bool[width, height];
        renderers = new SpriteRenderer[width, height];
    }

    void Start() => BuildGrid();

    void BuildGrid()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                var pos = new Vector3(x * cellSize + cellSize * 0.5f,
                                      y * cellSize + cellSize * 0.5f, 0f);
                var go = Instantiate(cellPrefab, pos, Quaternion.identity, transform);
                go.name = $"Cell_{x}_{y}";

                var gc = go.AddComponent<GridCell>();
                gc.gridX = x;
                gc.gridY = y;

                renderers[x, y] = go.GetComponent<SpriteRenderer>();
                RefreshVisual(x, y);
            }
        }
    }

    // -----------------------------------------------------------------------
    void Update()
    {
        if (!drawEnabled) return;

        var mouse = Mouse.current;
        if (mouse == null) return;

        if (mouse.leftButton.wasPressedThisFrame)
        {
            var c = MouseCell(mouse);
            if (c.HasValue)
            {
                isDragging = true;
                dragValue  = !cells[c.Value.x, c.Value.y];
                SetCell(c.Value.x, c.Value.y, dragValue);
                lastDrag = c.Value;
            }
        }

        if (mouse.leftButton.isPressed && isDragging)
        {
            var c = MouseCell(mouse);
            if (c.HasValue && c.Value != lastDrag)
            {
                SetCell(c.Value.x, c.Value.y, dragValue);
                lastDrag = c.Value;
            }
        }

        if (mouse.leftButton.wasReleasedThisFrame) isDragging = false;
    }

    // -----------------------------------------------------------------------
    void SetCell(int x, int y, bool value)
    {
        if (cells[x, y] == value) return;
        cells[x, y] = value;
        RefreshVisual(x, y);
    }

    void RefreshVisual(int x, int y)
    {
        if (renderers[x, y] != null)
            renderers[x, y].color = cells[x, y] ? colorSelected : colorEmpty;
    }

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    public bool IsSelected(int x, int y)
    {
        if (x < 0 || x >= width || y < 0 || y >= height) return false;
        return cells[x, y];
    }

    public void SetDrawEnabled(bool v) => drawEnabled = v;

    public bool HasSelection()
    {
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                if (cells[x, y]) return true;
        return false;
    }

    public void Clear()
    {
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                SetCell(x, y, false);
    }

    public Vector3 GridToWorld(int x, int y)
        => new Vector3(x * cellSize + cellSize * 0.5f,
                       y * cellSize + cellSize * 0.5f, 0f);

    public Vector2Int WorldToGrid(Vector3 world)
        => new Vector2Int(Mathf.FloorToInt(world.x / cellSize),
                          Mathf.FloorToInt(world.y / cellSize));

    // -----------------------------------------------------------------------
    // Bounce Resolution
    // -----------------------------------------------------------------------

    /// <summary>
    /// Topu <pos> hücresinde, <dir> yönünde hareket ettirmeye çalışıyoruz.
    /// Hedef hücre boşsa sekme kuralını uygulayarak yeni yönü döndürür.
    ///
    /// Koordinat mantığı:
    ///   horiz = pos + (dir.x, 0)  →  yatay komşu
    ///   vert  = pos + (0, dir.y)  →  dikey komşu
    ///
    ///   hOpen=true, vOpen=false : dikey duvar var, Y yansıtılır (yatay duvar sekeği)
    ///   hOpen=false, vOpen=true : yatay duvar var, X yansıtılır (dikey duvar sekeği)
    ///   hOpen && vOpen          : çıkıntı köşe, %33 üç yol
    ///   !hOpen && !vOpen        : çukur köşe, geri döner
    /// </summary>
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
            // Çıkıntı köşe — %33.3 üç seçenek
            float r = Random.value;
            if      (r < 1f / 3f) newDir = new Vector2Int(-dir.x,  dir.y); // X yansıt
            else if (r < 2f / 3f) newDir = new Vector2Int( dir.x, -dir.y); // Y yansıt
            else                  newDir = -dir;                            // geri
            type = BounceType.ConvexCorner;
        }
        else if (hOpen)
        {
            newDir = new Vector2Int(dir.x, -dir.y); // Y yansıt
            type   = BounceType.FlatWallH;
        }
        else if (vOpen)
        {
            newDir = new Vector2Int(-dir.x, dir.y); // X yansıt
            type   = BounceType.FlatWallV;
        }
        else
        {
            newDir = -dir; // çukur köşe → tam geri
            type   = BounceType.ConcaveCorner;
        }

        return (newDir, type);
    }

    // -----------------------------------------------------------------------
    Vector2Int? MouseCell(Mouse mouse)
    {
        Vector2 screenPos = mouse.position.ReadValue();
        Vector3 world     = Camera.main.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 0f));
        int x = Mathf.FloorToInt(world.x / cellSize);
        int y = Mathf.FloorToInt(world.y / cellSize);
        if (x >= 0 && x < width && y >= 0 && y < height)
            return new Vector2Int(x, y);
        return null;
    }
}
