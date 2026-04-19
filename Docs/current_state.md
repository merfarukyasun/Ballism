# Ballism — Güncel Durum
_Son güncelleme: Faz 2.5 uygulandı (stabil değil) + Mimari pivot kararı alındı (kod yazılmadı)_

---

## Özet

- **Kod durumu:** Faz 2.5 tamamlandı (köşe tabanlı spawn, neon/trail, discrete çarpışma) ama **stabil değil**.
- **Son surgical edit:** `GridManager.BounceFromCorner` ConvexCorner dalına `revOpen` kontrolü eklendi.
- **Büyük karar:** Ballism'in gerçek vizyonu **sürekli (continuous) sandbox simülasyonu**. Mevcut discrete/step-based hareket modeli yanlış temel — Faz 2'de tümüyle yeniden yazılacak.
- **Commit durumu:** Bu oturumda hiç commit atılmadı. Branch: `dev`. Son commit: Faz 0 (695fafd).

---

## Mimari Pivot (karar verildi, uygulanmadı)

**Eski model (şu an kodda):** Top hücre→hücre veya köşe→köşe ayrık adımlarla hareket eder; her adımda `Bounce`/`BounceFromCorner` yön döndürür. Çarpışma = discrete pozisyon eşitliği.

**Yeni model (hedef):**
- Top sürekli (`float` pozisyon, `Vector2` velocity) hareket eder.
- Duvar çarpışması = **swept circle-line segment** (önceden hesaplanan toi + reflect).
- Grid **yalnızca editör/referans** — simülasyon durumunun parçası değil.
- Katmanlı mimari: **L1 Config → L2 Grid Editor → L3 Geometry (edge→segment set) → L4 Simulation (BallBody + WallCollisionService) → L5 Presentation → L6 UI/State**.

Tam plan için bkz. `next_phase_plan.md`.

---

## Oyun Akışı (şu anki kod)

```
Drawing ──[Onayla]──► RegionSelect ──[tıkla]──► SpawnSelect ──[Başlat]──► Simulating ⇄ Paused
   ▲         hover         iç/dış          köşe seç, ≤3 top                    │
   └─────────────────────── [Sıfırla] ─────────────────────────────────────────┘
                                                    ▲ [Top Ekle]
```

### State → Input → Eylem

| State | Aktif giriş | Ne olur |
|---|---|---|
| Drawing | `GridManager.Update()` | Kenar toggle, sürükle |
| RegionSelect | `GameManager.Update()` her kare | Hover yeşil, tıkla seç |
| SpawnSelect | `GameManager.Update()` | Köşeye snap, yön oku, top yerleştir |
| Simulating | `BallController.Update()` | Discrete adım + corner bounce |
| Paused | — | Bekle |

---

## Mevcut Kod Yapısı (son hali)

### `GridManager.cs` (~680 satır)
- Edge-based editor: `hEdges[height+1, width]`, `vEdges[height, width+1]`
- `ComputeRegions()` BFS flood fill; `exteriorRegion` + `allRegions`
- `ShowRegionPreviews`, `HoverRegionAtCell`, `SelectRegionAtCell`
- `RefreshPlayableEdges` (inner/border/hidden)
- **Corner API (Faz 2):** `CornerToWorld`, `WorldToNearestPlayableCorner`, `GetValidCornerDirections`, `BounceFromCorner`
- **Son edit:** `BounceFromCorner` ConvexCorner dalı `revOpen` kontrolü eklendi — çapraz açık karşı hücre yoksa sadece 2 yansımadan seçim yapılıyor.

### `BallController.cs` (289 satır)
- İki mod: `Init` (cell) / `InitAtCorner` (corner). `useCornerMode: bool` ayrımı.
- Discrete step: `Vector3.Lerp(fromPos, toPos, SmoothStep(t/stepDur))`
- `ResolveDirection` (cell) → `GridManager.Bounce`
- `ResolveDirectionCorner` → `GridManager.BounceFromCorner`
- `SetupVisuals(color)`: Glow child (scale 2.8, alpha 0.15) + `TrailRenderer` (time 0.4, width 0.28→0)
- `CurrentPosition` (useCornerMode'a göre), `Direction { get; set; }`, `CornerPosition`, `GridPosition`
- **Faz 2'de tamamen yeniden yazılacak** (sürekli hareket + `Rigidbody2D`-benzeri model).

### `GameManager.cs` (316 satır)
- State machine (`Drawing/RegionSelect/SpawnSelect/Simulating/Paused`)
- `OnConfirmShape`, `OnStartSimulation`, `OnAddBall`, `OnTogglePause`, `OnReset`, `OnClearShape`
- `ShowCornerIndicators(corner)` → `SpawnIndicatorAt` (per direction)
- `SpawnBall(corner, dir)` → `bc.InitAtCorner(...)` → `SetPaused(true)`
- `CheckBallCollisions()` LateUpdate'te çağrılır — discrete pozisyon eşitliği + Direction swap. **Stabil değil.**
- `pendingSpawn: Vector2Int` corner koordinat.

### `UIManager.cs` (122 satır)
- `RefreshUI(state)` buton görünürlüğü + stateLabel + instructionLabel (switch).
- `Update()` SpawnSelect için dinamik yönerge + startButton visibility (BallCount'a göre).
- `ballCountLabel` her kare güncellenir.

### Değişmeyen
`DirectionIndicator.cs`, `GridCell.cs`, `BallismSetup.cs` (küçük: startButton + panel height)

---

## Açık Durumlar

- Faz 1 + 2 + 2.5 kodu **commit edilmedi**.
- Mevcut sahne `Ballceyda.unity`'de `startButton` yok (kullanıcı manuel eklemeli veya Setup rerun).
- GridManager'daki `Bounce` ve `BounceFromCorner` Faz 2 (continuous) uygulamasında tamamen kaldırılacak.

**Detaylı sorunlar için:** `known_issues.md`
**Sonraki fazlar için:** `next_phase_plan.md`
**Mimari detay için:** `architecture_notes.md`
