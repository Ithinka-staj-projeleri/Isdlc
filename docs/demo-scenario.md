# Uçtan Uca Demo Senaryosu

MVP'nin baştan sona gösterimi: bir talebin açılması, yöneticinin onayıyla projeye dönüşmesi ve projede çalışılması.
Süre: yaklaşık **20 dakika**. Sorumlu: Erva. Durum: **taslak**, 10 Kasım 2026'da baştan sona denendi. Bekleyen modüller
gelince ilgili adımlar eklenecek (bkz. "Bekleyen parçalar").

Anlatılacak hikâye: *"Bir talep sisteme girdiği andan itibaren 13 aşamalı ISDLC sürecinde kimin ne yapacağı belli;
sistem sırayı, yetkiyi ve onayları kendisi takip ediyor; toplantılar, açık konular, gereksinimler ve dokümanlar projenin
içinde tutuluyor; her değişiklik kayıt altında."*

## Hazırlık (demodan önce)

| # | Ne | Nasıl |
| --- | --- | --- |
| 1 | **Temiz veritabanı** | `docker compose down -v` ve `docker compose up -d`, sonra API'yi başlat. Seed verisi (13 aşama, kullanıcılar, 9 örnek proje) baştan yüklenir. Deneme sırasında değişen kayıtlar demoya karışmaz |
| 2 | Gizli ayarlar | `Jwt:SigningKey` ve `Identity:TestPassword` tanımlı olmalı (README → Backend) |
| 3 | Yapay zekâ servisi | `ai/.env` içinde `GEMINI_API_KEY`, sonra `docker compose --profile ai up -d --build`. Tutanak formunda yapay zekâ bölümü görünüyor mu kontrol et |
| 4 | Uygulama | API (`dotnet run --project backend/src/Isdlc.Api`) ve frontend (`frontend` klasöründe `npm start`) açık: http://localhost:4200 |
| 5 | **İki tarayıcı penceresi** | Biri normal, biri gizli pencere. Normal pencerede **Elif Yılmaz** (İş Analisti), gizli pencerede **Mert Kaya** (Yazılım Departman Yöneticisi) giriş yapmış olsun |
| 6 | Toplantı notu | Aşağıdaki "Sahte toplantı notu" panoya kopyalanmış olsun |
| 7 | Prova | Senaryoyu bir kez baştan sona dene; sonra 1. adımı tekrarla (veritabanını sıfırla) |

**Yalnızca sahte veri kullanılır.** Toplantı notları yapay zekâ servisine gönderilir; gerçek müşteri bilgisi girme.

### Demodaki kişiler

| Kişi | Rol | Ne yapar |
| --- | --- | --- |
| Elif Yılmaz (`elif.yilmaz@isdlc.local`) | İş Analisti | Talebi açar, süreci yürütür, tutanak, açık konu, gereksinim, doküman |
| Mert Kaya (`mert.kaya@isdlc.local`) | Yazılım Departman Yöneticisi | 3.2'de talebi proje olarak kabul eder (onay adımı) |
| Ayşe Demir | Proje Yöneticisi | Talepte proje yöneticisi olarak seçilir |
| Can Öztürk | Yazılım Geliştirici | Toplantı notlarında aksiyon sahibi olarak geçer |

Parola, ekipçe belirlenen test parolasıdır (`Identity:TestPassword`); belgeye yazılmaz.

## Akış

Her adımda: **Ekran** → **Yapılacak** → **Görülmesi gereken** → *Anlatım notu*.

### 1. Giriş ve süreç (2 dk)

| # | Ekran | Yapılacak | Görülmesi gereken |
| --- | --- | --- | --- |
| 1.1 | Giriş | Elif olarak giriş yap | Sağ üstte "Elif Yılmaz · İş Analisti" |
| 1.2 | Gösterge Paneli | Göster | **Bekliyor** (ekranı Dide yazıyor): bana bekleyen işler, projelerin genel durumu |
| 1.3 | Menü → Süreç Kütüphanesi | 5. aşamayı seç | 13 aşama, 75 adım, 21 karar noktası, 3 onay adımı. 5.1'de "Onay: Yazılım Departman Yöneticisi", 5.3'te "Hayır ise: Talep kapanır — Kaybedildi" |

*Anlatım: "Şirketin ISDLC süreci sisteme veri olarak girildi: her adımın sorumlusu, onaycısı ve 'Hayır' cevabında ne
olacağı tanımlı. Ekranlar bu tanıma göre çalışıyor."*

### 2. Talebin açılması (2 dk)

| # | Ekran | Yapılacak | Görülmesi gereken |
| --- | --- | --- | --- |
| 2.1 | Menü → Talepler | "+ Yeni talep" | Buton yalnızca İş Analisti'ne görünür |
| 2.2 | Yeni talep | Proje adı: *Saha Ekibi Stok Takibi*, açıklama: *Saha ekibinin depo stoklarını mobil cihazdan sayabilmesi ve farkların merkeze iletilmesi.*, talep türü: Yeni yazılım projesi, kaynak: Dış, kanal: Yazılı, müşteri / birim: *Demo Lojistik A.Ş.*, talep sahibi: *Hakan Yıldız*, iş analisti: Elif Yılmaz, proje yöneticisi: Ayşe Demir, başlangıç: bugün. **Talebi kaydet** | Proje kodu verilir (`ISDLC-2026-0xx`), durum **Aktif**, mevcut adım **1.1** |
| 2.3 | Talepler listesi | Göster | Yeni talep listede (1–3. aşamadaki kayıtlar "Talepler"de, 4. aşamadan sonra "Projeler"de) |

*Anlatım: "Talep açılınca süreç kendiliğinden 1.1'den başlar; kod sırayla verilir."*

### 3. Süreçte ilerleme ve yetki (4 dk)

Proje detayı → **Süreç** sekmesi.

| # | Yapılacak | Görülmesi gereken |
| --- | --- | --- |
| 3.1 | 1.1: not yaz (*"Talep e-postayla geldi."*), "Adımı tamamla" | 1.1 "Tamamlandı", mevcut adım 1.2, ilerleme çubuğu ilerler; üstteki özet de yenilenir |
| 3.2 | 1.2 (karar): önce **"Hayır → talep kapanır (Yönlendirildi)"** butonuna bas, sonra **Vazgeç** | Onay penceresi: talebi kapatan cevaplar geri alınamadığı için önce sorulur. Vazgeçince proje 1.2'de kalır |
| 3.3 | 1.2: "Evet → sonraki adım"; 1.3 tamamla | Ekran 2. aşamaya kendiliğinden geçer |
| 3.4 | 2.1 tamamla, 2.2 "Evet", 2.4 tamamla, 3.1 tamamla | **Bekliyor** (Baran): 2.4 ve 3.1'de seviye belirleme ve kabul puanlaması (`Assessment`) bu adımlardan açılacak |
| 3.5 | 3.2'ye gel | Butonlar yerine: *"Bu adım Yazılım Departman Yöneticisi tarafından onaylanır."* |

*Anlatım: "Kimin hangi adımı yapabileceğine sistem karar veriyor; analist onay adımını geçemez."*

### 4. Yöneticinin onayı (2 dk)

Gizli pencere → **Mert Kaya**.

| # | Yapılacak | Görülmesi gereken |
| --- | --- | --- |
| 4.1 | Talepler → *Saha Ekibi Stok Takibi* → Süreç | Mert'te "+ Yeni talep" butonu yok (yalnızca İş Analisti). 3.2'de "Evet → sonraki adım", "Hayır → talep kapanır (Reddedildi)" ve "Talebi reddet…" |
| 4.2 | "Evet → sonraki adım" (isteğe bağlı not: *"Bütçe onaylandı, öncelik yüksek."*) | 3. aşama tamamlandı, mevcut adım **4.1**. Kayıt artık **Projeler** listesinde |
| 4.3 | — | **Bekliyor** (Baran): Elif'e "talep kabul edildi" bildirimi (`Notifications`) |

*Anlatım: "Yönetici yalnızca kendisini bekleyen adımı görür ve onaylar; reddetmek isterse gerekçe zorunludur."*

*Yedek gösterim (zaman varsa):* Mert, bir örnek projede "Talebi reddet…" ile gerekçe yazarak reddeder → aşama
"durduruldu", "Süreç sona erdi: Reddedildi".

### 5. Toplantı tutanağı ve yapay zekâ (4 dk)

Normal pencere (Elif) → proje → **Toplantı Tutanakları** sekmesi → "+ Tutanak oluştur".

| # | Yapılacak | Görülmesi gereken |
| --- | --- | --- |
| 5.1 | Tür: Analiz Oturumu, tarih: bugün | — |
| 5.2 | "Ham toplantı notları"na sahte notu yapıştır → **"Yapay zekâ ile karar ve aksiyonları çıkar"** | Birkaç saniye sonra karar (tablet desteğinin ilk fazda olmaması) karar kutusuna, aksiyon (mobil ekran taslakları) satır olarak dolar. Can Öztürk listeden seçili gelir, "cuma" gerçek tarihe çevrilir, aksiyonun altında dayanak cümle görünür. Formda yeri olmayan bağımlılık (depo listesi — "gelecek hafta salı" da tarihe çevrilir), soru (sayım farkı onay tutarı) ve varsayım (telefon kamerası) ayrıca listelenir. *Model her seferinde birebir aynı cevabı vermeyebilir; sayılar az farklı olabilir* |
| 5.3 | Aksiyonu kontrol et, katılımcılar (*Elif Yılmaz, Can Öztürk, Hakan Yıldız (Demo Lojistik)*) ve gündemi (*Mobil stok sayımı kapsamı*) yaz, **"Tutanağı kaydet"** | Tutanak detayı açılır: `TT-001`; aksiyon `AK-001` (açık), karar `AK-002` (kapalı) |

*Anlatım: "Yapay zekâ kayıt oluşturmaz, öneri hazırlar; kaydeden her zaman kişidir. Kişisel bilgiler modele gitmeden
gizlenir."*

### 6. Açık konular (2 dk)

**Açık Konular** sekmesi.

| # | Yapılacak | Görülmesi gereken |
| --- | --- | --- |
| 6.1 | Göster | Tutanaktan gelen kayıtlarda "Tutanak: TT-001"; termini geçmişse "gecikti" |
| 6.2 | "+ Kayıt ekle": tür *Soru*, konu: *Saha ekibi çevrimdışı çalışabilmeli mi?*, sorumlu: sistem dışı — *Hakan Yıldız*, termin: gelecek hafta | Yeni `AK-…` kaydı |
| 6.3 | Bir aksiyonu "Kapat" | Durum "Kapandı" |

### 7. Gereksinimler ve dokümanlar (2 dk)

| # | Sekme | Yapılacak | Görülmesi gereken |
| --- | --- | --- | --- |
| 7.1 | Gereksinimler | "+ Gereksinim ekle": tür Fonksiyonel, öncelik Olmazsa olmaz, başlık *Saha personeli stok sayımını mobil cihazdan girebilmeli*. Kaydet | `GR-001`, durum Taslak |
| 7.2 | Gereksinimler | Satırdaki durum seçiminden "İncelemede" | Durum hemen kaydedilir |
| 7.3 | Dokümanlar | 4. aşamada *PKD — Proje Kapsam Dokümanı*: durumu "Hazırlanıyor" yap; "Not / bağlantı" ile bağlantı ekle (ör. *https://intranet.example/isdlc/pkd-demo.docx*) | Durum ve "Dokümanı aç ↗" bağlantısı görünür |
| 7.4 | — | **Bekliyor** (Baran): 4.8'de risk kaydı ve olasılık × etki puanı (`Risks`) |

### 8. Kayıt geçmişi (1 dk)

**Kayıt Geçmişi** sekmesi.

| # | Yapılacak | Görülmesi gereken |
| --- | --- | --- |
| 8.1 | Göster | Bu demoda yapılan her şey, en yeni önce: doküman bağlantısı ("Bağlantı — → https://…"), gereksinim ("Durum Taslak → İncelemede"), tutanak ve açık konular. Yeni kayıtlarda "5 alan" gibi katlanmış ayrıntı |
| 8.2 | Kayıt türü: *Proje* | Yalnızca projenin kendi değişiklikleri; en üstte **Mert Kaya — "Mevcut adım 3.2 → 4.1"** |

*Anlatım: "Kayıtlar silinmez, her değişiklik kimin yaptığıyla birlikte saklanır; değiştirilemez."*

### Kapanış (1 dk)

Talepler ve Projeler listeleri, gösterge paneli (hazır olduğunda). Sonraki adımlar: seviye ve kabul puanlaması, risk
yönetimi, bildirimler; yapay zekâ tarafında form doldurma ve belge yükleme (bkz. [ai-vision.md](ai-vision.md)).

## Sahte toplantı notu

```
Saha Ekibi Stok Takibi – Analiz toplantısı
Katılımcılar: Elif Yılmaz, Can Öztürk, Hakan Yıldız (Demo Lojistik)

Saha personeli stok sayımını telefondan girecek; tablet desteği ilk fazda olmayacak, karar verildi.
Depo listesini Demo Lojistik BT gelecek hafta salıya kadar gönderecek.
Mobil ekran taslaklarını Can Öztürk cuma gününe kadar hazırlayacak.
Sayım farklarının hangi tutardan sonra onaya düşeceği belli değil, Hakan Bey finansla konuşacak.
Barkod okuyucu olarak telefon kamerasının yeterli olacağı varsayılıyor.
```

## Bekleyen parçalar

| Adım | Eksik | Modül | Sorumlu |
| --- | --- | --- | --- |
| 1.2, Kapanış | Gösterge paneli ekranı (API hazır) | `Dashboard` | Dide |
| 3.4 | Seviye belirleme ve kabul puanlaması (2.4, 3.1) | `Assessment` | Baran |
| 4.3 | Uygulama içi bildirimler | `Notifications` | Baran |
| 7.4 | Risk kaydı ve puanlama (4.8) | `Risks` | Baran |

Her parça geldiğinde ilgili satır gerçek adıma çevrilir ve senaryo baştan prova edilir.

## Bir şey ters giderse

| Durum | Ne yapılır |
| --- | --- |
| Yapay zekâ bölümü görünmüyor | Servis kapalı ya da az önce açıldı (durum en fazla 30 sn önbellekte). Sayfayı yenile; olmazsa kararları ve aksiyonları elle gir — uygulamanın geri kalanı etkilenmez |
| Yapay zekâ "servis yoğun" diyor | Bir kez daha dene; olmazsa elle gir |
| "Proje bu arada başka bir adıma geçti" | İki pencerede aynı projede işlem yapıldı; ekran kendini yeniler, devam et |
| Giriş yapılamıyor | Test parolası tanımlı mı (README); veritabanı sıfırlandıysa API'yi parola tanımlıyken yeniden başlat |
