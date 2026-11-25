# Uçtan Uca Demo Senaryosu

Platformun baştan sona gösterimi: bir talebin e-postadan açılıp süreçte ilerlemesi, projede çalışılması ve aynı uygulamayı
kullanan ikinci bir şirketin açılması. Süre: yaklaşık **30 dakika**. Sorumlu: Erva.
Durum: **Faz 4, 5 ve 6 ile baştan yazıldı** (Faz 5 · F10 ve Faz 6 · T8). Denendiği tarih ve kapsam en altta.

Anlatılacak hikâye: *"Bir talep sisteme girdiği andan itibaren şirketin kendi akışında kimin ne yapacağı belli: sistem
sırayı, yetkiyi ve onayları takip ediyor, sırası gelene haber veriyor. Seviye, uygunluk, riskler, toplantılar ve açık
konular projenin içinde. Yapay zekâ öneri hazırlıyor, kaydı kişi yapıyor. Aynı uygulamayı başka bir şirket de kendi
kullanıcıları ve akışlarıyla kullanabiliyor; iki şirket birbirinin hiçbir kaydını görmüyor."*

## Hazırlık (demodan önce)

| # | Ne | Nasıl |
| --- | --- | --- |
| 1 | **Temiz veritabanı** | `docker compose down -v` ve `docker compose up -d`, sonra API'yi başlat. Akışlar, 7 Ithinka kullanıcısı, Platform Yöneticisi ve örnek projeler baştan yüklenir. Whisper modelini yeniden indirmemek için yalnızca veritabanı volume'ünü silmek yeterli: `docker compose rm -sf mssql`, `docker volume rm isdlc_isdlc-mssql-data`, `docker compose up -d mssql` |
| 2 | Gizli ayarlar | `Jwt:SigningKey` ve `Identity:TestPassword` tanımlı olmalı (README → Backend) |
| 3 | Yapay zekâ servisi | `ai/.env` içinde `GEMINI_API_KEY`, sonra `docker compose --profile ai up -d --build`. Yeni talep formunda "E-postadan doldur (yapay zekâ)" görünüyor mu kontrol et |
| 4 | Uygulama | API (`dotnet run --project backend/src/Isdlc.Api`) ve frontend (`frontend` klasöründe `npm start`): http://localhost:4200 |
| 5 | **Üç tarayıcı penceresi** | 1: normal pencere, **Elif Yılmaz** (İş Analisti). 2: gizli pencere, **Mert Kaya** (Yazılım Departman Yöneticisi). 3: başka bir tarayıcı (ya da ikinci profil), **Platform Yöneticisi** (`platform@isdlc.local`). Aynı tarayıcının iki gizli penceresi oturumu paylaşır; üçüncü kişi için ayrı tarayıcı gerekir |
| 6 | Metinler | Aşağıdaki "Sahte e-posta" ve "Sahte toplantı notu" panoya hazır olsun. Ses kaydı denenecekse sahte bir kayıt (kendi sesinle okunmuş not) hazırla |
| 7 | Prova | Senaryoyu bir kez baştan sona dene; sonra 1. adımı tekrarla |

**Yalnızca sahte veri kullanılır.** E-posta ve toplantı notları yapay zekâ servisine gönderilir; gerçek müşteri bilgisi girme.
Parolalar belgeye yazılmaz: Ithinka kullanıcılarının parolası ekipçe belirlenen test parolasıdır, yeni şirketin yöneticisinin
geçici parolası demo sırasında ekranda üretilir.

### Demodaki kişiler

| Kişi | Şirket · rol | Ne yapar |
| --- | --- | --- |
| Elif Yılmaz (`elif.yilmaz@isdlc.local`) | Ithinka · İş Analisti | Talebi e-postadan açar, süreci yürütür: uygunluk, seviye, tutanak, açık konu, risk |
| Mert Kaya (`mert.kaya@isdlc.local`) | Ithinka · Yazılım Departman Yöneticisi | Teklifi onaylar (10. adım); zildeki "onayınızı bekliyor" bildirimiyle gelir |
| Ayşe Demir | Ithinka · Proje Yöneticisi | Talebin proje yöneticisi; 9. adımı (zaman ve kaynak planı) yapar |
| Can Öztürk | Ithinka · Yazılım Geliştirici | Toplantı notunda aksiyon sahibi; profili ve iş yükü gösterilir |
| Platform Yöneticisi (`platform@isdlc.local`) | Şirketi yok | Yeni şirketi açar; şirketlerin kayıtlarını göremez |
| Deniz Aydın (`deniz.aydin@ornek-lojistik.example`) | Örnek Lojistik A.Ş. · İş Analisti, Şirket Yöneticisi | Demo sırasında açılır; kullanıcı ekler, yapay zekâ ayarlarını yapar |

## Akış

Her bölümde: **Ekran** → **Yapılacak** → **Görülmesi gereken** → *Anlatım notu*.

### 1. Giriş ve gösterge paneli (2 dk)

Pencere 1 → Elif.

| # | Ekran | Yapılacak | Görülmesi gereken |
| --- | --- | --- | --- |
| 1.1 | Giriş | Elif olarak giriş yap | Üst barda "ISDLC · Ithinka" ve "Elif Yılmaz · İş Analisti" |
| 1.2 | Gösterge Paneli | Göster | "Merhaba, Elif"; özet kartları, **Bana bekleyen adımlar** (her satır projenin Süreç sekmesine gider), **Ekip iş yükü** (kişi başına açık konu ve bekleyen adım), aşamalara göre aktif kayıtlar ve seviye dağılımı, son hareketler |
| 1.3 | Menü → Süreç Kütüphanesi | Göster | Beş akış (Yeni Yazılım Projesi 19 adım, Ek Modül 17, Süreç İyileştirme 8, Kaynak Planlama 8, Faturalama 6). Yeni Yazılım Projesi: 4 aşama, 4 karar noktası, 1 onay adımı; 4. adımda "Yalnızca dış talepte · Teklif hazırlanacaksa" ve "Hayır ise: Talep kapanır — Reddedildi" |

*Anlatım: "Şirketin akışları sisteme veri olarak girildi: her adımın sorumlusu, onaycısı, 'Hayır' cevabında ne olacağı ve
hangi koşulda atlanacağı tanımlı. Ekranlar bu tanıma göre çalışıyor."*

### 2. E-postadan talep (3 dk)

| # | Ekran | Yapılacak | Görülmesi gereken |
| --- | --- | --- | --- |
| 2.1 | Menü → Talepler → "+ Yeni talep" | **E-postadan doldur (yapay zekâ)** bölümünü aç, sahte e-postayı yapıştır, **Yapay zekâ ile doldur** | Birkaç saniye sonra form dolar: talep türü yeni yazılım projesi, talep sahibi *Demo Lojistik A.Ş.*, talebi ileten *Hakan Yıldız*, iletişim, açıklama, kanal E-posta, kaynak Dış. Model her seferinde birebir aynı cevabı vermeyebilir |
| 2.2 | Yeni talep | Proje adını kontrol et (*Saha Ekibi Stok Takibi*), **Talebi kaydet** | Proje sayfası açılır: **No 20**, durum Aktif, mevcut adım **1 · Talep ve Ön Analiz**. Üstte proje özeti kartları (açık konular, riskler, gereksinimler, dokümanlar, ekip iş yükü) |
| 2.3 | Proje → **Düzenle** | Proje yöneticisi: Ayşe Demir, **Değişiklikleri kaydet** | Başlıkta "Proje yöneticisi: Ayşe Demir". Ayşe'ye "… projesine proje yöneticisi olarak atandınız." bildirimi gider |

*Anlatım: "Yapay zekâ kayıt açmaz, formu doldurur; kontrol edip kaydeden kişidir. E-posta adresi ve telefon modele gitmeden
gizlenir."*

*Yedek:* Yapay zekâ "servis yoğun" derse formu elle doldur (alanlar 2.1'deki gibi). Uygulamanın geri kalanı etkilenmez.

### 3. Süreç, uygunluk ve seviye (6 dk)

Proje → **Süreç** sekmesi.

| # | Yapılacak | Görülmesi gereken |
| --- | --- | --- |
| 3.1 | 1, 2 ve 3. adımlarda "Adımı tamamla" | Her adım "Tamamlandı", mevcut adım 4. Süreç geçmişi alta yazılır |
| 3.2 | 4. adımı oku | Karar adımı: "Evet → sonraki adım" ve "Hayır → talep kapanır (Reddedildi)". Bu adım yalnızca dış taleplerde görünür; iç talepte atlanır |
| 3.3 | **Seviye & Uygunluk** sekmesi → "Müşteri uygunluğu · Akış 4": 10 soruya 0–10 puan ver (8, 7, 8, 6, 5, 7, 8, 8, 6, 7) | Başlıkta **70 / 100 · Şartlı uygun**; altında "Sonuç bir öneridir; kararı iş analisti 4. adımda verir" |
| 3.4 | Süreç → 4. adım: "Evet → sonraki adım" | Mevcut adım 5 |
| 3.5 | Seviye & Uygunluk → "Seviye belirleme · Akış 5": 26 soruda her birinin ortadaki seçeneğini seç | **Doküman seviyesi: Seviye 2**, **Araştırma süresi: 5 gün** (13/13 ve 13/13 soru) |
| 3.6 | **"Seviyeyi projeye uygula"** | "Projede: Seviye 2 · 5 gün — Önerilen sonuç projeye uygulanmış." Genel Bilgiler'de de Seviye 2 |
| 3.7 | Süreç → 5. adımı tamamla | Mevcut adım **6 · Talep anlaşılır** |

*Anlatım: "Uygunluk ve seviye şirketin kendi formlarındaki sorulardan, eşikleri de form tanımından hesaplanıyor; sistem
öneri veriyor, kararı kişi veriyor."*

*Not:* Bir cevap kaydedilirken butonlar bir an kilitlenir; çok hızlı tıklanırsa ikinci tıklama alınmaz. Sakin sırayla seç.

### 4. Toplantı, yapay zekâ ve açık konular (5 dk)

Proje → **Toplantı Tutanakları** → "+ Tutanak oluştur".

| # | Yapılacak | Görülmesi gereken |
| --- | --- | --- |
| 4.1 | Tür: Analiz Oturumu, tarih: bugün, tutanağı yazan: Elif Yılmaz | — |
| 4.2 | *(İsteğe bağlı)* **Ses kaydından metne çevir** → sahte kayıt | Kayıt metne çevrilip "Ham toplantı notları"na eklenir: "… dakikalık kayıt metne çevrilip notlara eklendi". Kayıt bilgisayarda işlenir, dışarı gönderilmez |
| 4.3 | Ham notlara sahte toplantı notunu yapıştır → **Yapay zekâ ile karar ve aksiyonları çıkar** | Karar (tablet desteğinin ilk fazda olmaması) karar kutusuna, aksiyonlar satır olarak dolar. **Kişilere göre aksiyonlar**: Can Öztürk listeden seçili gelir; *Demo Lojistik BT* "(sistem dışı)" olarak yazılır. "cuma", "gelecek hafta salı" gerçek tarihe çevrilir. Formda yeri olmayan soru ve varsayım ayrıca listelenir |
| 4.4 | Katılımcıları ve gündemi kontrol et, **Tutanağı kaydet** | `TT-001`; aksiyonlar açık, karar kapalı kayıt olarak Açık Konular'a geçer |
| 4.5 | **Açık Konular** sekmesi → "+ Kayıt ekle": tür Soru, konu *Saha ekibi çevrimdışı çalışabilmeli mi?*, sorumlu: Ekip üyesi açılır listesini göster | Her kişinin yanında iş yükü: "Can Öztürk · 1 açık konu". Sorumluyu seçip kaydet |
| 4.6 | Açık konuda **Bağlantıyı kopyala** | Kaydın bağlantısı panoya kopyalanır; açan kişi doğrudan bu kayda gelir |

*Anlatım: "Yapay zekâ toplantıdan kimin neyi ne zamana yapacağını çıkarıyor, kişiyi ekipten eşleştiriyor; kaydeden yine kişi.
Sorumlu seçerken herkesin elindeki iş görünüyor."*

### 5. Risk, onay ve bildirim (5 dk)

| # | Ekran | Yapılacak | Görülmesi gereken |
| --- | --- | --- | --- |
| 5.1 | Süreç | 6 ve 7. adımları tamamla | Mevcut adım 8 · Riskler belirlenir |
| 5.2 | Riskler | "+ Risk ekle": *ERP stok servislerinin dokümantasyonu eksik*, Teknik, sorumlu Can Öztürk, olasılık 4, etki 4, önleyici aksiyon *Servis sahibiyle teknik çalıştay; POC ile doğrulama*. Kaydet | Listede `RSK-001`, *4 × 4*, **16 · Yüksek**; ısı haritasında 4 × 4 hücresinde 1 |
| 5.3 | Süreç | 8. adımı tamamla | Mevcut adım **9 · Zaman ve kaynak planlanır**; butonlar yerine "Bu adım Proje Yöneticisi tarafından yapılır." Analist geçemez |
| 5.4 | Pencere 2 → Ayşe Demir | Zil: *"20 · RSK-001 yüksek risk (skor 16)"* ve atanma bildirimi. Proje → Süreç → 9. adımı tamamla | Mevcut adım **10 · Teklif ve Anlaşma**: "Bu adım Yazılım Departman Yöneticisi tarafından onaylanır." |
| 5.5 | Pencere 2 → çıkış, Mert Kaya | Zil: *"20 · 10 onayınızı bekliyor."* Bildirime tıkla. Süreç → 10. adım: seçenekler "Evet → sonraki adım", "Hayır → 9 adımına dön", "Talebi reddet…". **Evet** | Mevcut adım **11**. Bildirim okundu |

*Anlatım: "Sırası gelen kişiye sistem haber veriyor; kimse kendi işlemi için bildirim almaz. Onay adımını yalnızca onaycı
geçebilir."*

### 6. Projenin hafızası (3 dk)

Pencere 1 → Elif.

| # | Ekran | Yapılacak | Görülmesi gereken |
| --- | --- | --- | --- |
| 6.1 | Proje → **Zaman Çizelgesi** | Göster | En yeni önce: süreç adımları (kimin ne zaman yaptığı), tutanak, kararlar, "Kayıt açıldı · 20" |
| 6.2 | **Kayıt Geçmişi** | Göster | Alan bazında her değişiklik: eski → yeni değer, kim, ne zaman |
| 6.3 | Üst bardaki arama (Ctrl+K) | *stok* yaz | Projeler grubunda *20 · Saha Ekibi Stok Takibi*; tutanak, açık konu ve risk de aranabilir |
| 6.4 | Bir açık konunun sorumlusuna tıkla (Can Öztürk) | Profil | Uzmanlık alanları, "benimle nasıl çalışılır" notu, iş yükü, açık konuları ve bekleyen adımları |
| 6.5 | Menü → Projeler → **14** → **Ek Geliştirmeler** | Göster | *14 / EG-01 · Servis Formuna Müşteri İmzası*: devam eden projeye ek geliştirme kendi akışında, ana projenin numarasıyla |

*Anlatım: "Projede olan her şey tek yerde ve kayıtlar silinmiyor."*

### 7. İkinci şirket (5 dk)

Pencere 3 → Platform Yöneticisi.

| # | Ekran | Yapılacak | Görülmesi gereken |
| --- | --- | --- | --- |
| 7.1 | Giriş | `platform@isdlc.local` ile gir | Doğrudan **Şirketler** ekranı; menüde yalnızca bu var. Listede Ithinka (7 kullanıcı) |
| 7.2 | "+ Yeni şirket" | Ad *Örnek Lojistik A.Ş.*, kod *ornek-lojistik*; ilk yönetici *Deniz Aydın*, *deniz.aydin@ornek-lojistik.example*, İş Analisti. Geçici parola form tarafından üretilir. **Şirketi aç** | "Örnek Lojistik A.Ş. açıldı. İlk yönetici …, geçici parola …. Kişiye iletin; ilk girişte değiştirecek." Listede 1 kullanıcıyla yeni şirket |
| 7.3 | Çıkış → Deniz ile geçici parolayla giriş | — | **Parolanızı değiştirin** ekranı; başka bir sayfaya gidilemez. Yeni parola (en az 12 karakter) → panel |
| 7.4 | Gösterge Paneli | Göster | "Merhaba, Deniz"; her sayı 0; ekip iş yükünde yalnızca Deniz. Üst barda "Örnek Lojistik A.Ş." Menüde **Yönetim → Kullanıcılar, Şirket Ayarları** |
| 7.5 | Süreç Kütüphanesi | Göster | Aynı beş akış, şirketin kendi kopyası olarak |
| 7.6 | Arama | *stok* yaz | **Sonuç bulunamadı.** Ithinka'nın projesi görünmez |
| 7.7 | Adres çubuğuna Ithinka projesinin adresini yapıştır (pencere 1'den kopyala) | — | **Proje bulunamadı.** |
| 7.8 | Yönetim → Kullanıcılar → "+ Yeni kullanıcı" | *Ece Kurt*, Yazılım Departman Yöneticisi. Kaydet | Listede Ece Kurt, durum "Geçici parola"; "Geçici parola ver" düğmesi |
| 7.9 | Talepler → "+ Yeni talep" | **E-postadan doldur** bölümü yok. *Filo Bakım Takibi*, iç talep, kaydet | **No 1**: numaralar her şirkette 1'den başlar |
| 7.10 | Yönetim → Şirket Ayarları | İki anahtar da kapalı. Yalnızca **Yapay zekâ yardımcıları açık**'ı işaretle, kaydet | "Ayarlar kaydedildi." Tutanak formunda **Ses kaydından metne çevir** görünür; karar ve aksiyon önerisi ile e-posta yardımcısı kapalı kalır, çünkü metin dışarı gönderilmiyor |

*Anlatım: "Aynı uygulama, ayrı şirketler. Her şirket kendi kullanıcısını kendisi yönetiyor, akışları şablondan geliyor ve
değiştirebiliyor. Yapay zekâya ne gönderileceğine şirket karar veriyor: kayıt dışarı çıkmadan metne çevrilebilir, metnin
dış modele gitmesi ayrı bir izin."*

### Kapanış (1 dk)

Pencere 1 → Elif → Gösterge Paneli: **Son hareketler**'de demoda yapılanlar, **Yüksek riskler**'de `RSK-001`, **Ekip iş
yükü**'nde Can'ın açık konuları. Örnek Lojistik'in talebi burada görünmez. Sonraki adımlar: şirketin kendi akışını ekrandan
düzenlemesi, yapay zekâ ile şirket formlarının (DOCX) doldurulması (bkz. [ai-vision.md](ai-vision.md)).

## Sahte e-posta

```
Kimden: Hakan Yıldız <hakan.yildiz@demolojistik.example>
Tarih: 24 Kasım 2026
Konu: Saha ekibi için stok sayım uygulaması

Merhaba,

Saha ekibimiz depolardaki stokları şu an kâğıda yazıp ofise taşıyor. Sayımın telefondan yapılabileceği, farkların
merkeze anında düşeceği bir uygulama istiyoruz. Yaklaşık 40 saha personeli kullanacak; ERP'deki stok servisleriyle
konuşması gerekiyor.

Bu konuyu bir toplantıda konuşabilir miyiz?

Hakan Yıldız
Operasyon Müdürü, Demo Lojistik A.Ş.
0212 555 00 00
```

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
| Süreç iyileştirme (`SI-001`) | Menü → Süreç İyileştirme → "+ Yeni öneri". Zaman varsa gösterilir; akışı ayrı bir hikâye |
| Talep Analizi sekmesi | Soru listesi uzun; soru gelirse açılıp gösterilir |
| Şirketi pasife alma | Platform Yöneticisi → Şirketler → Düzenle. Gösterilirse o şirketin açık oturumları en geç bir dakika içinde kapanır; demodaki Örnek Lojistik'le yap, Ithinka'yla değil |
| 10. adım onaylanınca Elif'e bildirim | Süreç bu olayda bildirim göndermez; yalnızca onay bekleyen adım, geri dönüş ve kapanış bildirilir ([process-api.md](process-api.md) → Bildirim noktaları) |

## Bir şey ters giderse

| Durum | Ne yapılır |
| --- | --- |
| Yapay zekâ bölümü görünmüyor | Servis kapalı ya da az önce açıldı. Sayfayı yenile; olmazsa elle gir |
| Yapay zekâ "servis yoğun" diyor | Model sağlayıcısı geçici olarak cevap vermiyor. Bir kez daha dene; olmazsa elle gir |
| Giriş yapılamıyor | Test parolası tanımlı mı (README); veritabanı sıfırlandıysa API'yi parola tanımlıyken yeniden başlat |
| İkinci şirkette giriş "E-posta veya parola hatalı" diyor | Geçici parola yanlış kopyalandı. 7.2'deki mesaj parolayı bir kez gösterir; mesajı kapatmadan kopyala |
| Seviye / uygunluk cevabı işaretlenmedi | Önceki tıklama kaydedilirken butonlar kilitliydi; bir saniye bekleyip tekrar tıkla |
| Zilde bildirim görünmüyor | Zil dakikada bir yenilenir; sayfayı yenile |
| "Proje bu arada başka bir adıma geçti" | İki pencerede aynı projede işlem yapıldı; ekran kendini yeniler, devam et |

## Denendi

25 Kasım 2026, temiz veritabanında, uygulamada:

- **Denendi:** 1, 2.2, 2.3, 3, 4.5, 5, 6.1, 6.3, 6.4, 6.5, 7. Tablolardaki ekran metinleri ve bildirimler bu denemeden alındı.
- **Bu tarihte denenemedi:** 2.1, 4.2, 4.3, 4.4 ve 4.6. Model sağlayıcısı (Gemini) o gün sürekli 503 döndü ("model şu an
  yoğun"); bu yardımcılar kendi PR'larında denenmişti (F4, F5, F6). Ekranları ve düğmeleri görünüyor, yalnızca model cevabı
  alınamadı. Provada denenmeli.
- Denerken bulunan hata: yalnızca "yapay zekâ açık" olan şirkette ses kaydı düğmesi görünmüyordu (7.10). Ayrı PR ile
  düzeltildi; düzeltme birleşmeden 7.10'un son cümlesi görülmez.
