# Ballism — Bilinen Sorunlar
_Son güncelleme: `prototype-v0.2` sonrası + Spawn UX / Pause Semantics pass_

---

## ÇÖZÜLEN — `prototype-v0.2` içinde kapatıldı

### ✅ 1. `TrailRenderer` Unity fake-null
- `BallController.SetupVisuals` — explicit `if (trail == null)` deseni uygulandı.

### ✅ 2. Discrete ball-ball çarpışma stabilsiz
- `GameManager.LateUpdate` içindeki `CheckBallCollisions()` çağrısı kaldırıldı.
- Metod korundu, Faz 5 (continuous) kapsamında yeniden yazılacak.

### ✅ 3. Reset tam temizlemiyordu
- `ClearBalls` → önce `balls` listesi Destroy, sonra `Object.FindObjectsByType<BallController>` orphan sweep.

### ✅ 4. Spawn validation eksikti
- `SpawnBall` → `GetValidCornerDirections(corner).Contains(dir)` kontrolü + warning.

### ✅ 5. `startButton` null riski
- `UIManager.Start` içinde uyarı + mevcut `?.` safe guard'lar korundu. UI akışı bozulmuyor.

---

## ÇÖZÜLEN — UX pass içinde kapatıldı (commit bekliyor)

### ✅ 6. Spawn köşe ve yön seçimi iç içeydi
- `SpawnPhase { CornerPick, DirectionPick }` alt-state eklendi.
- Önce köşe seçilir (hover halo ile), sonra yön indicator'ları gösterilir.
- Seçili köşe mavi halo ile vurgulanır, indicator'lar turuncu — görsel ayrım net.

### ✅ 7. Dolu köşeye tekrar spawn
- `HashSet<Vector2Int> occupiedCorners` — spawn sonrası ve pause sonrası köşeler.
- CornerPick hover'da dolu köşe kırmızı halo ile gösterilir, tıklama reddedilir.

### ✅ 8. Pause anlık donma
- `BallController.RequestPauseAtNextCorner()` + `OnStoppedAtCorner` callback.
- Her top bir sonraki köşeye tam oturunca kendini pause'lar ve GM'ye bildirir.
- Tüm hareketli toplar durunca `GameState.Paused`'a geçilir.
- Resume (`OnTogglePause` Paused → Simulating) `occupiedCorners.Clear()` yapar, toplar devam eder.

---

## AÇIK — İzlenecek / Potansiyel

### 9. Pause → Reset race (guard'lar var, test edilmeli)
- `OnReset`'te `pauseRequested = false; ballsAwaitingStop = 0` sıfırlanır.
- `OnBallStoppedAtCorner` callback'inde `if (state != Simulating || !pauseRequested) return;` guard → gecikmiş çağrı ignore.
- **Hâlâ manuel test ister:** Durdur'a bas, toplar köşeye vurana ~0.1 sn kala Sıfırla → console temiz olmalı.

### 10. Dead-end top
- `BallController.ResolveDirectionCorner` trap durumunda `moving=false` yapıyor.
- `OnTogglePause` `IsMoving` filtresi uygular → dead-end top sayaca girmez.
- `IsPaused` filtresi ayrıca eklendi (önceden pause'lanmış top sayılmaz).
- Dead-end top `occupiedCorners`'a girmez — ama aslında o köşede duruyor. Sonraki SpawnSelect'te o köşe dolu görünmez → ufak tutarsızlık. Faz 2 continuous rewrite bunu çözüyor.

### 11. Corner movement "dışarıdan giriş"
- Belirli şekillerde top seçili olmayan hücreden geçebiliyor gibi — repro edilmedi.
- Faz 2 continuous modelde otomatik çözülür (geometri tabanlı çarpışma).

### 12. `Bounce` / `BounceFromCorner` kaldırılacak
- Faz 2 continuous rewrite bunları sileceği için iyileştirme yapılmıyor.

---

## Görsel / Render — Faz 3 polish

### 13. URP `Sprites/Default` pembe render riski
- `BallController.SetupVisuals` trail material.
- Şu an sorun raporlanmadı; Faz 3'te `Universal Render Pipeline/2D/Sprite-Unlit-Default`'a geçilecek.

### 14. Neon bloom yok
- Sadece soft glow child. Tam neon için URP Post Volume + Bloom override.
- Faz 3 polish.

### 15. Edge hover highlight yok (çizim modunda)
- Kullanıcı fareyle kenar üzerindeyken görsel feedback yok.
- Faz 3 polish.

---

## Git / Süreç

### 16. UX pass commit edilmedi
- Branch: `dev`. Son commit: `4c3eabb` (tag: `prototype-v0.2`).
- Kullanıcı manuel test sonrası commit kararı verecek.

---

## Referanslar

- `current_state.md` — mevcut kod durumu
- `architecture_notes.md` — hedef mimari
- `next_phase_plan.md` — faz bazlı plan
