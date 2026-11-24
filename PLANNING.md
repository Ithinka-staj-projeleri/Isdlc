# PLANNING.md — Proje Planı ve İş Dağılımı

Bu dosya MVP'nin fazlarını, kimin neyi yapacağını ve birlikte çalışma kurallarını anlatır.
Proje tanımı, teknoloji yığını, klasör yapısı ve isimlendirme kuralları için: [AGENTS.md](AGENTS.md).

## 1. Ekip

| Kişi | Ana sorumluluk |
| --- | --- |
| Erva | Süreç analizi ve `Process` hattı (projenin en ağır kısmı) |
| Dide | Backend iskeleti ve `Projects` hattı |
| Baran | Frontend iskeleti ve `Identity` hattı |

İş, katmanlara göre değil **modüllere göre** bölünür (dikey dilim): her kişi kendi modülünün
entity, API ve ekranını uçtan uca yazar.

## 2. Fazlar

| Faz | Erva | Dide | Baran |
| --- | --- | --- | --- |
| **0 — Ortak temel** | 13 aşamalı süreç analizi, veri modeli, seed verisi | Backend iskeleti, MSSQL (Docker), denetim kaydı altyapısı, temel entity'ler | Angular iskeleti, sayfa düzeni, ortak bileşenler |
| **1 — Çekirdek** | `Process` | `Projects` | `Identity` |
| **2 — Proje kayıtları** | `OpenIssues` + `Meetings` | `Requirements` + `Documents` | `Assessment` + `Risks` |
| **3 — Bitiriş** | `Audit` ekranı + uçtan uca demo senaryosu | `Dashboard` | `Notifications` |
| **4 — Şirket akışları** (kapatıldı) | Toplantı ve ziyaret tutanakları, süreç iyileştirme | Akış altyapısı, akış seed'leri, talep analizi; talepler, ret / erteleme, ek geliştirme | Soru bankası, seviye ve uygunluk |
| **5 — Takım çalışması** (kapatıldı) | Zaman çizelgesi, tutanak aksiyonları, ses kaydından tutanak, e-postadan talep, demo | Proje özeti, panel eklemeleri | Genel arama, bağlantıyı kopyala, kişi profili |
| **6 — Çok şirketli yapı** (öneri) | Sözleşme, şirket başına seed, yapay zekâ ayarları, belgeler ve demo | Ayrım altyapısı (`TenantId`, filtre, migration) | Platform ve şirket yönetimi, yönetim ekranları |
| **7 — Ayarlanabilirlik** (yol haritası) | Faz 6 biterken planlanır | | |
| **8+ — Entegrasyonlar** (yol haritası) | Faz 7 biterken planlanır | | |

Faz 6'dan sonraki yön ve gerekçesi: Bölüm 6 (Yol haritası).

### Faz 0 — Ortak temel

Amaç: Faz 1'in ihtiyaç duyduğu her şeyin main'de hazır olması, böylece Faz 1'de kimsenin kimseyi beklememesi.

**Sıra**
1. Erva önce 5 temel entity'nin alanlarını netleştirir: `User` (ve roller), `Project`, `ProcessStage`,
   `ProcessStep`, `StepRecord`. Sürecin diğer ayrıntıları faz boyunca tamamlanır.
2. Dide bu sırada iskeletin entity'den bağımsız kısmını kurar: çözüm ve 4 katman, `docker-compose.yml`,
   hata middleware'i. Entity'ler netleşince bunları, ilk migration'ı ve seed'i ekler.
3. Baran Angular iskeletini, sayfa düzenini (menü, üst bar), routing'i ve ortak bileşenleri kurar.

**Ortak API kuralları** (Dide backend tarafını, Baran frontend tarafını iskelete koyar; kurallar AGENTS.md'ye eklenir)
- Hatalar ASP.NET Core'un yerleşik `ProblemDetails` biçiminde döner.
- Enum'lar JSON'da metin olarak gönderilir (`"Active"`).
- Tarihler UTC tutulur, ISO 8601 biçiminde gönderilir.
- Liste uç noktaları ortak sayfalama yapısı döndürür: `items`, `totalCount`.
- DTO isimleri AGENTS.md'deki kurala uyar (`CreateXDto`, `XListItemDto`).

**Geçici kullanıcı:** `Identity` bitene kadar API, seed'deki sabit bir test kullanıcısıyla çalışır.
Bu tek bir noktada tanımlanır; giriş sistemi gelince yalnızca orası değişir.

**Bitiş koşulu**
- `docker compose up` ile veritabanı açılıyor; API ve Angular uygulaması çalışıyor.
- Temel entity'ler, ilk migration ve seed (13 aşama, adımlar, roller, test kullanıcıları) main'de.
- Ortak API kuralları iskelette uygulanmış ve AGENTS.md'ye yazılmış.
- Veri modeli üç kişinin onayından geçmiş.

### Faz 1 — Çekirdek modüller

- **Erva — `Process`:** 13 aşamalı akış, Evet/Hayır kararları, onaylar, geri dönüşler.
- **Dide — `Projects`:** talep kaydı, proje listesi ve detayı.
- **Baran — `Identity`:** giriş, JWT, roller, guard; ardından geçici kullanıcının kaldırılması.

**Bitiş koşulu:** Giriş yapan bir kullanıcı talep açabiliyor ve talep süreç aşamalarında ilerleyebiliyor.

### Faz 2 — Proje içi kayıtlar

- **Erva:** `OpenIssues` + `Meetings` (tutanaktaki aksiyonlar açık konulara aktarıldığı için aynı kişide).
- **Dide:** `Requirements` + `Documents`.
- **Baran:** `Assessment` + `Risks` (ikisi de puanlama mantığı içeriyor).

**Bitiş koşulu:** Bir projenin detayından bütün kayıt türleri eklenip görüntülenebiliyor.

### Faz 3 — Kesişen modüller ve bitiriş

- **Erva:** `Audit` ekranı ve uçtan uca demo senaryosu.
- **Dide:** `Dashboard`.
- **Baran:** `Notifications`.

**Bitiş koşulu:** MVP demosu baştan sona sorunsuz çalışıyor.

### Faz 4 — Şirket akışları ve formlarına uyum

Uygulama, iş analistlerinin paylaştığı akışların son hâline (yeni yazılım projesi, ek geliştirme, süreç iyileştirme,
haftalık kaynak planlama, aylık faturalama) ve formlarına göre güncellenir. İşler, sıra ve açık konular:
[docs/phase-4-plan.md](docs/phase-4-plan.md).

**Kapatıldı (19 Kasım 2026).** Yeni yazılım projesi, ek geliştirme ve süreç iyileştirme akışları uygulamada; yapılmamış işler
(dokümanlar, gereksinim alanları, kapsam ve efor, risk ölçeği, ekip, kaynak planlama, faturalama, demo) iptal edildi.
Ayrıntı: [docs/phase-4-plan.md](docs/phase-4-plan.md) → Kapanış.

### Faz 5 — Takım çalışması ve yapay zekâ yardımcıları

17 Kasım 2026'daki Volobox tanıtımında görülen özelliklerden platforma uyanlar eklenir: proje özeti, panel eklemeleri,
proje zaman çizelgesi, tutanaktan kişi kişi aksiyon, ses kaydından tutanak ve e-postadan talep taslağı, genel arama,
kişi profili. İşler, sıra ve açık konular: [docs/phase-5-plan.md](docs/phase-5-plan.md).

**Kapatıldı (24 Kasım 2026).** F1–F9 ve çapraz testleri birleşti. Demo (F10), Faz 6'nın T8'iyle iki şirketli olarak yazılacak.

### Faz 6 — Çok şirketli yapı (multi-tenant)

Tek kurulum birden fazla şirkete hizmet eder: her şirketin kendi kullanıcıları, akışları, soru listeleri ve doküman türleri
olur; hiçbir şirket başka şirketin kaydını göremez. Tek veritabanı, her tabloda `TenantId` ve global sorgu filtresi.
Roller, koşullar ve akışların ekrandan düzenlenmesi sonraki faza kalır. İşler, kararlar ve sıra: [docs/phase-6-plan.md](docs/phase-6-plan.md).

**Bitiş koşulu:** İki şirket aynı kurulumda çalışıyor; her modülde bir şirketin diğerinin kaydını göremediği ve
değiştiremediği testlerle gösteriliyor.

## 3. Birlikte çalışma kuralları

- **Modül içi sıra:** önce entity ve DTO, sonra API uç noktası (Swagger'dan denenir), en son ekran.
  Her biri küçük ve ayrı bir PR olur.
- **Modüller arası bağlantı:** Modüller birbirinin içine doğrudan girmez, önceden anlaşılmış
  arayüzler üzerinden konuşur. Örnek: `Process`, bildirim için yalnızca `INotificationService`'i çağırır;
  içini `Notifications` modülü doldurur.
- **Migration çakışması:** Migration, PR açmadan hemen önce, main dala alındıktan sonra üretilir.
  PR başına tek migration olur. Çakışma çıkarsa kendi migration'ınızı silip yeniden üretin.
- **Ortak dosyalar** (`Program.cs`, DbContext, `app.routes.ts`): küçük ve sık PR'lar açılır.
- **İş takibi:** GitHub Issue açılmaz. Her iş ayrı ve küçük bir PR'dır; PR açıklaması işi ve nedenini anlatır.
  Dal adları AGENTS.md'deki kurala uyar. Her PR'ı en az bir kişi inceler.
- **Çapraz test:** Herkes başka birinin modülünün testini yazar.
  - Erva → Baran'ın modülleri
  - Baran → Dide'nin modülleri
  - Dide → Erva'nın modülleri

## 4. Prototip

Referans prototip: "ISDLC Analiz Merkezi" (tek HTML dosyası). Birebir kopyalanmaz; kapsam AGENTS.md'ye göredir.

**Prototip ile MVP eşleşmesi**

| Prototipte | MVP'de |
| --- | --- |
| Gösterge Paneli, Talepler, Projeler | `Dashboard`, `Projects` |
| Seviye & Kabul, Gereksinimler, Risk Yönetimi, Açık Konular, Toplantı Tutanakları | `Assessment`, `Requirements`, `Risks`, `OpenIssues`, `Meetings` |
| Proje detayı: Süreç, Dokümanlar, Kayıt sekmeleri | `Process`, `Documents`, `Audit` |
| Test & UAT, Kaynak Planlama, Proje Devir, Faturalama, proje detayındaki Teklif sekmesi | Sonraki sürüm |
| Yapay Zekâ Merkezi, Doküman Stüdyosu, Bilgi Bankası, Otomasyon | Sonraki sürüm |

**Önerilen, ekip onayı bekleyen kararlar**
- Uyum sekmesi (kontrol listesi maddeleri) → `Process` içinde.
- Süreç Kütüphanesi → `Process` içinde; Doküman Kütüphanesi → `Documents` içinde.
- İzlenebilirlik sekmesi (gereksinim ↔ test) → Test modülüne bağlı olduğu için sonraki sürüm.

**Prototipten seed verisine aktarılacaklar**
- 13 aşama ve her aşamadaki adımlar; Evet/Hayır karar adımları ve "Hayır" durumunda ne yapılacağı.
- 6 rol: İş Analisti, Yazılım Departman Yöneticisi, Proje Yöneticisi, Yazılım Geliştirici,
  Test Uzmanı, Satış Temsilcisi.
- Onay adımları: 3.2, 4.5, 5.1 → Yazılım Departman Yöneticisi. (Prototipte 2.3 de onay adımıydı; şirketin güncel
  ISDLC v12.0 akışında 2.3, yöneticinin "Talebi reddet" işlemidir.)
- Talebi kapatan sonuçlar: 1.2 → Yönlendirildi, 3.2 → Reddedildi, 5.3 → Kaybedildi.

**Veri modeli için not:** Akış her zaman ileri gitmez; bazı "Hayır" cevapları önceki bir adıma döner
(ör. 4.7 → 4.1, 9.2 → 8.1). `StepRecord` bir adımın birden fazla kez işlenmesine izin vermelidir.

## 5. Yapay zekâ

Ekip, komut satırındaki denemenin ardından toplantı ajanını MVP'ye almayı onayladı. Tasarım ve yol haritası:
[docs/ai-vision.md](docs/ai-vision.md), kod: [ai/](ai/README.md). Sorumlu: Erva.

- **Mimari:** Ajanlar ayrı bir Python servisidir (`ai/`, FastAPI). Yalnızca backend çağırır; servis veritabanına erişmez.
  Servis çalışmıyorsa yapay zekâ butonları görünmez, uygulamanın geri kalanı etkilenmez.
- **MVP kapsamı:** Yeni tutanak formunda "Yapay zekâ ile karar ve aksiyonları çıkar". Öneriler formu doldurur (kararlar,
  aksiyonlar; boşsa toplantı türü, katılımcılar ve gündem); kişi düzenleyip tutanağı normal şekilde kaydeder. Ajan kendisi kayıt oluşturmaz.
- **Risk ajanı:** Risks modülü gelince bağlandı. Riskler sekmesinde "Yapay zekâ ile risk öner" (metinden riskler) ve
  risk formunda "Yapay zekâ ile puan ve aksiyon öner". Öneriler risk formunu doldurur; kaydeden yine kişidir.
- **Sonraya kalanlar:** `AgentRun` / `AgentSuggestion` tabloları ve öneri paneli, diğer ajan tipleri.
- **Yalnızca sahte veriyle** çalışılır; model sağlayıcısı ve şirketin veri gönderme izni ayrıca kararlaştırılacak.

## 6. Yol haritası ve yön kararları

23 Kasım 2026'da alınan karar. Durum: **ekip onayı bekliyor.**

### Karar: yeni proje değil, bu projeyi dönüştürmek

Hedef üç şeyi birlikte karşılamak: şirketin kendi akışlarına uyum, başka şirketlere ve onların akışlarına uyum, ileride
iş analizinden geliştirmeye (kodlama) bağlanabilmek. Bunun için yeni bir projeye başlanmaz; bu proje bir yöne doğru dönüştürülür:
**"bir şirketin iş analizi aracı" → "şirketlerin kendi süreçlerini tanımladığı iş analizi platformu".**

**Neden:**
- Asıl zor parça hazır ve geneldir: akışlar veriden çalışıyor (aşama, adım, rol, Evet/Hayır, dönüş, koşullu atlama).
  Başka bir şirketin akışı da bir JSON dosyasıdır.
- Giriş, yetki, denetim kaydı, bildirim, kayıt modülleri (risk, gereksinim, açık konu, tutanak, doküman) ve yapay zekâ
  servisi her şirkette aynıdır; yeniden yazmanın getirisi yoktur.
- Yaklaşık 1000 backend testi ve CI, bugün çalışan davranışın güvencesidir; yeniden yazmada en çok kaybedilen şey budur.
- Başka şirketlere uyarlamayı engelleyen noktalar belli ve sınırlıdır: sabit 6 rol, üç tür atlama koşulu, bu şirketin
  formlarına göre yazılmış alanlar ve tek şirketli yapı. Her biri ayrı bir fazda değiştirilebilir; yeni projede de aynı
  sorunlar çözülmek zorunda kalırdı.

**Ne zaman yeniden düşünülür:** ürünün hedefi iş analizi olmaktan çıkıp Volobox gibi genel bir "her işi yapan yapay zekâ
çalışma alanı"na dönüşürse. Bu, ayrı bir ürün ve ayrı bir ekip ölçeği demektir.

### İlkeler

1. **Çekirdek her şirkette aynıdır; şirkete özel olan her şey veridir.** Rol, akış, atlama koşulu, form alanı, soru listesi
   ve doküman türü koda gömülmez; şablon ve şirket verisi olarak tutulur. Bu şirketin akışları "ilk şablon"dur.
2. **Büyük yetenekler çekirdeğe gömülmez, yanına servis olarak eklenir.** `ai/` servisinde olduğu gibi: çekirdek "ne
   yapılacak, kim onayladı" sorusunu yönetir; kodlama, sandbox, deploy gibi işler ayrı servis ya da hazır araçla
   (GitHub, Azure DevOps, Volobox) entegrasyon olur. Çekirdek iş verir ve sonucu (PR bağlantısı, durum) geri yazar.
3. **Her faz çalışan bir ürünle biter.** Dönüşüm bir kerede yapılmaz; her faz sonunda uygulama bugünkü şirket için
   çalışmaya devam eder ve testler yeşil kalır.

### Fazlar

| Faz | Kapsam | Sonunda |
| --- | --- | --- |
| **6 — Çok şirketli yapı** | `TenantId`, global filtre, şirket başına şablon kopyası, Platform ve Şirket Yöneticisi ([phase-6-plan.md](docs/phase-6-plan.md)) | Aynı kurulumda birbirinden ayrı birden fazla şirket |
| **7 — Şirkete göre ayarlanabilirlik** | Roller veriye (şirketin tanımladığı roller; adımın sorumlu ve onaylayan rolü bunlardan seçilir). Atlama koşulları sabit üç alan yerine kayıt alanlarına bakan kurallar olarak tanımlanır. Akış düzenleme ekranı (aşama ve adım ekleme, çıkarma, sıralama; migration gerekmez). Formlara şirkete özel ek alanlar (tanımı şirket verisinde, değeri kayıtta). Şirket başına yapay zekâ modeli ve kullanım takibi | Başka bir şirket kendi akışını, rollerini ve form alanlarını kod değişmeden kurar; bu şirketin akışları da aynı motorda bir şablon olur |
| **8 ve sonrası — Entegrasyonlar** | GitHub / Azure DevOps bağlantısı; akış adımından iş (issue, görev) açma ve durumunu geri okuma; kodlama ajanı ayrı servis olarak; e-posta ve takvim bağlantıları | İş analizinden geliştirmeye kesintisiz geçiş |

Faz 7 ve sonrasının iş listesi ve sorumluları, bir önceki faz biterken o fazın plan dosyasında (`docs/phase-N-plan.md`) yazılır.
