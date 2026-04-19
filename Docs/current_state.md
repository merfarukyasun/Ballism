# Ballism — Güncel Durum
_Son güncelleme: `prototype-v0.2` checkpoint + Spawn UX / Pause Semantics intermediate pass_

---

## Özet

- **Checkpoint:** `prototype-v0.2` (commit `4c3eabb`) — stabil baseline, remote'a push edildi.
- **Intermediate UX pass** uygulandı: iki fazlı spawn, occupied corner takibi, safe-corner-stop pause. Commit edilmedi.
- **Büyük karar (değişmedi):** Ana Faz 2 hâlâ **continuous motion rewrite**. Bu UX pass ara katman; continuous rewrite geldiğinde büyük kısmı doğal olarak çöpe gidecek.
- **Branch:** `dev`. Son commit: `4c3eabb` (tag: `prototype-v0.2`).

---

## Mimari Pivot (karar korunuyor, uygulanmadı)

**Eski model (şu an kodda):** Top hücre→hücre veya köşe→köşe ayrık adımlarla hareket eder; her adımda `Bounce`/`BounceFromCorner` yön döndürür.

**Yeni model (Faz 2 — continuous rewrite):**
- Top sürekli (`float` pozisyon, `Vector2` velocity) hareket eder.
- Duvar çarpışması = **swept circle-line segment**.
- Katmanlı mimari: **L1 Config → L2 Grid Editor → L3 Geometry → L4 Simulation → L5 Presentation → L6 UI/State**.

Tam plan için bkz. `next_phase_plan.md`.

---

## Oyun Akışı (son hali)

```
Drawing ──[Onayla]──► RegionSelect ──[tıkla]──► SpawnSelect ──[Başlat]──► Simulating ⇄ Paused
   ▲         hover         iç/dış          ┌──────────────┐                     │
   │                                       │ CornerPick   │             [Durdur] = safe-
   │                                       │   ↓ click    │              corner-stop
   │                                       │ DirectionPick│              (top bir sonraki
   │                                       └──────────────┘               köşeye oturup durur)
   └─────────────────────────── [Sıfırla] ────────────────────────────────┘
                                                    ▲ [Top Ekle]
```

### State → Input → Eylem

| State | Aktif giriş | Ne olur |
|---|---|---|
| Drawing | `GridManager.Update()` | Kenar toggle, sürükle |
| RegionSelect | `GameManager.Update()` | Hover yeşil, tıkla seç |
| SpawnSelect (CornerPick) | `GameManager.Update()` | Hover halo (sarı/kırmızı); tıkla → DirectionPick |
| SpawnSelect (DirectionPick) | `GameManager.Update()` | Selected halo (mavi) + yön okları (turuncu); ok tıkla → spawn; boşluk → iptal |
| Simulating | `BallController.Update()` | Discrete adım + corner bounce |
| Simulating + `pauseRequested` | — | "DURDURULUYOR…" — toplar köşeye oturana kadar |
| Paused | — | Bekle; toplar köşede duruyor, occupied |

---

## Mevcut Kod Yapısı (son hali)

### `GridManager.cs` (~680 satır) — DOKUNULMADI
- Edge-based editor + region + corner API + Bounce/BounceFromCorner.
- Faz 2 continuous rewrite'ta `Bounce*` kaldırılacak.

### `BallController.cs` (+~15 satır)
- İki mod: `Init` (cell) / `InitAtCorner` (corner).
- `SetupVisuals` TrailRenderer fake-null fix uygulandı.
- **Yeni:** `pauseAtNextCorner` flag + `RequestPauseAtNextCorner()` + `OnStoppedAtCorner` callback + `IsPaused` getter.
- Corner mode `t >= stepDur` dalında, yeni `toPos` ayarlandıktan sonra pause check; top tam köşede (`fromPos = toPos = cornerPos`, `t = 0`) kendini pause'lar ve GM'ye bildirir.

### `GameManager.cs` (~575 satır)
- State machine korundu (**yeni GameState eklenmedi**).
- **Yeni enum (private):** `SpawnPhase { CornerPick, DirectionPick }`.
- **Yeni field'lar:** `occupiedCorners: HashSet<Vector2Int>`, `spawnPhase`, `selectedCorner`, `hoverHalo`, `selectedHalo`, `pauseRequested`, `ballsAwaitingStop`.
- **Yeni public property'ler:** `IsPickingCorner`, `IsStopping` (UIManager için).
- `Update()` SpawnSelect dalı iki fazlı: CornerPick (hover + click) → DirectionPick (ok click veya iptal).
- `SpawnBall` → `occupiedCorners.Add(corner)` + `bc.OnStoppedAtCorner = OnBallStoppedAtCorner`.
- `OnTogglePause` Simulating → tüm hareketli toplar için `RequestPauseAtNextCorner`, sayaç ile son top durunca Paused'a geç.
- `OnBallStoppedAtCorner` callback — `state != Simulating || !pauseRequested` guard ile gecikmiş çağrıları yutar.
- `OnReset` — **tüm** yeni field'lar güvenli sıfırlanır (occupied, pauseRequested, sayaç, halo'lar, spawnPhase, selectedCorner).
- `SetState(Simulating)` → `occupiedCorners.Clear()` (tek kaynak hakikat).
- Halo görselleri: `dirIndicatorPrefab` runtime'da reuse edilir; DirectionIndicator + Collider2D component'leri Destroy edilir. **Yeni sprite asset veya yeni C# sınıfı yok.**
- `CheckBallCollisions` metodu duruyor ama çağrılmıyor (Faz 5'e rezerv).

### `UIManager.cs` (+~15 satır)
- Start'ta `startButton` null uyarısı (mevcut null-safe guard'lar korundu).
- `Update()` SpawnSelect dalı — yönerge `IsPickingCorner`'a göre iki dalda.
- `Update()` Simulating dalı — `IsStopping` ise stateLabel "DURDURULUYOR…", aksi halde "SİMÜLASYON".

### Değişmeyen
`DirectionIndicator.cs`, `GridCell.cs`, `BallismSetup.cs`, `GridManager.cs`.

---

## Açık Durumlar

- UX pass kodu **commit edilmedi** (kullanıcı test sonrası karar verecek).
- Mevcut sahne `Ballceyda.unity`'de `startButton` yok (manuel eklenmeli veya Setup rerun — güvenli uyarı var, akış bozulmuyor).
- Continuous motion rewrite hâlâ sıradaki ana iş.

**Detaylı sorunlar için:** `known_issues.md`
**Sonraki fazlar için:** `next_phase_plan.md`
**Mimari detay için:** `architecture_notes.md`
