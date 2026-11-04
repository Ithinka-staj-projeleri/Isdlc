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

## 3. Birlikte çalışma kuralları

- **Modül içi sıra:** önce entity ve DTO, sonra API uç noktası (Swagger'dan denenir), en son ekran.
  Her biri küçük ve ayrı bir PR olur.
- **Modüller arası bağlantı:** Modüller birbirinin içine doğrudan girmez, önceden anlaşılmış
  arayüzler üzerinden konuşur. Örnek: `Process`, bildirim için yalnızca `INotificationService`'i çağırır;
  içini `Notifications` modülü doldurur.
- **Migration çakışması:** Migration, PR açmadan hemen önce, main dala alındıktan sonra üretilir.
  PR başına tek migration olur. Çakışma çıkarsa kendi migration'ınızı silip yeniden üretin.
- **Ortak dosyalar** (`Program.cs`, DbContext, `app.routes.ts`): küçük ve sık PR'lar açılır.
- **İş takibi:** Her iş için bir GitHub Issue açılır. Dal adları AGENTS.md'deki kurala uyar.
  Her PR'ı en az bir kişi inceler.
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
- Onay adımları: 2.3, 3.2, 4.5, 5.1 → Yazılım Departman Yöneticisi.
- Talebi kapatan sonuçlar: 1.2 → Yönlendirildi, 3.2 → Reddedildi, 5.3 → Kaybedildi.

**Veri modeli için not:** Akış her zaman ileri gitmez; bazı "Hayır" cevapları önceki bir adıma döner
(ör. 4.7 → 4.1, 9.2 → 8.1). `StepRecord` bir adımın birden fazla kez işlenmesine izin vermelidir.
