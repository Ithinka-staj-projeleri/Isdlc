# Faz 5 — Takım çalışması ve yapay zekâ yardımcıları

Bu dosya Faz 5'te yapılacak işleri, kimin neyi yapacağını ve işlerin sırasını anlatır.
Ortak kurallar için: [AGENTS.md](../AGENTS.md), ekip ve çalışma kuralları için: [PLANNING.md](../PLANNING.md).
Durum: **kapatıldı** (24 Kasım 2026). F1–F9 birleşti ve çapraz testleri yazıldı; F10 (demo) Faz 6'nın T8'iyle birlikte yapılacak ([phase-6-plan.md](phase-6-plan.md)).

## 1. Neden

Faz 4 kapatıldı; yapılmamış işleri iptal edildi ([phase-4-plan.md](phase-4-plan.md) → Kapanış).
17 Kasım 2026'da Volobox ekibi şirkete ürünlerini tanıttı. Volobox; proje ve görev yönetimi, toplantı notları,
destek talepleri, göstergeler ve yapay zekâ ajanlarını tek bir web uygulamasında birleştiren bir platform.
Faz 5'te bu tanıtımda görülen özelliklerden **iş analizi platformumuza uyanlar** alınır.

Seçim ölçütleri:
- İş analistinin, proje yöneticisinin ve ekibin günlük işini kolaylaştırıyor olmalı.
- Mevcut modüllerin üstüne kurulmalı; yeni bir dış sisteme (GitHub, Azure DevOps, e-posta sunucusu, takvim) bağlantı istememeli.
- Yapay zekâ kuralları aynen geçerli: ajan kayıt oluşturmaz, yalnızca formu doldurur; yalnızca sahte veriyle çalışılır.

## 2. Volobox'ta görülenler ve kararlar

| Volobox'ta | Karar | Faz 5'te |
| --- | --- | --- |
| Proje genel bakışı: açık / geciken görevler, önceliğe ve türe göre dağılım, ekip iş yükü, teslimat | Alınır | F1 |
| Kişisel ana sayfa: "Senden bekleyenler", "Bugünün planı" | Büyük kısmı panelde zaten var ("Bana bekleyen adımlar", "Bana atanan açık konular"); eksikler eklenir | F2 |
| İş Hikâyesi: bir işin baştan sona akışı | Alınır | F3 |
| Toplantıdan kişi kişi görev çıkarma | Alınır; mevcut tutanak ajanının uzantısı | F4 |
| Toplantıya katılıp not tutan ajan | Canlı katılım alınmaz; kaydedilmiş sesten tutanak taslağı alınır | F5 |
| Destek e-postalarını okuyup görev açan iş akışı | E-posta kutusu bağlanmaz; yapıştırılan e-postadan talep taslağı alınır | F6 |
| Genel arama (Ctrl+K) | Alınır | F7 |
| Her kaydın bağlantısı | Alınır | F8 |
| Kişi profili: uzmanlık, çalışma notu; işi atarken profile ve iş yüküne bakma | Alınır | F9 |
| Doküman ve sohbetlerde anlamsal arama, kayda soru sorma | Sonraki sürüm ([ai-vision.md](ai-vision.md)); vektör veritabanı ve veri gönderme izni ister | — |
| Kişisel göstergeler ve hazır gösterge şablonları | Alınmaz; panel sabit kalır | — |
| Takvim ve toplantı çakışması | Alınmaz; takvim entegrasyonu ister | — |
| Müşterinin talep açtığı destek masası, dış kullanıcılar | Alınmaz; dış erişim açmayı gerektirir | — |
| Kodlama ajanı, önizleme, PR incelemesi, deploy, gizli bilgi yönetimi | Alınmaz; iş analizi aracının kapsamı dışında | — |
| Sürüm notu ve sosyal medya duyurusu, uygulama oluşturma, beyaz tahta | Alınmaz; kapsam dışı | — |

## 3. Yapılacak işler

Her satır en az bir PR'dır. Modül içi sıra aynı kalır: önce sözleşme (`docs/…-api.md`), sonra entity ve migration, sonra API, en son ekran.
Sorumlular **öneridir**; modül sahipliğine göre dağıtıldı, ekip onaylarken değiştirebilir.

### Göstergeler ve görünürlük

| No | İş | Sorumlu | Bağımlılık |
| --- | --- | --- | --- |
| F1 | **Proje özeti** (proje detayının "Genel Bilgiler" sekmesinin üstü): durumuna ve önceliğine göre açık konular, geciken açık konular, seviyesine göre açık riskler, durumuna göre gereksinimler, onaylanan / takip edilen dokümanlar, sorumluya göre açık konu sayısı (ekip iş yükü), son 30 günde kapanan açık konular. Tek istek: `GET /projects/{id}/summary`. Yeni tablo yok | Dide | — |
| F2 | **Panel eklemeleri:** "Termini bu hafta dolan açık konularım" listesi ve "Ekip iş yükü" (aktif projelerde kişi başına açık konu ve bekleyen adım sayısı). `DashboardDto`'ya eklenir | Dide | — |
| F3 | **Proje zaman çizelgesi** (proje detayında yeni sekme): süreç kayıtları, tutanaklar, kararlar (`OpenIssueType.Decision`), onaylanan gereksinimler ve kapanan riskler tek akışta, tarihe göre; satır ilgili kayda gider. Mevcut kayıtlardan hesaplanır, yeni tablo yok | Erva | — |

### Yapay zekâ yardımcıları

| No | İş | Sorumlu | Bağımlılık |
| --- | --- | --- | --- |
| F4 | **Tutanaktan kişi kişi aksiyon:** tutanak taslağındaki her aksiyona sorumlu önerisi (katılımcılardan, sistem kullanıcısıyla eşleşirse `OwnerUserId`, yoksa `OwnerName`) ve metinde geçiyorsa termin. Öneri formu doldurur; açık konuyu kişi kaydeder | Erva | — |
| F5 | **Ses kaydından tutanak taslağı:** tutanak formunda ses / video dosyası seçilir; `ai/` servisi kaydı **kendi makinesinde** metne çevirir (faster-whisper), metin mevcut tutanak taslağına verilir. Ses hiçbir dış servise gitmez. Paket (faster-whisper, isteğe bağlı `requirements-audio.txt`) 20 Kasım 2026'da onaylandı; ffmpeg gerekmedi (ses PyAV ile çözülür). Dosya boyutu sınırı ve uzun kayıtta bekleme süresi sözleşmede belirlenir | Erva | F4 ile aynı ajan |
| F6 | **E-postadan talep taslağı:** yeni talep formunda "E-postadan doldur"; yapıştırılan e-posta metninden talep adı, talep sahibi, talebi ileten kişi ve iletişim bilgisi, talep türü ve notlar önerilir, kanal E-posta seçilir. Öneri formu doldurur; talebi kişi kaydeder | Erva | — |

### Gezinme ve kişiler

| No | İş | Sorumlu | Bağımlılık |
| --- | --- | --- | --- |
| F7 | **Genel arama** (üst barda, Ctrl+K): projeler (no, ad, talep sahibi), açık konular, tutanaklar, riskler ve gereksinimler (kod ve başlık); her türden en fazla 5 sonuç, sonuç kayda gider. `GET /search?q=` (en az 2 karakter) | Baran | — |
| F8 | **Bağlantıyı kopyala:** proje, süreç iyileştirme önerisi ve proje içi kayıtların detayında düğme; bağlantı sekmeyi ve kaydı açar (`?tab=risks&id=12`). Kayıt sekmeleri `id` parametresiyle ilgili kaydı açacak şekilde güncellenir | Baran | — |
| F9 | **Kişi profili ve sorumlu önerisi:** kullanıcıya uzmanlık alanları ve "benimle nasıl çalışılır" notu eklenir; kişinin profil sayfası (rol, uzmanlık, açık konuları, bekleyen adımları). Açık konu formunda sorumlu seçilirken her kişinin açık konu sayısı ve uzmanlığı gösterilir | Baran | F2 (iş yükü hesabı ortak) |

### Bitiriş

| No | İş | Sorumlu | Bağımlılık |
| --- | --- | --- | --- |
| F10 | **Demo senaryosu** Faz 4 akışları (yeni yazılım projesi, ek geliştirme, süreç iyileştirme) ve Faz 5 özellikleriyle baştan yazılır ve denenir | Erva | Hepsi |

### Çapraz testler

Kural aynı kalır (PLANNING.md → Birlikte çalışma kuralları): Erva → Baran'ın, Baran → Dide'nin, Dide → Erva'nın işleri.

## 4. Sıra

```
Dalga 1 (birbirini beklemez)   F1 proje özeti · F3 zaman çizelgesi · F4 tutanak aksiyonları · F7 genel arama · F8 bağlantı
Dalga 2                        F2 panel eklemeleri · F5 ses kaydı · F6 e-posta taslağı · F9 kişi profili
Dalga 3                        F10 demo · çapraz testler
```

- F5 için paket onayı Dalga 1'de istenir; onay gelmezse F5 iptal edilir, F4 tek başına kalır.
- F2 ve F9 aynı iş yükü hesabını kullanır: Dide hesabı F2'de yazar, Baran F9'da kullanır.
- **Migration:** F9 dışında yeni tablo ya da kolon beklenmiyor. Kural aynı: PR'dan hemen önce main'i alıp üretin, PR başına tek migration.

## 5. Ortak dosyalar ve dokümanlar

| Dosya | Kim, ne zaman |
| --- | --- |
| `AGENTS.md` Bölüm 6 | Yeni kavramlar (proje özeti, zaman çizelgesi, kullanıcı uzmanlığı) kodu yazan kişi tarafından, kod PR'ından önce eklenir |
| `docs/data-model.md` | F9'da `User` değişikliğiyle Baran günceller |
| `docs/dashboard-api.md` | F2 ile Dide günceller |
| `docs/ai-vision.md`, `ai/README.md` | F4–F6 ile Erva günceller |
| Üst bar ve menü | F7 arama kutusu üst bara; yeni menü yok |

## 6. Açık konular

1. **Sorumlular:** Bölüm 3'teki dağılım öneridir; ekip onaylar.
2. **F5 paket onayı:** onaylandı (20 Kasım 2026). Model ilk çalışmada indirilir (`small` ~500 MB); GPU yoksa 1 saatlik kayıt dakikalar sürer.
3. **Gerçek kayıtlar:** Şirket verisi gönderme kararı gelene kadar F5 ve F6 yalnızca sahte kayıt ve e-postalarla denenir. Ses metne çevrilirken bilgisayardan çıkmaz, ama metin taslak için yine modele gider.
