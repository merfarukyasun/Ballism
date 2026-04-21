using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Canvas UI — buton görünürlüğü, durum etiketi, yönergeler.
/// Faz 2.5: startButton, SpawnSelect akışı, köşe tabanlı spawn yönergesi.
/// Camera+Sandbox Controls pass:
///   • Runtime'da alt-orta kontrol barı (pause / reset / settings)
///   • Settings placeholder paneli
///   • Space kısayolu ile pause/resume
///   • Main Camera'ya CameraController ekleme (lazy attach)
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("Butonlar")]
    public Button confirmShapeButton; // Drawing
    public Button clearShapeButton;   // Drawing
    public Button startButton;        // SpawnSelect (en az 1 top varken)
    public Button togglePauseButton;  // Simulating / Paused — sahne butonu (alt bar görünürken gizlenir)
    public Button addBallButton;      // Simulating / Paused (maxBalls dolmadıysa)
    public Button resetButton;        // Drawing hariç her state — alt bar görünürken gizlenir

    [Header("Etiketler")]
    public TextMeshProUGUI stateLabel;
    public TextMeshProUGUI ballCountLabel;
    public TextMeshProUGUI instructionLabel;

    // -----------------------------------------------------------------------
    // Runtime-built alt bar (Simulating/Paused'da görünür)
    GameObject       bottomBar;
    Button           centerPauseButton;
    TextMeshProUGUI  centerPauseLabel;
    Button           leftSettingsButton;
    Button           rightResetButton;

    GameObject       settingsPanel;
    bool             settingsOpen;

    void Start()
    {
        confirmShapeButton?.onClick.AddListener(GameManager.Instance.OnConfirmShape);
        clearShapeButton  ?.onClick.AddListener(GameManager.Instance.OnClearShape);
        startButton       ?.onClick.AddListener(GameManager.Instance.OnStartSimulation);
        togglePauseButton ?.onClick.AddListener(GameManager.Instance.OnTogglePause);
        addBallButton     ?.onClick.AddListener(GameManager.Instance.OnAddBall);
        resetButton       ?.onClick.AddListener(GameManager.Instance.OnReset);

        // startButton sahnede yoksa simülasyon başlatılamaz — kullanıcıyı uyar.
        // Buton referansının null kalması güvenlidir: Update ve RefreshUI'daki tüm
        // startButton erişimleri null-conditional (?.) veya explicit null-check ile
        // korunur; UI akışı bozulmaz, yalnızca "Başlat" butonu görünmez.
        if (startButton == null)
            Debug.LogWarning("[UI] ⚠ startButton referansı yok — sahnede eksik. " +
                             "Ballism/Setup Scene menüsünü yeniden çalıştır veya " +
                             "UIManager.startButton alanına Button sürükle.");

        // --- Duplicate panel temizliği (Setup Scene tekrar çalıştırıldıysa orphan kalabilir) ---
        CleanupOrphanPanels();

        // --- Camera controller lazy attach (sahne dosyası değişmez) ---
        AttachCameraController();

        // --- Runtime alt bar + settings panel (sahne dosyası değişmez) ---
        BuildRuntimeUI();

        // --- EventSystem sanity check (UI-over detection için gerekli) ---
        if (EventSystem.current == null)
            Debug.LogWarning("[UI] ⚠ EventSystem sahnede yok — UI-over guard'ları " +
                             "çalışmayabilir (scroll/right-click UI üzerinde de kamerayı etkiler).");

        GameManager.Instance.OnStateChanged += RefreshUI;
        RefreshUI(GameManager.Instance.State);
    }

    // -----------------------------------------------------------------------
    void AttachCameraController()
    {
        var cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("[UI] ⚠ Camera.main bulunamadı — kamera kontrolleri devre dışı.");
            return;
        }
        if (cam.GetComponent<CameraController>() == null)
            cam.gameObject.AddComponent<CameraController>();
    }

    // -----------------------------------------------------------------------
    /// <summary>
    /// Canvas altında kalan orphan Ballism panel'lerini temizler.
    /// Sadece isim + Image + bilinen Ballism child (BtnConfirm veya LblState) üçlüsüyle
    /// hedeflenir; kendi panel'imize (confirmShapeButton.parent) dokunulmaz.
    /// </summary>
    void CleanupOrphanPanels()
    {
        if (confirmShapeButton == null) return;
        Transform myPanel = confirmShapeButton.transform.parent;
        if (myPanel == null) return;

        Canvas canvas = myPanel.GetComponentInParent<Canvas>();
        if (canvas == null) return;

        for (int i = canvas.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = canvas.transform.GetChild(i);
            if (child.name == "Panel"
                && child != myPanel
                && child.GetComponent<Image>() != null
                && (child.Find("BtnConfirm") != null || child.Find("LblState") != null))
            {
                Debug.Log($"[UI] Orphan Ballism panel temizleniyor — childCount:{child.childCount}");
                Destroy(child.gameObject);
            }
        }
    }

    // -----------------------------------------------------------------------
    void Update()
    {
        var gm = GameManager.Instance;

        // --- Space: pause/resume kısayolu ---
        // GameManager.OnTogglePause idempotent:
        //   • Simulating + pauseRequested zaten true → no-op (spam güvenli)
        //   • Paused → Resume
        //   • Diğer state'ler → guard ile no-op
        var kb = Keyboard.current;
        if (kb != null && kb.spaceKey.wasPressedThisFrame)
        {
            if (gm.State == GameState.Simulating || gm.State == GameState.Paused)
                gm.OnTogglePause();
        }

        // --- Merkez pause butonu etiketi (alt bar görünür durumdaysa) ---
        if (centerPauseLabel != null)
        {
            centerPauseLabel.text = gm.IsStopping
                ? "Durduruluyor…"
                : (gm.State == GameState.Paused ? "Devam" : "Durdur");
        }

        // Top sayısı etiketi (her kare)
        if (ballCountLabel != null)
        {
            int placed = gm.BallCount;
            int max    = gm.maxBalls;
            ballCountLabel.text = placed > 0
                ? $"Top: {placed} / {max}"
                : "";
        }

        // SpawnSelect — yönerge (iki fazlı) ve Başlat butonu dinamik
        if (gm.State == GameState.SpawnSelect)
        {
            if (instructionLabel != null)
            {
                int placed = gm.BallCount;
                int max    = gm.maxBalls;

                if (gm.IsPickingCorner)
                {
                    instructionLabel.text = placed == 0
                        ? "1) Geçerli bir köşeye tıkla (sarı = boş, kırmızı = dolu)."
                        : placed < max
                            ? $"{placed}/{max} top hazır.  Yeni köşe seç veya [Başlat]."
                            : $"{placed}/{max} top hazır.  [Başlat] ile simülasyonu başlat.";
                }
                else // DirectionPick
                {
                    instructionLabel.text = "2) Bir ok seç (top yerleşir)  •  " +
                                            "boş alana tıkla → iptal.";
                }
            }

            if (startButton != null)
                startButton.gameObject.SetActive(gm.BallCount > 0);
        }

        // Simulating — "DURDURULUYOR..." etiketi (pause in-flight)
        if (gm.State == GameState.Simulating && stateLabel != null)
        {
            stateLabel.text = gm.IsStopping ? "DURDURULUYOR…" : "SİMÜLASYON";
        }
    }

    // -----------------------------------------------------------------------
    void RefreshUI(GameState s)
    {
        var gm = GameManager.Instance;

        // ---- Durum etiketi ----
        if (stateLabel != null)
            stateLabel.text = s switch
            {
                GameState.Drawing      => "ÇİZİM",
                GameState.RegionSelect => "BÖLGE SEÇ",
                GameState.SpawnSelect  => "TOP YERLEŞTIR",
                GameState.Simulating   => "SİMÜLASYON",
                GameState.Paused       => "DURAKLATILDI",
                _                      => ""
            };

        // ---- Yönerge (SpawnSelect dinamik, Update'te güncelleniyor) ----
        if (instructionLabel != null && s != GameState.SpawnSelect)
            instructionLabel.text = s switch
            {
                GameState.Drawing      => "Kırmızı kenar çiz → kapalı alan → [Onayla]",
                GameState.RegionSelect => "Fareyi bölgenin üzerine getir → yeşil görünce tıkla",
                // Alt bar bu state'lerde kontrolü üstleniyor — alt kısımdaki label'ı boşalt
                GameState.Simulating   => "",
                GameState.Paused       => "",
                _                      => ""
            };

        // ---- Durdur / Devam etiketi ----
        if (togglePauseButton != null)
        {
            var lbl = togglePauseButton.GetComponentInChildren<TextMeshProUGUI>();
            if (lbl != null) lbl.text = s == GameState.Paused ? "Devam Et" : "Durdur";
        }

        // ---- Buton görünürlükleri ----
        bool drawing     = s == GameState.Drawing;
        bool spawnSelect = s == GameState.SpawnSelect;
        bool simOrPaused = s == GameState.Simulating || s == GameState.Paused;
        bool canPause    = simOrPaused;
        bool canAddBall  = simOrPaused && gm.BallCount < gm.maxBalls;
        bool canStart    = spawnSelect && gm.BallCount > 0;

        confirmShapeButton?.gameObject.SetActive(drawing);
        clearShapeButton  ?.gameObject.SetActive(drawing);
        startButton       ?.gameObject.SetActive(canStart);
        addBallButton     ?.gameObject.SetActive(canAddBall);

        // Alt bar Simulating/Paused'da primary kontrol — çakışma önlemek için
        // eski sahne butonlarını o state'lerde gizle.
        togglePauseButton ?.gameObject.SetActive(canPause   && bottomBar == null);
        resetButton       ?.gameObject.SetActive(!drawing   && !simOrPaused);

        // ---- Alt bar görünürlüğü ----
        if (bottomBar != null)
            bottomBar.SetActive(simOrPaused);

        // Alt bar buton görünürlükleri (barın içinde de mantık aynı)
        if (centerPauseButton  != null) centerPauseButton .gameObject.SetActive(canPause);
        if (rightResetButton   != null) rightResetButton  .gameObject.SetActive(simOrPaused);
        if (leftSettingsButton != null) leftSettingsButton.gameObject.SetActive(simOrPaused);

        // State değişince settings panelini kapat (UI akışı temiz)
        if (settingsPanel != null && !simOrPaused)
        {
            settingsOpen = false;
            settingsPanel.SetActive(false);
        }
    }

    // =======================================================================
    // RUNTIME UI — alt bar + settings panel (sahne dosyasına dokunmaz)
    // =======================================================================

    static readonly Color ColorBarBg        = new Color(0.08f, 0.09f, 0.12f, 0.85f);
    static readonly Color ColorPauseBtn     = new Color(1.00f, 0.55f, 0.20f, 1f);
    static readonly Color ColorResetBtn     = new Color(0.85f, 0.30f, 0.30f, 1f);
    static readonly Color ColorSettingsBtn  = new Color(0.40f, 0.55f, 0.70f, 1f);
    static readonly Color ColorPanelBg      = new Color(0.10f, 0.11f, 0.14f, 0.95f);

    void BuildRuntimeUI()
    {
        // Canvas referansını mevcut bir UI elemanından al
        Canvas canvas = null;
        foreach (var b in new[] { resetButton, togglePauseButton, confirmShapeButton,
                                   clearShapeButton, addBallButton, startButton })
        {
            if (b != null) { canvas = b.GetComponentInParent<Canvas>(); if (canvas != null) break; }
        }
        if (canvas == null && stateLabel != null)
            canvas = stateLabel.GetComponentInParent<Canvas>();

        if (canvas == null)
        {
            Debug.LogWarning("[UI] ⚠ Canvas bulunamadı — alt bar runtime'da kurulamadı.");
            return;
        }

        bottomBar = CreateBottomBar(canvas);
        settingsPanel = CreateSettingsPanel(canvas);
    }

    GameObject CreateBottomBar(Canvas canvas)
    {
        var bar = new GameObject("BottomBar_Runtime", typeof(RectTransform), typeof(Image));
        bar.transform.SetParent(canvas.transform, false);

        var rt = (RectTransform)bar.transform;
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot     = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 20f);
        rt.sizeDelta = new Vector2(380f, 52f);  // Daha düz, metin butonlarına uygun

        var bg = bar.GetComponent<Image>();
        bg.color = ColorBarBg;
        bg.raycastTarget = true; // arkasındaki world space'i bloklasın

        // Sol: Ayarlar
        leftSettingsButton = CreateBarButton(bar.transform, "LeftSettingsBtn",
            new Vector2(-125f, 0f), new Vector2(110f, 38f), "Ayarlar", ColorSettingsBtn, out _);
        leftSettingsButton.onClick.AddListener(ToggleSettingsPanel);

        // Merkez: Durdur / Devam (biraz daha geniş — metin için alan)
        centerPauseButton = CreateBarButton(bar.transform, "CenterPauseBtn",
            new Vector2(0f, 0f), new Vector2(130f, 38f), "Durdur", ColorPauseBtn, out centerPauseLabel);
        centerPauseButton.onClick.AddListener(GameManager.Instance.OnTogglePause);

        // Sağ: Sifirla
        rightResetButton = CreateBarButton(bar.transform, "RightResetBtn",
            new Vector2(125f, 0f), new Vector2(110f, 38f), "Sifirla", ColorResetBtn, out _);
        rightResetButton.onClick.AddListener(GameManager.Instance.OnReset);

        bar.SetActive(false); // başlangıçta Drawing state — gizli
        return bar;
    }

    Button CreateBarButton(Transform parent, string name, Vector2 pos, Vector2 size,
                           string label, Color color, out TextMeshProUGUI labelText)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        var img = go.GetComponent<Image>();
        img.color = color;
        // Sprite yok — düz renkli dikdörtgen; Knob.psd bağımlılığı kaldırıldı, her versiyonda çalışır

        var btn = go.GetComponent<Button>();

        // Label
        var txtGO = new GameObject("Label", typeof(RectTransform));
        txtGO.transform.SetParent(go.transform, false);
        var txtRT = (RectTransform)txtGO.transform;
        txtRT.anchorMin = Vector2.zero;
        txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = txtRT.offsetMax = Vector2.zero;

        labelText = txtGO.AddComponent<TextMeshProUGUI>();
        labelText.text      = label;
        labelText.color     = Color.white;
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.fontSize  = 14f;
        labelText.raycastTarget = false;

        return btn;
    }

    GameObject CreateSettingsPanel(Canvas canvas)
    {
        var panel = new GameObject("SettingsPanel_Runtime", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvas.transform, false);

        var rt = (RectTransform)panel.transform;
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot     = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(-125f, 80f);  // Ayarlar butonunun tam üstüne hizala (bar top: 20+52=72, +8px gap)
        rt.sizeDelta = new Vector2(260f, 180f);

        var bg = panel.GetComponent<Image>();
        bg.color = ColorPanelBg;

        // Başlık
        var titleGO = new GameObject("Title", typeof(RectTransform));
        titleGO.transform.SetParent(panel.transform, false);
        var titleRT = (RectTransform)titleGO.transform;
        titleRT.anchorMin = new Vector2(0f, 1f);
        titleRT.anchorMax = new Vector2(1f, 1f);
        titleRT.pivot     = new Vector2(0.5f, 1f);
        titleRT.anchoredPosition = new Vector2(0f, -10f);
        titleRT.sizeDelta = new Vector2(-20f, 30f);
        var title = titleGO.AddComponent<TextMeshProUGUI>();
        title.text = "Ayarlar";
        title.color = Color.white;
        title.alignment = TextAlignmentOptions.Center;
        title.fontSize = 22f;
        title.raycastTarget = false;

        // İçerik placeholder
        var contentGO = new GameObject("Content", typeof(RectTransform));
        contentGO.transform.SetParent(panel.transform, false);
        var contentRT = (RectTransform)contentGO.transform;
        contentRT.anchorMin = new Vector2(0f, 0f);
        contentRT.anchorMax = new Vector2(1f, 1f);
        contentRT.offsetMin = new Vector2(12f, 40f);
        contentRT.offsetMax = new Vector2(-12f, -50f);
        var content = contentGO.AddComponent<TextMeshProUGUI>();
        content.text = "Yakında:\n• Simülasyon hızı\n• Ses\n• Ekran";
        content.color = new Color(0.80f, 0.82f, 0.88f);
        content.alignment = TextAlignmentOptions.TopLeft;
        content.fontSize = 15f;
        content.raycastTarget = false;

        // Close (X) — sağ üst
        var closeGO = new GameObject("CloseBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        closeGO.transform.SetParent(panel.transform, false);
        var closeRT = (RectTransform)closeGO.transform;
        closeRT.anchorMin = new Vector2(1f, 1f);
        closeRT.anchorMax = new Vector2(1f, 1f);
        closeRT.pivot     = new Vector2(1f, 1f);
        closeRT.anchoredPosition = new Vector2(-8f, -8f);
        closeRT.sizeDelta = new Vector2(28f, 28f);
        var closeImg = closeGO.GetComponent<Image>();
        closeImg.color = new Color(0.45f, 0.15f, 0.15f, 1f);
        var closeBtn = closeGO.GetComponent<Button>();
        closeBtn.onClick.AddListener(() => { settingsOpen = false; if (settingsPanel != null) settingsPanel.SetActive(false); });

        var closeTxtGO = new GameObject("X", typeof(RectTransform));
        closeTxtGO.transform.SetParent(closeGO.transform, false);
        var closeTxtRT = (RectTransform)closeTxtGO.transform;
        closeTxtRT.anchorMin = Vector2.zero;
        closeTxtRT.anchorMax = Vector2.one;
        closeTxtRT.offsetMin = closeTxtRT.offsetMax = Vector2.zero;
        var closeTxt = closeTxtGO.AddComponent<TextMeshProUGUI>();
        closeTxt.text = "×";
        closeTxt.color = Color.white;
        closeTxt.alignment = TextAlignmentOptions.Center;
        closeTxt.fontSize  = 22f;
        closeTxt.raycastTarget = false;

        panel.SetActive(false);
        return panel;
    }

    void ToggleSettingsPanel()
    {
        if (settingsPanel == null) return;
        settingsOpen = !settingsOpen;
        settingsPanel.SetActive(settingsOpen);
    }
}
