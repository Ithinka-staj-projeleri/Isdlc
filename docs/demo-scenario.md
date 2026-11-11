# Uçtan Uca Demo Senaryosu

MVP'nin baştan sona gösterimi: bir talebin açılması, yöneticinin onayıyla projeye dönüşmesi ve projede çalışılması.
Süre: yaklaşık **25 dakika**. Sorumlu: Erva. Durum: **tamamlandı**; MVP'nin bütün modülleri senaryoda. Seviye, kabul,
bildirim ve risk adımları 11 Kasım 2026'da uygulamada denendi.

Anlatılacak hikâye: *"Bir talep sisteme girdiği andan itibaren 13 aşamalı ISDLC sürecinde kimin ne yapacağı belli;
sistem sırayı, yetkiyi ve onayları kendisi takip ediyor, sırası gelen kişiye bildirim gönderiyor; seviye ve kabul puanı,
riskler, toplantılar, açık konular, gereksinimler ve dokümanlar projenin içinde tutuluyor; her değişiklik kayıt altında."*

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
| 8 | Sürüm | main güncel olmalı: Seviye & Kabul (#96–#98), Riskler (#91–#93), Bildirimler (#101, #108, #109) ve gösterge panelindeki risk / seviye kartları (#105, #106) |

**Yalnızca sahte veri kullanılır.** Toplantı notları yapay zekâ servisine gönderilir; gerçek müşteri bilgisi girme.

### Demodaki kişiler

| Kişi | Rol | Ne yapar |
| --- | --- | --- |
| Elif Yılmaz (`elif.yilmaz@isdlc.local`) | İş Analisti | Talebi açar, süreci yürütür; seviye ve kabul puanlaması, risk, tutanak, açık konu, gereksinim, doküman |
| Mert Kaya (`mert.kaya@isdlc.local`) | Yazılım Departman Yöneticisi | Zildeki "onayınızı bekliyor" bildirimiyle talebe gelir, 3.2'de proje olarak kabul eder (onay adımı) |
| Ayşe Demir | Proje Yöneticisi | Talepte proje yöneticisi olarak seçilir; yüksek risk bildirimini alır |
| Can Öztürk | Yazılım Geliştirici | Toplantı notlarında aksiyon sahibi; riskin sorumlusu olarak "size atandı" bildirimini alır |

Parola, ekipçe belirlenen test parolasıdır (`Identity:TestPassword`); belgeye yazılmaz.

## Akış

Her adımda: **Ekran** → **Yapılacak** → **Görülmesi gereken** → *Anlatım notu*.

### 1. Giriş ve süreç (2 dk)

| # | Ekran | Yapılacak | Görülmesi gereken |
| --- | --- | --- | --- |
| 1.1 | Giriş | Elif olarak giriş yap | Sağ üstte "Elif Yılmaz · İş Analisti" |
| 1.2 | Gösterge Paneli | Göster | "Merhaba, Elif"; özet kartları (onay bekleyen talepler, aktif projeler, bana bekleyen adımlar, geciken açık konular, takvimi geçen projeler, **yüksek riskler**, bu yıl sonuçlananlar), **Bana bekleyen adımlar** listesi (her satır projenin Süreç sekmesine gider), aşamalara göre aktif kayıtlar ve altında **seviyeye göre dağılım**, **Yüksek riskler** ve **Kabul kararı bekleyen talepler** listeleri, son hareketler. "Tüm projeler / Benim projelerim" seçimi. Üst barda bildirim zili |
| 1.3 | Menü → Süreç Kütüphanesi | 5. aşamayı seç | 13 aşama, 75 adım, 21 karar noktası, 3 onay adımı. 5.1'de "Onay: Yazılım Departman Yöneticisi", 5.3'te "Hayır ise: Talep kapanır — Kaybedildi" |

*Anlatım: "Herkes giriş yapınca önce kendisini bekleyen işleri görür. Şirketin ISDLC süreci sisteme veri olarak girildi: her adımın sorumlusu, onaycısı ve 'Hayır' cevabında ne
olacağı tanımlı. Ekranlar bu tanıma göre çalışıyor."*

### 2. Talebin açılması (2 dk)

| # | Ekran | Yapılacak | Görülmesi gereken |
| --- | --- | --- | --- |
| 2.1 | Menü → Talepler | "+ Yeni talep" | Buton yalnızca İş Analisti'ne görünür |
| 2.2 | Yeni talep | Proje adı: *Saha Ekibi Stok Takibi*, açıklama: *Saha ekibinin depo stoklarını mobil cihazdan sayabilmesi ve farkların merkeze iletilmesi.*, talep türü: Yeni yazılım projesi, kaynak: Dış, kanal: Yazılı, müşteri / birim: *Demo Lojistik A.Ş.*, talep sahibi: *Hakan Yıldız*, iş analisti: Elif Yılmaz, proje yöneticisi: Ayşe Demir, başlangıç: bugün. **Talebi kaydet** | Proje kodu verilir (`ISDLC-2026-0xx`), durum **Aktif**, mevcut adım **1.1** |
| 2.3 | Talepler listesi | Göster | Yeni talep listede (1–3. aşamadaki kayıtlar "Talepler"de, 4. aşamadan sonra "Projeler"de) |

*Anlatım: "Talep açılınca süreç kendiliğinden 1.1'den başlar; kod sırayla verilir."*

### 3. Süreçte ilerleme, seviye ve kabul (6 dk)

Proje detayı → **Süreç** sekmesi.

| # | Yapılacak | Görülmesi gereken |
| --- | --- | --- |
| 3.1 | 1.1: not yaz (*"Talep e-postayla geldi."*), "Adımı tamamla" | 1.1 "Tamamlandı", mevcut adım 1.2, ilerleme çubuğu ilerler; üstteki özet de yenilenir |
| 3.2 | 1.2 (karar): önce **"Hayır → talep kapanır (Yönlendirildi)"** butonuna bas, sonra **Vazgeç** | Onay penceresi: talebi kapatan cevaplar geri alınamadığı için önce sorulur. Vazgeçince proje 1.2'de kalır |
| 3.3 | 1.2: "Evet → sonraki adım"; 1.3 tamamla | Ekran 2. aşamaya kendiliğinden geçer |
| 3.4 | 2.1 tamamla, 2.2 "Evet" | Mevcut adım **2.4** (Talep Anlama & Seviye) |
| 3.5 | **Seviye & Kabul** sekmesi → "Seviye belirleme · Akış 2.4": efor *21–80 adam-gün*, *6–20 ekran veya 2–3 modül*, entegrasyon *1–2 servis*, *2–4 rol*, *Kişisel veri var*, *Standart cihaz (yazıcı, barkod)* | Her tıklama hemen kaydedilir ("0/6 soru" sayacı ilerler). Sonunda **Puan: 12 / 18** |
| 3.6 | **"Seviyeyi projeye uygula"** | "Projenin seviyesi: **Seviye 2**", altında "Önerilen seviye projeye uygulanmış." Genel Bilgiler'de de Seviye 2 |
| 3.7 | Süreç sekmesi → 2.4 tamamla | Mevcut adım **3.1** (Proje Kabul) |
| 3.8 | Seviye & Kabul → "Proje kabul puanlaması · Akış 3.1": stratejik uyum **4**, ticari değer **4**, teknik uygulanabilirlik **4**, kaynak ve takvim **3**, müşteri uygunluğu **4**, risk düzeyi **3** | Başlıkta **3,75 · Kabul önerilir**. Altında: öneri yalnızca bilgi içindir, kararı 3.2'de yönetici verir |
| 3.9 | Süreç → 3.1 tamamla | Mevcut adım **3.2**; butonlar yerine: *"Bu adım Yazılım Departman Yöneticisi tarafından onaylanır."* |

*Anlatım: "Seviye ve kabul puanı kurumun formundaki sorulardan hesaplanıyor; sistem öneri veriyor, kararı kişi veriyor.
Kimin hangi adımı yapabileceğine de sistem karar veriyor; analist onay adımını geçemez."*

*Not:* Bir cevap kaydedilirken butonlar bir an kilitlenir; çok hızlı arka arkaya tıklanırsa ikinci tıklama alınmaz.
Cevapları sakin sırayla seç.

### 4. Bildirim ve yöneticinin onayı (3 dk)

Gizli pencere → **Mert Kaya** (sayfayı yenile).

| # | Yapılacak | Görülmesi gereken |
| --- | --- | --- |
| 4.1 | Üst bardaki zil | Zilde **1**. Tıklayınca: *"ISDLC-2026-0xx · 3.2 onayınızı bekliyor."* Elif 3.1'i tamamladığı anda gönderildi (zil dakikada bir yenilenir; beklemeden görmek için sayfayı yenile) |
| 4.2 | Gösterge Paneli | **Bana bekleyen adımlar**'da *Saha Ekibi Stok Takibi — Adım 3.2 · Onay*; **Kabul kararı bekleyen talepler**'de aynı talep: *Adım 3.2 · Yönetici kararı*, **3,75 · Kabul önerilir**. Mert'te "+ Yeni talep" butonu yok (yalnızca İş Analisti) |
| 4.3 | Zildeki bildirime tıkla | Proje açılır, zildeki sayı kaybolur (bildirim okundu). **Seviye & Kabul** sekmesinde Elif'in puanlaması: Seviye 2, 3,75 |
| 4.4 | Süreç sekmesi → 3.2: "Evet → sonraki adım" (isteğe bağlı not: *"Bütçe onaylandı, öncelik yüksek."*). Diğer seçenekler: "Hayır → talep kapanır (Reddedildi)" ve "Talebi reddet…" | 3. aşama tamamlandı, mevcut adım **4.1**. Kayıt artık **Projeler** listesinde |

*Anlatım: "Sırası gelen kişiye sistem haber veriyor; yönetici bildirimden talebe gidip analistin puanlamasına bakarak
onaylıyor. Reddetmek isterse gerekçe zorunludur. Kişi kendi yaptığı işlem için bildirim almaz."*

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

### 7. Gereksinimler, dokümanlar ve riskler (3 dk)

| # | Sekme | Yapılacak | Görülmesi gereken |
| --- | --- | --- | --- |
| 7.1 | Gereksinimler | "+ Gereksinim ekle": tür Fonksiyonel, öncelik Olmazsa olmaz, başlık *Saha personeli stok sayımını mobil cihazdan girebilmeli*. Kaydet | `GR-001`, durum Taslak |
| 7.2 | Gereksinimler | Satırdaki durum seçiminden "İncelemede" | Durum hemen kaydedilir |
| 7.3 | Dokümanlar | 4. aşamada *PKD — Proje Kapsam Dokümanı*: durumu "Hazırlanıyor" yap; "Not / bağlantı" ile bağlantı ekle (ör. *https://intranet.example/isdlc/pkd-demo.docx*) | Durum ve "Dokümanı aç ↗" bağlantısı görünür |
| 7.4 | Riskler | "+ Risk ekle": risk tanımı *ERP stok servislerinin dokümantasyonu eksik*, kategori Teknik, sorumlu **Can Öztürk**, olasılık **4**, etki **4**, önleyici aksiyon *Servis sahibiyle teknik çalıştay; POC ile doğrulama*, "Talep Sahibi ile paylaşılır" işaretli | Formda skor kendiliğinden: **16 · Yüksek** |
| 7.5 | Riskler | **Kaydet** | Listede `RSK-001`, *4 × 4*, **16 · Yüksek**, "Can Öztürk · Paylaşılır". Isı haritasında 4 × 4 hücresinde **1**; altta "15–25 Yüksek · 1". Hücreye tıklayınca liste o hücreye süzülür |

*Anlatım: "Risk Analiz Formu (akış 4.8) artık projenin içinde: skor olasılık × etkiden hesaplanır, saklanmaz. Risk
yüksek seviyeye çıkınca proje yöneticisi Ayşe Demir'e, sorumlu olarak atanan Can Öztürk'e bildirim gider."*

### 8. Kayıt geçmişi (1 dk)

**Kayıt Geçmişi** sekmesi.

| # | Yapılacak | Görülmesi gereken |
| --- | --- | --- |
| 8.1 | Göster | Bu demoda yapılan her şey, en yeni önce: risk (`RSK-001`), doküman bağlantısı ("Bağlantı — → https://…"), gereksinim ("Durum Taslak → İncelemede"), tutanak ve açık konular, Seviye & Kabul cevapları (her cevap ayrı satır). Yeni kayıtlarda "5 alan" gibi katlanmış ayrıntı |
| 8.2 | Kayıt türü: *Proje* | Yalnızca projenin kendi değişiklikleri: **Mert Kaya — "Mevcut adım 3.2 → 4.1"**, Elif'in seviye uygulaması (Seviye — → 2) |

*Anlatım: "Kayıtlar silinmez, her değişiklik kimin yaptığıyla birlikte saklanır; değiştirilemez."*

### Kapanış (1 dk)

Gösterge Paneli: **Son hareketler**'de demoda yapılanlar, **Aşamalara göre aktif kayıtlar**'da projenin 4. aşamada
görünmesi ve seviye dağılımında bir Seviye 2 daha, **Yüksek riskler** kartında ve listesinde `RSK-001`. Sonraki adımlar:
adım bazında doküman listesi, risk ajanı, yapay zekâ ile şirket formlarının (DOCX) doldurulması ve belge yükleme
(bkz. [ai-vision.md](ai-vision.md)).

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

## Bilerek gösterilmeyenler

| Konu | Neden |
| --- | --- |
| 3.2 onaylanınca Elif'e bildirim | Süreç bu olayda bildirim göndermez; yalnızca onay bekleyen adım, geri dönüş ve kapanış bildirilir ([process-api.md](process-api.md) → Bildirim noktaları) |
| Ayşe ve Can'ın bildirimleri | Demoda pencere yok. Soru gelirse üçüncü bir pencerede Ayşe Demir ile giriş yapıp zili göster: *"… · RSK-001 yüksek risk (skor 16)"* |
| Menüdeki Seviye & Kabul, Risk Yönetimi, Bildirimler sayfaları | Proje sekmelerindekiyle aynı bileşenler; zaman varsa gösterilir |

## Bir şey ters giderse

| Durum | Ne yapılır |
| --- | --- |
| Yapay zekâ bölümü görünmüyor | Servis kapalı ya da az önce açıldı (durum en fazla 30 sn önbellekte). Sayfayı yenile; olmazsa kararları ve aksiyonları elle gir — uygulamanın geri kalanı etkilenmez |
| Yapay zekâ "servis yoğun" diyor | Bir kez daha dene; olmazsa elle gir |
| "Proje bu arada başka bir adıma geçti" | İki pencerede aynı projede işlem yapıldı; ekran kendini yeniler, devam et |
| Giriş yapılamıyor | Test parolası tanımlı mı (README); veritabanı sıfırlandıysa API'yi parola tanımlıyken yeniden başlat |
| Seviye / kabul cevabı işaretlenmedi | Önceki tıklama kaydedilirken butonlar kilitliydi; bir saniye bekleyip tekrar tıkla |
| Zilde bildirim görünmüyor | Zil dakikada bir yenilenir; sayfayı yenile. Bildirimi işlemi yapan kişi değil, sırası gelen kişi alır |
