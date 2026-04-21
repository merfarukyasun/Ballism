using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;
using TMPro;
using UnityEngine.InputSystem.UI;

public class BallismSetup : EditorWindow
{
    /// <summary>
    /// Mevcut açık sahnedeki StandaloneInputModule'ü kaldırır,
    /// yerine InputSystemUIInputModule ekler.
    /// Setup Scene çalıştırılmadan önce kurulmuş sahneleri düzeltmek için kullan.
    /// </summary>
    [MenuItem("Ballism/Fix EventSystem (Input System)")]
    static void FixEventSystem()
    {
        var es = Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
        if (es == null)
        {
            Debug.LogError("[BallismSetup] Sahnede EventSystem bulunamadı.");
            return;
        }

        bool changed = false;

        var legacy = es.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        if (legacy != null)
        {
            Object.DestroyImmediate(legacy);
            Debug.Log("[BallismSetup] StandaloneInputModule kaldırıldı.");
            changed = true;
        }
        else
        {
            Debug.Log("[BallismSetup] StandaloneInputModule zaten yoktu.");
        }

        if (es.GetComponent<InputSystemUIInputModule>() == null)
        {
            es.gameObject.AddComponent<InputSystemUIInputModule>();
            Debug.Log("[BallismSetup] InputSystemUIInputModule eklendi.");
            changed = true;
        }
        else
        {
            Debug.Log("[BallismSetup] InputSystemUIInputModule zaten vardı.");
        }

        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[BallismSetup] EventSystem düzeltildi — Ctrl+S ile sahneyi kaydet.");
        }

        EditorUtility.DisplayDialog("Ballism Fix EventSystem",
            changed
                ? "EventSystem düzeltildi!\nCtrl+S ile sahneyi kaydetmeyi unutma."
                : "Değiştirilecek bir şey bulunamadı.\nEventSystem zaten doğru yapılandırılmış.",
            "Tamam");
    }

    [MenuItem("Ballism/Setup Scene")]
    static void SetupScene()
    {
        const string prefabDir = "Assets/Prefabs";
        if (!AssetDatabase.IsValidFolder(prefabDir))
            AssetDatabase.CreateFolder("Assets", "Prefabs");

        // --- Sprite asset'leri ---
        SaveTexture(CreateSolidTexture(64, 64, Color.white), "Assets/Prefabs/SquareTex.png");
        SaveTexture(CreateCircleTexture(64, Color.white),    "Assets/Prefabs/CircleTex.png");
        SaveTexture(CreateDiamondTexture(64, Color.white),   "Assets/Prefabs/DiamondTex.png");
        AssetDatabase.Refresh();

        Sprite squareSprite  = LoadSprite("Assets/Prefabs/SquareTex.png");
        Sprite circleSprite  = LoadSprite("Assets/Prefabs/CircleTex.png");
        Sprite diamondSprite = LoadSprite("Assets/Prefabs/DiamondTex.png");

        // --- Prefablar ---
        GameObject cellPrefab = BuildAndSavePrefab("CellPrefab", prefabDir, go =>
        {
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = squareSprite;
            go.transform.localScale = new Vector3(0.92f, 0.92f, 1f);
        });

        GameObject ballPrefab = BuildAndSavePrefab("BallPrefab", prefabDir, go =>
        {
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = circleSprite;
            sr.sortingOrder = 2;
            go.transform.localScale = new Vector3(0.55f, 0.55f, 1f);
            go.AddComponent<BallController>();
        });

        GameObject indPrefab = BuildAndSavePrefab("DirIndicatorPrefab", prefabDir, go =>
        {
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = diamondSprite;
            sr.sortingOrder = 3;
            go.transform.localScale = new Vector3(0.38f, 0.38f, 1f);
        });

        // ----------------------------------------------------------------
        // 1. Main Camera
        // ----------------------------------------------------------------
        GameObject camGO = GameObject.Find("Main Camera");
        if (camGO == null)
            camGO = new GameObject("Main Camera");

        camGO.tag = "MainCamera";

        Camera cam = camGO.GetComponent<Camera>();
        if (cam == null)
            cam = camGO.AddComponent<Camera>();

        cam.orthographic     = true;
        cam.orthographicSize = 6.5f;
        cam.backgroundColor  = new Color(0.06f, 0.07f, 0.09f);
        cam.clearFlags       = CameraClearFlags.SolidColor;
        camGO.transform.position = new Vector3(5f, 5f, -10f);

        if (camGO.GetComponent<AudioListener>() == null)
            camGO.AddComponent<AudioListener>();

        // ----------------------------------------------------------------
        // 2. GridManager
        // ----------------------------------------------------------------
        GameObject gridGO = GameObject.Find("GridManager");
        if (gridGO == null)
            gridGO = new GameObject("GridManager");

        GridManager gridMgr = gridGO.GetComponent<GridManager>();
        if (gridMgr == null)
            gridMgr = gridGO.AddComponent<GridManager>();

        gridMgr.cellPrefab = cellPrefab;

        // ----------------------------------------------------------------
        // 3. GameManager
        // ----------------------------------------------------------------
        GameObject gmGO = GameObject.Find("GameManager");
        if (gmGO == null)
            gmGO = new GameObject("GameManager");

        GameManager gm = gmGO.GetComponent<GameManager>();
        if (gm == null)
            gm = gmGO.AddComponent<GameManager>();

        gm.ballPrefab         = ballPrefab;
        gm.dirIndicatorPrefab = indPrefab;

        // ----------------------------------------------------------------
        // 4. UIManager (Canvas içinde)
        // ----------------------------------------------------------------
        GameObject canvasGO = GameObject.Find("UI Canvas");
        if (canvasGO == null)
            canvasGO = new GameObject("UI Canvas");

        Canvas canvas = canvasGO.GetComponent<Canvas>();
        if (canvas == null)
            canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        if (canvasGO.GetComponent<CanvasScaler>() == null)
        {
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
        }

        if (canvasGO.GetComponent<GraphicRaycaster>() == null)
            canvasGO.AddComponent<GraphicRaycaster>();

        // EventSystem — yeni Input System ile uyumlu modül
        if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGO.AddComponent<InputSystemUIInputModule>();
        }

        // UIManager objesi
        GameObject uiMgrGO = canvasGO.transform.Find("UIManager")?.gameObject;
        if (uiMgrGO == null)
        {
            uiMgrGO = new GameObject("UIManager");
            uiMgrGO.transform.SetParent(canvasGO.transform, false);
        }

        UIManager uiMgr = uiMgrGO.GetComponent<UIManager>();
        if (uiMgr == null)
            uiMgr = uiMgrGO.AddComponent<UIManager>();

        // Önceki Ballism UI nesnelerini temizle — Setup Scene tekrar çalıştırma güvenliği
        CleanupExistingBallismUI(canvasGO.transform);

        // Sağ panel
        RectTransform panel = CreateUIPanel(canvasGO.transform, "Panel",
            new Vector2(200, 460), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(-110, 0));

        uiMgr.confirmShapeButton = CreateButton(panel.transform, "BtnConfirm", "Onayla",   new Vector2(0,  180));
        uiMgr.clearShapeButton   = CreateButton(panel.transform, "BtnClear",   "Temizle",  new Vector2(0,  130));
        uiMgr.togglePauseButton  = CreateButton(panel.transform, "BtnPause",   "Durdur",   new Vector2(0,   80));
        uiMgr.addBallButton      = CreateButton(panel.transform, "BtnAddBall", "Top Ekle", new Vector2(0,   30));
        uiMgr.startButton        = CreateButton(panel.transform, "BtnStart",   "Başlat",   new Vector2(0,  -20));
        uiMgr.resetButton        = CreateButton(panel.transform, "BtnReset",   "Sıfırla",  new Vector2(0,  -70));

        uiMgr.stateLabel     = CreateLabel(panel.transform, "LblState",  "",         new Vector2(0, -125), 16, FontStyles.Bold);
        uiMgr.ballCountLabel = CreateLabel(panel.transform, "LblBalls",  "Top: 0/3", new Vector2(0, -155), 14);

        GameObject instrGO = new GameObject("LblInstruction");
        instrGO.transform.SetParent(canvasGO.transform, false);
        var instrRect = instrGO.AddComponent<RectTransform>();
        instrRect.anchorMin = new Vector2(0f, 0f);
        instrRect.anchorMax = new Vector2(1f, 0f);
        instrRect.offsetMin = new Vector2(10f, 10f);
        instrRect.offsetMax = new Vector2(-10f, 50f);
        var instrTmp = instrGO.AddComponent<TextMeshProUGUI>();
        instrTmp.fontSize  = 13;
        instrTmp.alignment = TextAlignmentOptions.Center;
        instrTmp.color     = new Color(0.8f, 0.85f, 1f);
        uiMgr.instructionLabel = instrTmp;

        EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        Debug.Log("[BallismSetup] Kurulum tamamlandı. Sahneyi Ctrl+S ile kaydet.");
        EditorUtility.DisplayDialog("Ballism Setup",
            "Kurulum tamamlandı!\nSahneyi Ctrl+S ile kaydetmeyi unutma.", "Tamam");
    }

    // -----------------------------------------------------------------------
    /// <summary>
    /// Canvas altındaki Ballism tarafından oluşturulmuş UI nesnelerini temizler.
    /// Yalnızca isim + component + bilinen child kontrolüyle hedeflenir — agresif değil.
    /// İleride eklenen başka Panel / Label objeleri dokunulmaz.
    /// </summary>
    static void CleanupExistingBallismUI(Transform canvasTransform)
    {
        for (int i = canvasTransform.childCount - 1; i >= 0; i--)
        {
            var child = canvasTransform.GetChild(i);

            // Ballism sağ paneli:
            //   • ad == "Panel"
            //   • Image component'i var (panel arka planı)
            //   • Ballism'e özgü child'lardan en az biri mevcut (BtnConfirm veya LblState)
            if (child.name == "Panel"
                && child.GetComponent<Image>() != null
                && (child.Find("BtnConfirm") != null || child.Find("LblState") != null))
            {
                Object.DestroyImmediate(child.gameObject);
                continue;
            }

            // Ballism alt yönerge etiketi:
            //   • ad == "LblInstruction"
            //   • TextMeshProUGUI component'i var
            //   • Tam-genişlik alt-anchor: anchorMin=(0,0), anchorMax=(1,0)
            if (child.name == "LblInstruction")
            {
                var rt  = child.GetComponent<RectTransform>();
                var tmp = child.GetComponent<TextMeshProUGUI>();
                if (tmp != null && rt != null
                    && Mathf.Approximately(rt.anchorMin.x, 0f)
                    && Mathf.Approximately(rt.anchorMax.x, 1f)
                    && Mathf.Approximately(rt.anchorMin.y, 0f)
                    && Mathf.Approximately(rt.anchorMax.y, 0f))
                {
                    Object.DestroyImmediate(child.gameObject);
                    continue;
                }
            }
        }
    }

    // -----------------------------------------------------------------------
    // Texture yardımcıları
    // -----------------------------------------------------------------------

    static Texture2D CreateSolidTexture(int w, int h, Color col)
    {
        var tex    = new Texture2D(w, h);
        var pixels = new Color[w * h];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = col;
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    static Texture2D CreateCircleTexture(int size, Color col)
    {
        var tex    = new Texture2D(size, size);
        float cx   = size / 2f;
        float cy   = size / 2f;
        float r    = size / 2f - 1f;
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = x - cx, dy = y - cy;
            float a  = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy) + 1f);
            pixels[y * size + x] = new Color(col.r, col.g, col.b, a);
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    static Texture2D CreateDiamondTexture(int size, Color col)
    {
        var tex    = new Texture2D(size, size);
        float half = size / 2f;
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = Mathf.Abs(x - half) / half;
            float dy = Mathf.Abs(y - half) / half;
            float a  = (dx + dy) <= 1f ? 1f : 0f;
            pixels[y * size + x] = new Color(col.r, col.g, col.b, a);
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    static void SaveTexture(Texture2D tex, string path)
    {
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }

    static Sprite LoadSprite(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType         = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 64;
            importer.alphaIsTransparency = true;
            importer.filterMode          = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static GameObject BuildAndSavePrefab(string name, string dir,
        System.Action<GameObject> setup)
    {
        var go = new GameObject(name);
        setup(go);
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{dir}/{name}.prefab");
        Object.DestroyImmediate(go);
        return prefab;
    }

    // -----------------------------------------------------------------------
    // UI yardımcıları
    // -----------------------------------------------------------------------

    static RectTransform CreateUIPanel(Transform parent, string name,
        Vector2 size, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos)
    {
        var go   = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin        = anchorMin;
        rect.anchorMax        = anchorMax;
        rect.sizeDelta        = size;
        rect.anchoredPosition = anchoredPos;
        var img  = go.AddComponent<Image>();
        img.color = new Color(0.08f, 0.10f, 0.14f, 0.88f);
        return rect;
    }

    static Button CreateButton(Transform parent, string name, string label, Vector2 pos)
    {
        var go   = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta        = new Vector2(170, 38);
        rect.anchoredPosition = pos;

        var img = go.AddComponent<Image>();
        img.color = new Color(0.18f, 0.32f, 0.62f);

        var btn = go.AddComponent<Button>();
        var cs  = btn.colors;
        cs.highlightedColor = new Color(0.28f, 0.48f, 0.85f);
        cs.pressedColor     = new Color(0.12f, 0.22f, 0.45f);
        btn.colors = cs;

        var txtGO = new GameObject("Text");
        txtGO.transform.SetParent(go.transform, false);
        var txtRect = txtGO.AddComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = Vector2.zero;
        txtRect.offsetMax = Vector2.zero;
        var tmp = txtGO.AddComponent<TextMeshProUGUI>();
        tmp.text      = label;
        tmp.fontSize  = 14;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = Color.white;

        return btn;
    }

    static TextMeshProUGUI CreateLabel(Transform parent, string name, string text,
        Vector2 pos, float size, FontStyles style = FontStyles.Normal)
    {
        var go   = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta        = new Vector2(180, 30);
        rect.anchoredPosition = pos;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = size;
        tmp.fontStyle = style;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = new Color(0.8f, 0.85f, 1f);
        return tmp;
    }
}
