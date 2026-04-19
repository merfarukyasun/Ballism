# Ballism — Mimari Notları
_Son güncelleme: Sürekli (continuous) simülasyon mimarisine pivot sonrası_

---

## Çalışma Kuralları (Token Tasarrufu)

1. Yeni göreve başlarken **önce `current_state.md` oku**, sonra karar ver.
2. Dosyayı yeniden oku ancak: kod değiştiyse, hata ayıklıyorsan, ya da `current_state.md` eksikse.
3. Her büyük adımdan sonra `current_state.md` güncelle.
4. Faz dışına çıkma, bağımsız dosyaları merak edip açma.

---

## Proje Bilgileri

- **Unity**: 6.3 LTS, Universal Render Pipeline 2D
- **Input**: `UnityEngine.InputSystem` (New Input System Only)
- **UI**: TextMeshPro (TMP)
- **Yol**: `C:\Users\merfa\Documents\Unity\Ballism`
- **Sahne**: `Assets/Scenes/Ballceyda.unity`
- **Setup menu**: `Ballism/Setup Scene` (`Assets/Editor/BallismSetup.cs`)

---

## Hedef Mimari — Katmanlı (L1–L6)

```
┌─────────────────────────────────────────────────────────────┐
│ L6  UI / State Machine      UIManager, GameManager          │
├─────────────────────────────────────────────────────────────┤
│ L5  Presentation            BallView (Glow, Trail),         │
│                              CameraController                │
├─────────────────────────────────────────────────────────────┤
│ L4  Simulation (core)       BallBody (pos, vel, radius),    │
│                              WallCollisionService (swept),   │
│                              SimulationLoop (fixed-step)     │
├─────────────────────────────────────────────────────────────┤
│ L3  Geometry                EdgeGeometry: edge grid →        │
│                              List<Segment2D> (line segments) │
├─────────────────────────────────────────────────────────────┤
│ L2  Grid Editor             GridManager (çizim, region,     │
│                              corner snap — simulation dışı)  │
├─────────────────────────────────────────────────────────────┤
│ L1  Config                  SimulationConfig                 │
│                              (ScriptableObject, hız/radius)  │
└─────────────────────────────────────────────────────────────┘
```

**Tek yönlü bağımlılık:** L_n yalnızca L_{n-1}..L_1'i tanır. Grid simülasyonun parçası değildir; yalnızca editör ve spawn için referanstır.

---

## Sorumluluk Haritası

| Dosya | Sorumluluk | Durum |
|---|---|---|
| `Scripts/GridManager.cs` | Edge editör + region + corner snap; `Bounce*` Faz 2'de kaldırılacak | Faz 2.5 |
| `Scripts/GameManager.cs` | State machine + spawn; çarpışma kodu Faz 2'de taşınır | Faz 2.5 |
| `Scripts/UIManager.cs` | Canvas UI (stabil) | Faz 2.5 |
| `Scripts/BallController.cs` | Discrete cell/corner hareket — Faz 2'de kaldırılacak | Faz 2.5 |
| `Scripts/DirectionIndicator.cs` | Yön verisi taşıyıcı | Faz 0 |
| `Scripts/GridCell.cs` | Hücre koordinat metadata | Faz 0 |
| `Editor/BallismSetup.cs` | Editor kurulum aracı (+ startButton, panel 460) | Faz 2.5 |
| `Scripts/SimulationConfig.cs` | **PLANLI** — ScriptableObject (ballSpeed, radius, maxBalls) | Faz 4 |
| `Scripts/EdgeGeometry.cs` | **PLANLI** — `hEdges`/`vEdges` → `List<Segment2D>` | Faz 2 |
| `Scripts/BallBody.cs` | **PLANLI** — continuous pos/vel/radius | Faz 2 |
| `Scripts/WallCollisionService.cs` | **PLANLI** — swept circle vs segment | Faz 2 |
| `Scripts/SimulationLoop.cs` | **PLANLI** — fixed timestep substep | Faz 2 |
| `Scripts/BallView.cs` | **PLANLI** — sprite + glow + trail (BallBody'den okur) | Faz 2 |
| `Scripts/CameraController.cs` | **PLANLI** — pan/zoom, aspect | Faz 3 |

---

## Kritik Konvansiyonlar

### Edge index
```
Grid: 10×10 hücre
hEdges: [11, 10]  →  hEdges[yr, xc] = cell(xc,yr-1) ile (xc,yr) arası YATAY kenar
vEdges: [10, 11]  →  vEdges[yc, xr] = cell(xr-1,yc) ile (xr,yc) arası DİKEY kenar

Flood fill geçişleri (cell x,y'den):
  Sağa git  → vEdges[y, x+1] == false
  Sola git  → vEdges[y, x]   == false
  Yukarı git→ hEdges[y+1, x] == false
  Aşağı git → hEdges[y, x]   == false
```

### Traversal hücre (corner yönünden)
```
dir = (dx, dy), corner = (cx, cy)
traversalCell = (dx>0 ? cx : cx-1, dy>0 ? cy : cy-1)
```

### Corner bounce üçü (KALDIRILACAK — Faz 2)
```
fwd  = (dx>0 ? cx : cx-1 , dy>0 ? cy : cy-1)
hor  = (fwd.x            , dy<0 ? cy : cy-1)   // Y yansıması
ver  = (dx<0 ? cx : cx-1 , fwd.y           )   // X yansıması
rev  = (ver.x            , hor.y            )  // ConvexCorner revOpen check
```

### Dünya uzayı
- `cellSize = 1.0f`, origin = (-width/2, -height/2)
- `GridToWorld(x, y)` = hücre merkezi
- `CornerToWorld(cx, cy)` = köşe noktası

---

## Discrete Bounce (şu anki kod — referans)

```
target = pos + dir
if IsSelected(target)     → Free (devam et)
hOpen = IsSelected(pos.x + dir.x, pos.y)   // yatay komşu var mı
vOpen = IsSelected(pos.x, pos.y + dir.y)   // dikey komşu var mı

hOpen && vOpen   → ConvexCorner (revOpen ile 2 veya 3 ihtimalden random)
hOpen && !vOpen  → FlatWallH   (Y eksenini çevir)
!hOpen && vOpen  → FlatWallV   (X eksenini çevir)
!hOpen && !vOpen → ConcaveCorner (tam ters)
```

---

## Sürekli Simülasyon Algoritması (Faz 2 hedefi)

### BallBody.Step(dt)
```
remaining = dt
for (substep = 0; substep < maxSubsteps && remaining > 0; substep++)
  hit = WallCollisionService.Sweep(position, velocity, radius, remaining)
  if (hit == null)
    position += velocity * remaining
    break
  position += velocity * hit.toi
  velocity = Reflect(velocity, hit.normal)
  remaining -= hit.toi
  // küçük epsilon ile duvar içine saplanmayı önle
```

### WallCollisionService.Sweep
- Input: `pos, vel, radius, dt`
- Output: `(toi, normal, segment)?` — ilk çarpışma (en küçük toi)
- Segment gövdesi: Minkowski dilation (yarıçap kadar paralel kaydırılmış hat) ile ray-line kesişimi
- Endpoint cap: segment uç noktası ↔ top — nokta-daire süpürme (quadratic)
- Convex köşe normal seçimi: endpoint'e yönelen radyal normal

### SimulationLoop
- `FixedUpdate()` — `Time.fixedDeltaTime` (default 0.02s)
- `maxSubsteps = 4` — sıkışma/loop önler
- `Paused` state'te çağrılmaz

---

## Grid ↔ Geometry Köprüsü

`OnConfirmShape` / region selection sonrası **bir kez** `EdgeGeometry.Rebuild(gridManager, selectedRegion)`:
1. Seçili region sınırındaki tüm kenarları topla (iç + border).
2. Her kenarı `Segment2D(worldA, worldB)` olarak kaydet.
3. `WallCollisionService` bu listeyi tüketir.

**Simülasyon çalışırken grid değişmez.** State machine bunu garanti eder.

---

## Input Migrasyonu Notları

```csharp
// KULLANMA:
Input.GetMouseButton(0)
OnMouseDown() / OnMouseEnter()

// KULLAN:
Mouse.current.leftButton.wasPressedThisFrame
Mouse.current.leftButton.isPressed
Mouse.current.leftButton.wasReleasedThisFrame
Mouse.current.position.ReadValue()
```

---

## Tuzaklar

### Unity "Fake Null"
```csharp
// YANLIŞ — ?? operatörü Unity'nin fake-null'unu yakalamaz:
var trail = GetComponent<TrailRenderer>() ?? gameObject.AddComponent<TrailRenderer>();

// DOĞRU:
var trail = GetComponent<TrailRenderer>();
if (trail == null) trail = gameObject.AddComponent<TrailRenderer>();
```

### URP 2D + Sprites/Default shader
URP altında `Sprites/Default` bazen pink render olur. Güvenli alternatif: `Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")`.

### TextMeshPro
- `TextMeshPro` 3D, `TextMeshProUGUI` Canvas UI. Karıştırma.

### Input System
- `Mouse.current` null olabilir (başlangıç). Her erişimde null-check.
- `wasPressedThisFrame` tek kare true — yalnız `Update`'te oku.

---

## TMP Examples İzolasyonu

```json
// Assets/TextMesh Pro/Examples & Extras/Scripts/TMPro.Examples.asmdef
{ "name": "TMPro.Examples", "defineConstraints": ["ENABLE_LEGACY_INPUT_MANAGER"] }
```
→ New Input System Only modunda TMP örnek scriptleri derlenmez.

---

## Tekrar Okumaya Gerek Olmayan Dosyalar

- `GridCell.cs` — 2 int, değişmez
- `DirectionIndicator.cs` — tek `Vector2Int direction`, değişmez
- `.gitignore` / `.gitattributes` — kurulu, değişmez

---

## Referanslar

- `current_state.md` — mevcut kod durumu
- `known_issues.md` — bilinen buglar/risks
- `next_phase_plan.md` — sıradaki fazlar
