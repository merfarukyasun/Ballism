using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Sandbox kamera kontrolleri: mouse scroll ile yumuşak zoom, sağ tık drag ile pan.
/// Runtime'da Main Camera'ya UIManager tarafından attach edilir — sahne dosyası değişmez.
///
/// Guard'lar:
///   • UI üzerinde → scroll/right-click kamera tetiklemez (EventSystem.IsPointerOverGameObject)
///   • Uzak zoom'da → pan başlamaz (orthographicSize < panThreshold)
///   • Drag devam ederken → zoom değişse bile bırakılana kadar pan sürer
///   • Z ekseni korunur (-10 vb.)
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraController : MonoBehaviour
{
    [Header("Zoom")]
    [SerializeField] float zoomStep       = 1.2f;   // tek scroll tik kuvveti
    [SerializeField] float minOrthoSize   = 2f;
    [SerializeField] float maxOrthoSize   = 12f;
    [SerializeField] float zoomSmoothTime = 0.10f;

    [Header("Pan")]
    [SerializeField] float   panThreshold  = 7f;      // orthoSize < bu değer → pan aktif
    [SerializeField] float   panSmoothTime = 0.08f;
    [SerializeField] Vector2 panBoundsX    = new Vector2(-4f, 22f); // world-space X sınırı
    [SerializeField] Vector2 panBoundsY    = new Vector2(-4f, 22f); // world-space Y sınırı

    Camera cam;
    float  targetOrthoSize;
    Vector3 targetPosition;

    // Pan drag state
    bool    dragging;
    Vector2 dragStartScreen;
    Vector3 dragStartCamPos;

    void Awake()
    {
        cam = GetComponent<Camera>();
        targetOrthoSize = cam.orthographicSize;
        targetPosition  = cam.transform.position;
    }

    void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        bool overUI = EventSystem.current != null
                      && EventSystem.current.IsPointerOverGameObject();

        // --- Zoom (scroll) — UI üzerinde skip ---
        if (!overUI)
        {
            float scrollY = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scrollY) > 0.01f)
            {
                // scrollY normalde ±120 civarı (Windows) — normalize
                float step = Mathf.Sign(scrollY) * zoomStep;
                targetOrthoSize = Mathf.Clamp(targetOrthoSize - step, minOrthoSize, maxOrthoSize);
            }
        }

        // Smooth zoom her karede çalışır (drag halinde de hedef değerine gitsin)
        cam.orthographicSize = Mathf.Lerp(
            cam.orthographicSize, targetOrthoSize,
            1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(zoomSmoothTime, 0.001f)));

        // --- Pan (sağ tık drag) ---
        // Başlatma: UI değil + yakın zoom + fresh press
        if (!dragging
            && mouse.rightButton.wasPressedThisFrame
            && !overUI
            && cam.orthographicSize < panThreshold)
        {
            dragging        = true;
            dragStartScreen = mouse.position.ReadValue();
            dragStartCamPos = cam.transform.position;
        }

        // Devam: drag bir kez başladıysa bırakılana kadar sürer (zoom değişse de)
        if (dragging)
        {
            if (!mouse.rightButton.isPressed)
            {
                dragging = false;
            }
            else
            {
                Vector2 delta = mouse.position.ReadValue() - dragStartScreen;
                // Screen pixel → world unit: orthoSize = yarı yükseklik
                float unitsPerPixel = (cam.orthographicSize * 2f) / Mathf.Max(Screen.height, 1);
                Vector3 world = dragStartCamPos - new Vector3(delta.x, delta.y, 0f) * unitsPerPixel;
                targetPosition = new Vector3(world.x, world.y, dragStartCamPos.z);
            }
        }

        // Pan sınırı — kamera oyun alanının çok dışına kaymasın (Inspector'dan ayarlanabilir)
        targetPosition = new Vector3(
            Mathf.Clamp(targetPosition.x, panBoundsX.x, panBoundsX.y),
            Mathf.Clamp(targetPosition.y, panBoundsY.x, panBoundsY.y),
            targetPosition.z);

        // Smooth pan — z korunur
        Vector3 cur = cam.transform.position;
        Vector3 tgt = new Vector3(targetPosition.x, targetPosition.y, cur.z);
        float   k   = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(panSmoothTime, 0.001f));
        cam.transform.position = Vector3.Lerp(cur, tgt, k);
    }
}
