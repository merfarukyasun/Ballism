using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Canvas butonlarını ve durum metnini yönetir.
/// Inspector'dan buton referanslarını bağlayın.
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("Buttons")]
    public Button confirmShapeButton;
    public Button clearShapeButton;
    public Button togglePauseButton;
    public Button addBallButton;
    public Button resetButton;

    [Header("Labels")]
    public TextMeshProUGUI stateLabel;
    public TextMeshProUGUI ballCountLabel;
    public TextMeshProUGUI instructionLabel;

    static readonly string[] Instructions =
    {
        /* Drawing     */ "Hücrelere tıkla / sürükle → şekil çiz. Bitti mi? [Onayla]",
        /* SpawnSelect */ "Şekil içinde bir hücreye tıkla, çıkan oka tıkla → top fırlat.",
        /* Simulating  */ "Simülasyon devam ediyor. [Durdur] veya [Top Ekle]",
        /* Paused      */ "Duraklatıldı. [Devam Et] ile simülasyona dön."
    };

    void Start()
    {
        confirmShapeButton?.onClick.AddListener(GameManager.Instance.OnConfirmShape);
        clearShapeButton  ?.onClick.AddListener(GameManager.Instance.OnClearShape);
        togglePauseButton ?.onClick.AddListener(GameManager.Instance.OnTogglePause);
        addBallButton     ?.onClick.AddListener(GameManager.Instance.OnAddBall);
        resetButton       ?.onClick.AddListener(GameManager.Instance.OnReset);

        GameManager.Instance.OnStateChanged += RefreshUI;
        RefreshUI(GameManager.Instance.State);
    }

    void Update()
    {
        // Top sayısını sürekli güncelle
        if (ballCountLabel != null)
            ballCountLabel.text = $"Top: {GameManager.Instance.BallCount} / {GameManager.Instance.maxBalls}";
    }

    void RefreshUI(GameState s)
    {
        // Durum etiketi
        if (stateLabel != null)
            stateLabel.text = s switch
            {
                GameState.Drawing     => "ÇİZİM",
                GameState.SpawnSelect => "TOP SPAWNLA",
                GameState.Simulating  => "SİMÜLASYON",
                GameState.Paused      => "DURAKLATILDI",
                _                     => ""
            };

        // Yönerge
        if (instructionLabel != null)
            instructionLabel.text = Instructions[(int)s];

        // Duraklat / Devam butonu metni
        if (togglePauseButton != null)
        {
            var txt = togglePauseButton.GetComponentInChildren<TextMeshProUGUI>();
            if (txt != null)
                txt.text = s == GameState.Paused ? "Devam Et" : "Durdur";
        }

        // Buton görünürlükleri
        bool drawing    = s == GameState.Drawing;
        bool canAddBall = (s == GameState.Simulating || s == GameState.Paused)
                          && GameManager.Instance.BallCount < GameManager.Instance.maxBalls;
        bool canPause   = s == GameState.Simulating || s == GameState.Paused;

        confirmShapeButton ?.gameObject.SetActive(drawing);
        clearShapeButton   ?.gameObject.SetActive(drawing);
        togglePauseButton  ?.gameObject.SetActive(canPause);
        addBallButton      ?.gameObject.SetActive(canAddBall);
        resetButton        ?.gameObject.SetActive(s != GameState.Drawing);
    }
}
