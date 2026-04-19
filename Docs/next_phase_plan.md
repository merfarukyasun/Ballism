# Ballism — Sonraki Faz Planı
_Mimari pivot sonrası plan. Sürekli sandbox simülasyonuna geçiş._

---

## Genel Strateji

**Önce stabilize et, sonra yeniden yaz.** Mevcut Faz 2.5 sisteminin çökmemesini sağla (Faz 1); sonra discrete hareket modelini sürekli modelle değiştir (Faz 2). Grid editörü + region + spawn akışı KORUNUR — yalnız hareket/çarpışma çekirdeği değişir.

---

## Faz 1 — Stabilizasyon (discrete sistemi çalışır halde tut)

**Amaç:** Mevcut Faz 2.5 çökmeden test edilebilsin. Kısa süreli — tek commit ile kapatılır.

### Görevler

1. **TrailRenderer fake-null fix**
   - Dosya: `BallController.SetupVisuals()`
   - Explicit `if (trail == null) AddComponent` deseni.

2. **Ball-ball collision devre dışı**
   - Dosya: `GameManager.LateUpdate`
   - `CheckBallCollisions()` çağrısını kaldır (metod kalabilir, Faz 5'te continuous versiyon yazılır).

3. **Reset thoroughness**
   - Dosya: `GameManager.ClearBalls`
   - `FindObjectsByType<BallController>(FindObjectsSortMode.None)` ile yedek temizlik.
   - `ClearIndicators` `OnReset`'te zaten çağrılıyor — doğrula.

4. **Spawn validation**
   - Dosya: `GameManager.SpawnBall`
   - `GridManager.GetValidCornerDirections(corner).Contains(dir)` kontrolü; aksi halde early return + log.

5. **Scene startButton uyarısı**
   - Dosya: `UIManager.Start`
   - `if (startButton == null) Debug.LogWarning("⚠ startButton eksik — Setup Scene rerun.");`

### Çıktı
- Commit: `faz-1-2-2.5: stabilization pass before continuous rewrite`
- Test: Çiz → Onayla → Bölge seç → 1-3 top yerleştir → Başlat → Durdur → Sıfırla döngüsü crash-free.

### Riskler
- `FindObjectsByType` performans: 50+ topta O(n) ama `OnReset` tek seferlik, sorun yok.

---

## Faz 2 — Continuous Motion Core (**ana iş**)

**Amaç:** Discrete step-based hareketi tamamen sürekli (`Vector2` pos + vel + swept collision) modelle değiştir.

### Yeni dosyalar

#### `Assets/Scripts/Geometry/Segment2D.cs`
```csharp
public readonly struct Segment2D {
    public readonly Vector2 a, b;
    public readonly Vector2 Normal; // önceden hesaplanmış outward normal
    public Segment2D(Vector2 a, Vector2 b, Vector2 n) { ... }
}
```

#### `Assets/Scripts/Geometry/EdgeGeometry.cs`
- `public static List<Segment2D> Build(GridManager gm, int regionIndex)`
- Seçili region sınırındaki tüm kenarları dolaş, dünya-uzay `Segment2D`'a çevir.
- Normal: seçili region'ın DIŞINA doğru bakar (top içerden çarpar).

#### `Assets/Scripts/Simulation/BallBody.cs`
```csharp
public class BallBody {
    public Vector2 position;
    public Vector2 velocity;
    public float   radius;
    public bool    paused;
    public void Step(float dt, WallCollisionService walls);
}
```
- MonoBehaviour değil — pure data + Step.

#### `Assets/Scripts/Simulation/WallCollisionService.cs`
```csharp
public class WallCollisionService {
    List<Segment2D> segments;
    public void Rebuild(List<Segment2D> segs);
    public Hit? Sweep(Vector2 pos, Vector2 vel, float radius, float dt);
}
public struct Hit { public float toi; public Vector2 normal; }
```
- Her segment için Minkowski dilation + ray-line kesişimi.
- Endpoint cap: nokta-daire süpürme (quadratic).

#### `Assets/Scripts/Simulation/SimulationLoop.cs`
- `MonoBehaviour`, `FixedUpdate()`.
- `List<BallBody> bodies` ile `WallCollisionService walls`.
- `if (GameManager.State != Simulating) return;`
- Tüm bodies'i `Step(Time.fixedDeltaTime, walls)`.
- `maxSubsteps` sınırı BallBody içinde.

#### `Assets/Scripts/Presentation/BallView.cs`
- `MonoBehaviour`, `BallBody body`.
- `Update`'te `transform.position = body.position`.
- Eski `SetupVisuals` (glow + trail) buraya taşınır.

### Silinen / değişen
- **Kaldır:** `BallController.cs` (hem cell hem corner mode).
- **Kaldır:** `GridManager.Bounce`, `GridManager.BounceFromCorner`.
- **Kaldır:** `GameManager.CheckBallCollisions` (Faz 5'e kadar yok).
- **Değişen:** `GameManager.SpawnBall` — artık `BallBody` + `BallView` yaratır.
  - `InitAtCorner` yerine: `body.position = CornerToWorld(corner); body.velocity = dir.normalized * ballSpeed;`
- **Değişen:** `GameManager` tutar: `List<BallBody> bodies`, `SimulationLoop loop`, `WallCollisionService walls`.
- **Değişen:** `OnConfirmShape` veya region selection sonrası `EdgeGeometry.Build` → `walls.Rebuild`.

### Test senaryoları
- Dikdörtgen, üçgen, L-şekli — top içeride kapalı ve duvarlara sürekli yansıyor.
- Köşeye tam dik giriş — endpoint cap doğru.
- Convex çıkıntı — normal endpoint'e radyal.
- Çok küçük dt — substep sıkışması yok.

### Riskler
- **Swept collision matematik hataları:** en büyük risk. Unit-test edilmesi zor. Strateji: önce eksen-hizalı segmentler (tüm kenarlar axis-aligned), sonra generalize.
- **Normal yönü tutarsızlığı:** `EdgeGeometry.Build` region'ı dolaşırken normal'i outward set etmeli. Yanlış normal → top duvara yapışır.
- **Performans:** 3 top × ~50 segment = 150 sweep/frame — Unity için hiçbir şey, ama yine de erken optimize etme.

---

## Faz 3 — Polish + Camera

**Amaç:** Sandbox hissi. Görsel kalite.

### Görevler
1. **CameraController**
   - Pan: orta tuş sürükle
   - Zoom: scroll wheel, aspect koruma
   - Sınır: grid sınırını aşma (opsiyonel)

2. **URP shader migrasyonu**
   - `BallView` trail material: `Universal Render Pipeline/2D/Sprite-Unlit-Default`.
   - Glow child: SpriteRenderer `Additive` blend (custom material).

3. **URP Post Volume (Bloom)**
   - Sahneye `Volume` GameObject + `Bloom` override.
   - Intensity 1.2, threshold 0.9 civarı — neon his.

4. **Edge hover highlight**
   - `GridManager.Update` (Drawing state): fare altındaki kenar yarı-saydam kırmızı preview.

5. **Region selection feedback cila**
   - Hover yeşil alpha artış, tıklama flash animasyonu.

### Riskler
- URP shader Find string'i Unity sürümüne göre değişebilir — fallback gerekir.

---

## Faz 4 — Menu / Settings + SimulationConfig

**Amaç:** Sandbox parametrelerini oyun içinden değiştir.

### Görevler
1. **`Assets/Scripts/Config/SimulationConfig.cs`** (ScriptableObject)
   - `float ballSpeed`, `float ballRadius`, `int maxBalls`, `int maxSubsteps`, `Color[] ballColors`.
   - `Assets/Resources/DefaultConfig.asset` oluştur.

2. **Settings paneli (UI)**
   - Hız slider, radius slider, maxBalls dropdown.
   - `UIManager` içinde panel toggle.

3. **GameManager.Instance.config** referansı ile tüm hard-coded değerler değiştirilir.

4. **Menu skeleton**
   - Ana menü sahnesi (opsiyonel) veya in-game pause menüsü.
   - "Yeni sandbox", "Ayarlar", "Çıkış".

### Riskler
- ScriptableObject hot-reload — Editor'de değişiklik Runtime'a yansıması garanti değil. `OnValidate` ile uyarı.

---

## Faz 5 — Ball-Ball Collision

**Amaç:** Topların birbirine çarpması. (Faz 1'de devre dışı bırakıldı.)

### Görevler
1. **Pairwise swept sphere-sphere**
   - Her frame tüm top çiftleri için quadratic toi hesabı.
   - En küçük toi'de iki topu ilerlet + elastik çarpışma (mass = 1, eşit).
2. **Broad-phase** (opsiyonel, 10+ topta gerekli)
   - Grid hash veya simple AABB sweep.
3. **Duvar + top karışık event ordering**
   - Tüm eventlerin min-toi'sini bul, en erken olanı uygula, remaining dt ile devam.

### Riskler
- **Event sıralaması karmaşık:** duvar ve top çarpışması aynı substep'te olursa doğru sıra kritik.
- Faz 2 ve 3 tamamlanmadan girilmemeli.

---

## Faz 6+ (ileri)

- **Preset şekiller** (daire, yıldız, labirent)
- **Ses efektleri** (çarpışma + ambient)
- **Mobil dokunmatik input**
- **Save/Load sandbox** (JSON)
- **WebGL build**

---

## Karar Bekleyen Noktalar (kullanıcı onayı)

- [ ] Faz 1 stabilizasyonu için tek commit mi, yoksa her görevde ayrı mı?
- [ ] `BallController.cs` Faz 2'de fiziksel silinsin mi yoksa deprecated bırakılıp sonra mı silinsin?
- [ ] URP Post-Processing setup — Faz 3 gerekli mi yoksa opsiyonel mi?
- [ ] `SimulationConfig` Faz 2'de mi, Faz 4'te mi? (Hard-coded değerler Faz 2'de yeterli olabilir.)

---

## Referanslar

- `current_state.md` — kod durumu
- `architecture_notes.md` — katmanlı mimari
- `known_issues.md` — blocker listesi
