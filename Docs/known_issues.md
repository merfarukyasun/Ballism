# Ballism — Bilinen Sorunlar
_Son güncelleme: Faz 2.5 sonrası, mimari pivot öncesi stabilizasyon planı_

---

## BLOCKER — Faz 1 (Stabilizasyon) kapsamı

### 1. `TrailRenderer` Unity fake-null
**Dosya:** `Assets/Scripts/BallController.cs` — `SetupVisuals()` (~satır 273)
```csharp
var trail = GetComponent<TrailRenderer>() ?? gameObject.AddComponent<TrailRenderer>();
```
Unity'nin fake-null davranışı: `GetComponent` "null" görünse de `??` operatörü için non-null sayılır → `AddComponent` çağrılmaz → null reference. 
**Fix:** explicit null check.
```csharp
var trail = GetComponent<TrailRenderer>();
if (trail == null) trail = gameObject.AddComponent<TrailRenderer>();
```

### 2. Discrete ball-ball çarpışma stabil değil
**Dosya:** `Assets/Scripts/GameManager.cs` — `CheckBallCollisions()` (~satır 252) ve `LateUpdate` (~satır 309)
- Model: aynı `CurrentPosition`'da iki top → `Direction` swap.
- Sorun: step-based hareket içinde iki top aynı frame aynı hücreye nadiren denk gelir → çarpışma kaçar; ayrıca swap sonrası iki topun fromPos/toPos state'leri lerp ortasında kalıyor → görsel aksaklık.
- **Şimdilik devre dışı bırak**: `LateUpdate`'teki `CheckBallCollisions()` çağrısını kaldır. Faz 5'te continuous modelde yeniden yazılacak.

### 3. Reset tam temizlemiyor
**Dosya:** `Assets/Scripts/GameManager.cs` — `OnReset()` / `ClearBalls()`
- `balls` listesi boşaltılıyor ama sahneye düşmüş orphan `BallController` olabilir (spawn sırası kesildiyse).
- **Fix:** `ClearBalls` içinde liste dışı yedek:
  ```csharp
  foreach (var b in FindObjectsByType<BallController>(FindObjectsSortMode.None))
      if (b != null) Destroy(b.gameObject);
  ```

### 4. Spawn validation eksik
**Dosya:** `Assets/Scripts/GameManager.cs` — `SpawnBall(corner, dir)` (~satır 224)
- Korner ve yön doğrulanmadan top yaratılıyor. Geçersiz yön verilirse `InitAtCorner` içinde `moving=false` olsa da `balls` listesine ekleniyor → hayalet top sayısı.
- **Fix:** `SpawnBall` içinde `GridManager.GetValidCornerDirections(corner).Contains(dir)` kontrolü; geçersizse early return.

### 5. Sahne `Ballceyda.unity`'de `startButton` yok
- `UIManager.startButton` referansı null olursa sessiz geçer ama simülasyon başlatılamaz.
- **Fix seçenekleri:**
  a) Kullanıcı `Ballism/Setup Scene` menüsünü rerun eder.
  b) Editor'de manuel Button ekler ve `UIManager.startButton` alanına sürükler.
  c) `UIManager.Start()` içinde hard fail log eklenir ("⚠ startButton ref yok").

---

## Mimari / Yeniden Yazım (Faz 2 kapsamı — stabilize edilmeyecek, değiştirilecek)

### 6. Discrete hareket modeli
**Dosyalar:** `BallController.cs` (tüm), `GridManager.Bounce`, `GridManager.BounceFromCorner`
- Cell/corner step-based hareket sürekli sandbox vizyonu ile uyumsuz.
- Faz 2'de tamamen `BallBody` + `WallCollisionService` ile değiştirilecek.
- **Not:** Faz 1 stabilizasyonu sadece mevcut discrete sistemin çökmemesini sağlar; uzun vadede kod gider.

### 7. Corner bounce edge-case'leri
**Dosya:** `GridManager.BounceFromCorner`
- ConvexCorner `revOpen` düzeltmesi yapıldı (son surgical edit) ama `ConcaveCorner` + mikro-adım sıkışması hâlâ mümkün.
- **Durum:** Faz 2'de bu fonksiyon kaldırılacak → iyileştirme yerine çöp.

---

## Görsel / Render

### 8. URP `Sprites/Default` pembe render riski
**Dosya:** `BallController.SetupVisuals()` — trail material
```csharp
var sh = Shader.Find("Sprites/Default");
```
- URP 2D altında bazen magenta render. Kullanıcı şu an için sorun bildirmedi ama Faz 2 `BallView` içinde URP shader'a geçilecek.

### 9. Neon bloom yok
- Sadece soft glow child sprite'ı. Tam neon görünümü için URP Post-Processing Volume + Bloom override gerekir.
- **Durum:** Faz 3 polish.

### 10. Edge hover highlight yok (çizim modunda)
- Kullanıcı fareyle kenar üzerindeyken görsel feedback yok.
- **Durum:** Faz 3 polish.

---

## Git / Süreç

### 11. Faz 1 + 2 + 2.5 commit edilmedi
- Branch: `dev`. Son commit: Faz 0 (695fafd).
- **Öneri:** Faz 1 (stabilizasyon) tamamlanınca tek commit → checkpoint. Mimari pivot öncesi güvenli dönüş noktası.

---

## İzlenecek / Belirsiz

- **Corner movement "dışarıdan giriş":** Belirli şekillerde top seçili olmayan hücreden geçebiliyor gibi görünüyor — repro edilmedi. Faz 2 continuous modelde otomatik çözülür.
- **"Traversal geçersiz" warning:** `BallController.ResolveDirectionCorner` içinde `LogWarning` — kullanıcı oyun içinde görüyor mu? Tracking eksik.
- **Substep sıkışması:** Continuous modelde `maxSubsteps` aşımı = ball içeri girmiş demek; log + fallback gerek.

---

## Referanslar

- `current_state.md` — mevcut kod durumu
- `architecture_notes.md` — hedef mimari
- `next_phase_plan.md` — faz bazlı plan
