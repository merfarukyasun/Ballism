# Ballism

Ballism, oyuncunun kendi oyun alanını oluşturduğu ve topların bu alan içindeki hareketini izlediği bir fizik/simülasyon oyunu fikridir.

Oyunun temelinde, kenarlar çizerek bir alan oluşturmak, bu alanın hangi kısmında oynanacağını seçmek ve daha sonra topları bu alanın içindeki geçerli noktalardan başlatıp hareketlerini takip etmek vardır. Amaç klasik bir level tabanlı oyun yapmaktan çok, önce sağlam ve keyifli bir sandbox/simülasyon çekirdeği oluşturmaktır.

## Oyun Fikri

Ballism’de grid, doğrudan oynanışın kendisi değil; daha çok alan oluşturmak ve referans noktaları belirlemek için kullanılan bir düzen aracıdır.

Oyuncu:

- Kenarlar çizerek bir şekil oluşturur
- İç veya dış bölgeden hangisinin oyun alanı olacağını seçer
- Topları oyun alanındaki uygun noktalara yerleştirir
- Topun hangi yönde hareket edeceğini belirler
- Sonra da topların kenarlara, köşelere ve ileride birbirlerine çarpışmasını izler

Uzun vadede Ballism, yalnızca tek bir prototip olarak kalmayacak; farklı modlar, daha gelişmiş ayarlar ve daha güçlü bir simülasyon yapısıyla büyüyecek.

## Şu Ana Kadar Neler Yapıldı?

Proje şu anda aktif prototip aşamasında. Şimdiye kadar üzerinde çalışılan ana sistemler şunlar:

- Unity projesi ve temel sahne yapısı kuruldu
- Grid tabanlı alan oluşturma sistemi geliştirildi
- Hücre boyama yaklaşımından çıkılıp kenar çizme yaklaşımına geçildi
- Bölge seçimi için iç/dış alan mantığı oluşturuldu
- Oynanabilir alanın görsel olarak ayrılması sağlandı
- Topların köşe tabanlı yerleştirilmesi için sistem geliştirildi
- Top yerleştirme akışı daha kontrollü hale getirildi
- Pause / resume davranışı üzerinde çalışıldı
- Kamera için zoom ve pan kontrolleri eklenmeye başlandı
- GitHub üzerinden prototip checkpoint’leri alınarak sürüm takibi yapılmaya başlandı

Kısacası proje şu anda “fikir aşaması”nı geçmiş durumda; oynanabilir ve test edilebilir bir prototip çekirdeği oluştu.

## Şu Anda Odak Noktası Ne?

Şu an en önemli hedef, oyunun temel simülasyon hissini doğru oturtmak.

Yani öncelik:

- Top hareketinin akıcı ve tatmin edici olması
- Alan oluşturma sisteminin rahat kullanılması
- Top yerleştirme deneyiminin açık ve anlaşılır olması
- Kamera ve oyun içi kontrollerin sandbox hissini desteklemesi
- Sistemin yeni özellik ekledikçe bozulmayan temiz bir yapıya kavuşması

## Sıradaki Hedefler

Geliştirmenin bir sonraki aşamalarında planlanan başlıca şeyler şunlar:

- Top hareket sistemini daha sürekli ve doğal hale getirmek
- Top-top çarpışmasını düzgün şekilde eklemek
- Kamera kontrolünü daha rahat ve sınırlı hale getirmek
- Oyun içi ayarlar paneli eklemek
- Simülasyon hızı, top hızı ve benzeri değerleri kullanıcıya açmak
- Ana menü eklemek
- Ses ve müzik sistemi eklemek
- Yeni oyun modları geliştirmek
- Genel görsel kaliteyi ve geri bildirimi iyileştirmek

## Kontroller

Kontroller şu anda geliştirme sürecine göre değişebiliyor, ancak hedeflenen temel kullanım şöyle:

- Mouse ile alan çizme / seçim
- Mouse ile köşe seçme
- Mouse ile yön belirleme
- Mouse scroll ile yakınlaştırma / uzaklaştırma
- Sağ tık ile kamera gezdirme
- Space ile durdurma / devam ettirme

## Teknik Taraf

Projede şu araçlar ve yapı kullanılıyor:

- **Unity 6.3 LTS**
- **URP 2D**
- **C#**
- **Git / GitHub**
- Prototipleme ve mimari planlama tarafında yapay zeka destekli geliştirme yaklaşımı

## Projenin Durumu

Ballism şu anda erken geliştirme aşamasında. Bazı sistemler çalışıyor, bazıları hâlâ yeniden tasarlanıyor, bazıları ise sadece temel prototip olarak mevcut.

Bu depo, oyunun fikirden çalışan bir sandbox çekirdeğine dönüşme sürecini adım adım takip etmek için tutuluyor.

## Not

Bu proje aktif olarak gelişiyor. Yapı, mekanikler ve görsel detaylar zaman içinde değişebilir. Ama ana hedef aynı:

**Oyuncunun kendi alanını kurduğu ve topların o alan içindeki davranışını keyifle izlediği güçlü bir simülasyon oyunu yapmak.**
