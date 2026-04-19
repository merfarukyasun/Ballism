using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Canvas UI — buton görünürlüğü, durum etiketi, yönergeler.
/// Faz 2.5: startButton, SpawnSelect akışı, köşe tabanlı spawn yönergesi.
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("Butonlar")]
    public Button confirmShapeButton; // Drawing
    public Button clearShapeButton;   // Drawing
    public Button startButton;        // SpawnSelect (en az 1 top varken)
    public Button togglePauseButton;  // Simulating / Paused
    public Button addBallButton;      // Simulating / Paused (maxBalls dolmadıysa)
    public Button resetButton;        // Drawing hariç her state

    [Header("Etiketler")]
    public TextMeshProUGUI stateLabel;
    public TextMeshProUGUI ballCountLabel;
    public TextMeshProUGUI instructionLabel;

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

        GameManager.Instance.OnStateChanged += RefreshUI;
        RefreshUI(GameManager.Instance.State);
    }

    // -----------------------------------------------------------------------
    void Update()
    {
        var gm = GameManager.Instance;

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
                GameState.Simulating   => "Simülasyon aktif.  [Durdur] ile beklet.",
                GameState.Paused       => "Duraklatıldı.  [Devam Et] veya [Top Ekle].",
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
        bool canPause    = s == GameState.Simulating || s == GameState.Paused;
        bool canAddBall  = (s == GameState.Simulating || s == GameState.Paused)
                           && gm.BallCount < gm.maxBalls;
        bool canStart    = spawnSelect && gm.BallCount > 0;

        confirmShapeButton?.gameObject.SetActive(drawing);
        clearShapeButton  ?.gameObject.SetActive(drawing);
        startButton       ?.gameObject.SetActive(canStart);
        togglePauseButton ?.gameObject.SetActive(canPause);
        addBallButton     ?.gameObject.SetActive(canAddBall);
        resetButton       ?.gameObject.SetActive(!drawing);
    }
}
