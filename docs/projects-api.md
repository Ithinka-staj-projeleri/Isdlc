# Projects Modülü — API Sözleşmesi

Talep kaydı, proje listesi ve proje detayı. Veri modeli: [data-model.md](data-model.md) → `Project`.
Durum: **onaylandı** (5 Kasım 2026). Faz 4 değişiklikleri (D1, aşağıda "Talep Kayıt Formu'na uyum"): **onaylandı ve uygulandı** (12 Kasım 2026). Sorumlu: Dide.

Bütün uç noktalar AGENTS.md'deki ortak kurallara uyar: `/api/v1/` öneki, enum'lar metin, tarihler UTC / ISO 8601
(`DateOnly` alanları `2026-11-05` biçiminde), hatalar `ProblemDetails`, listeler `items` + `totalCount`,
JSON alanları camelCase, doğrulama hataları `errors.<alanAdı>` altında Türkçe.

## Ekip kararları

| # | Soru | Karar |
| --- | --- | --- |
| 1 | Talebi kim açabilir, kim düzenleyebilir? | **Yalnızca İş Analisti** talep açar (diğer roller 403). Düzenleme: giriş yapmış her aktif kullanıcı |
| 2 | Kapanmış (aktif olmayan) projenin temel bilgileri düzenlenebilir mi? | Hayır, 409. Proje kayıtlarındaki kuralla aynı |
| 3 | Prototipteki "Talepler" ve "Projeler" menüleri ayrı mı kalacak? | Tek liste, `phase` filtresiyle: onay bekleyen talepler Talepler'de, geri kalan her şey (kabul edilmiş ve kapanmış) Projeler'de. Bir kayıt iki listede birden görünmez (aşağıda) |
| 4 | Liste sütunları ve filtreler yeterli mi? | Evet: aşağıdaki `ProjectListItemDto` ve filtreler |
| 5 | Formdaki kullanıcı listesi (`GET /users`) kimde? | Dide yazar (`Identity` modülünün klasöründe); Baran onayladı |

## Erva ile alınan kararlar

- Yeni talep **`Active`** durumda ve akışın **ilk adımında** (yeni yazılım projesi akışında 1: "Talep alınır ve Proje No
  verilir") oluşturulur. İlk adımın metni talep kaydının kendisidir; adımı tamamlamak `Process`'in işidir.
- `Status`, `CurrentStepId` ve `Priority` alanlarını **yalnızca `Process`** değiştirir; `Level`'i **yalnızca `Assessment`**.
  `Projects`'in hiçbir uç noktası bu dört alanı almaz ve değiştirmez.

## Talep Kayıt Formu'na uyum (Faz 4, D1)

Kaynak: şirketin Talep Kayıt Formu (ISDLC-Form-Analiz-002, v2.0) ve Proje Kayıt Listesi (ISDLC-Form-PM-032).
Kararlar: [phase-4-plan.md](phase-4-plan.md) → Alınan kararlar.

**Formdaki alanlar ve API karşılıkları:**

| Formdaki alan | API alanı | Değişiklik |
| --- | --- | --- |
| Proje No | `code` | `"ISDLC-2026-014"` yerine düz sayı: `"14"` (aşağıda "Proje No") |
| Proje Adı | `name` | — |
| Talep Tarihi | `requestDate` | `startDate` bu adı alır (aşağıda) |
| Talep Sahibi (Firma / Birim) | `customerName` | Yalnızca ekrandaki etiket değişir |
| Talebi İleten Kişi | `requesterName` | Yalnızca ekrandaki etiket değişir |
| İletişim (E-posta / Telefon) | `requesterContact` | **Yeni**, isteğe bağlı |
| Talebi Kayıt Altına Alan Kişi | `createdBy` | Yeni alan yok: talebi sisteme giren İş Analisti zaten tutuluyor |
| Talep Açıklaması | `description` | — |
| Talep Kanalı | `requestChannel` | `Written` / `Verbal` yerine `Email` / `Phone` |
| Talep Kaynağı | `source` | `Internal` / `External` / `Affiliated` (İlişkili şirket, soru bankasının üçüncü alt kategorisi) |
| Notlar | `notes` | **Yeni**, isteğe bağlı |
| Teklif hazırlanacak mı (akış) | `requiresOffer` | **Yeni** API alanı (entity'de E1'den beri var); zorunlu |

Formda olmayan `requestType`, `requiresNda`, `businessAnalystId`, `projectManagerId` ve `dueDate` aynen kalır.

**Kararlar:**

| # | Konu | Karar |
| --- | --- | --- |
| D1-1 | `startDate` ne olacak? | **`requestDate` (Talep tarihi)** olur. Kayıt bir talep olarak açıldığı için "başlangıç" o talebin tarihidir; projenin başlama tarihi akıştaki "proje başlatma" adımıyla gelir. Kolon yeniden adlandırılır, veri korunur |
| D1-2 | Talebi kayda alan kişi ayrı alan mı? | Hayır. Talebi yalnızca İş Analisti açar (karar 1); kaydı açan kişi `createdBy` olarak zaten tutuluyor ve detayda "Talebi kayda alan" etiketiyle gösterilir |
| D1-3 | Talep kaynağı ve teklif bilgisi sonradan değiştirilebilir mi? | Yalnızca kayıt, akışın bu iki bilgiye bağlı **ilk adımına gelmeden** (yeni yazılım projesinde 4. adım). Sonrasında 409: hangi adımların atlandığı bu iki bilgiyle belirlendiği için geçmiş tutarsız olur. Aynı değeri tekrar göndermek hata değildir |
| D1-4 | Mevcut kayıtlar | Migration çevirir: kod `ISDLC-2026-014` → `14`, kanal `Written` → `Email`, `Verbal` → `Phone` |

## Ortak kurallar

- **Okuma:** giriş yapmış her kullanıcı.
- **Talep açma:** yalnızca aktif bir **İş Analisti** (karar 1). Diğer roller 403 alır.
- **Güncelleme:** giriş yapmış her aktif kullanıcı (karar 1), yalnızca aktif projede (karar 2).
  Talep kaynağı ve teklif bilgisi akıştaki ilk koşullu adıma kadar (karar D1-3).
- **Silme yok.** Yanlış açılmış talebi Yazılım Departman Yöneticisi `Process` üzerinden reddeder (`POST .../process/reject`).
- **Eş zamanlı düzenleme:** İstemci `rowVersion` göndermez; temel bilgilerde son kaydeden kazanır, her değişiklik
  denetim kaydında görünür (proje kayıtlarıyla aynı kural). `Project.RowVersion` aynı anda yapılan iki kaydı
  yakalamaya devam eder (409).

## Uç noktalar

| İstek | Açıklama | Başarılı cevap |
| --- | --- | --- |
| `GET /projects` | Proje listesi (filtreli, sayfalı) | `200` `PagedResult<ProjectListItemDto>` |
| `GET /projects/{projectId}` | Proje detayı | `200` `ProjectDto` |
| `POST /projects` | Yeni talep | `201` `ProjectDto` (`Location: /api/v1/projects/{id}`) |
| `PUT /projects/{projectId}` | Temel bilgileri güncelle | `200` `ProjectDto` |
| `GET /users` | Formdaki kişi seçimi için kullanıcı listesi (karar 5) | `200` `UserLookupDto[]` |

**Liste filtreleri** (hepsi isteğe bağlı):

| Parametre | Açıklama |
| --- | --- |
| `status` | `ProjectStatus` (`Active`, `Closed`, …) |
| `phase` | Her kayıt tam olarak bir aşamadadır (karar 3). `Request` → onay bekleyen talepler: aktif ve mevcut adımı akışın proje bölümünden önce (yeni yazılım projesinde bölüm 1–2). `Project` → geri kalan her şey: proje bölümündeki aktif projeler ve bütün kapanmış kayıtlar (talep aşamasında kapanmış olanlar dahil). Yalnızca aktif projeler için `phase=Project&status=Active`. Ayrıntı: [process-api.md](process-api.md) → "Talep mi, proje mi" |
| `search` | Proje No (birebir: `14`, 114'ü bulmaz) ya da proje adında veya talep sahibinde (firma / birim) geçen metin (büyük/küçük harf duyarsız) |
| `businessAnalystId` | İş analisti |
| `page`, `pageSize` | Sayfalama |

**Sıralama:** en yeni talep önce (Proje No azalan).

## DTO'lar

### Okuma

```jsonc
// ProjectListItemDto
{
  "id": 1,
  "code": "14",                                     // Proje No
  "name": "Saha Servis Mobil Uygulaması",
  "customerName": "Anadolu Enerji A.Ş. (örnek)",   // Talep sahibi (firma / birim)
  "requestType": "NewProject",
  "status": "Active",
  "currentStep": { "id": 30, "code": "14", "stageOrder": 3, "stageShortTitle": "Geliştirme" }, // aktif değilse null
  "businessAnalyst": { "id": 1, "fullName": "Elif Yılmaz" },                                   // atanmadıysa null
  "level": 3,           // seviye belirlenmediyse null
  "priority": "High",   // proje kabul edilmediyse null
  "requestDate": "2026-09-01",
  "dueDate": "2027-02-07"
}

// ProjectDto = ProjectListItemDto alanları +
{
  "description": "Saha teknisyenlerinin iş emirlerini mobilde alması...",
  "source": "External",
  "requestChannel": "Email",
  "requesterName": "Saha Operasyon Müdürü",          // Talebi ileten kişi
  "requesterContact": "saha.operasyon@ornek.com",     // girilmediyse null
  "notes": null,                                       // girilmediyse null
  "requiresOffer": true,
  "requiresNda": true,
  "projectManager": { "id": 3, "fullName": "Ayşe Demir" },  // atanmadıysa null
  "createdBy": { "id": 1, "fullName": "Elif Yılmaz" },      // Talebi kayda alan
  "createdAt": "2026-09-01T09:00:00Z",
  "updatedAt": null
}

// UserLookupDto — GET /users?role=BusinessAnalyst (yalnızca aktif kullanıcılar, ada göre sıralı)
{ "id": 1, "fullName": "Elif Yılmaz", "role": "BusinessAnalyst" }
```

### Yazma

```jsonc
// CreateProjectDto (POST) ve UpdateProjectDto (PUT) — aynı alanlar
{
  "name": "Bayi Sipariş Portalı",
  "description": "Bayilerin sipariş vermesi ve durumunu takip etmesi.",
  "source": "External",
  "requestChannel": "Email",
  "requestType": "NewProject",
  "customerName": "Ege Gıda Dağıtım Ltd.",
  "requesterName": "Satış Direktörü",
  "requesterContact": "0232 000 00 00",   // isteğe bağlı
  "notes": null,                          // isteğe bağlı
  "requiresOffer": true,
  "requiresNda": false,
  "businessAnalystId": 1,     // isteğe bağlı
  "projectManagerId": null,   // isteğe bağlı
  "requestDate": "2026-11-05",
  "dueDate": "2027-03-01"     // isteğe bağlı
}
```

| Alan | Kural |
| --- | --- |
| `name` | Zorunlu, en fazla 200 karakter |
| `description` | Zorunlu, en fazla 2000 karakter |
| `source`, `requestChannel`, `requestType` | Zorunlu |
| `customerName`, `requesterName` | Zorunlu, en fazla 200 karakter |
| `requesterContact` | İsteğe bağlı, en fazla 200 karakter (e-posta ya da telefon; biçim kontrol edilmez) |
| `notes` | İsteğe bağlı, en fazla 2000 karakter |
| `requiresOffer` | Zorunlu (`true` / `false`). Gönderilmezse 400, sessizce `false` sayılmaz |
| `requiresNda` | Gönderilmezse `false` |
| `businessAnalystId` | İsteğe bağlı; aktif bir **İş Analisti** olmalı |
| `projectManagerId` | İsteğe bağlı; aktif bir **Proje Yöneticisi** olmalı |
| `requestDate` | Zorunlu (form bugünü önerir) |
| `dueDate` | İsteğe bağlı; `requestDate`'ten önce olamaz |
| `source`, `requiresOffer` (yalnızca PUT) | Kayıt akışın bu iki bilgiye bağlı ilk adımına geldiyse değiştirilemez (karar D1-3) |

Metin alanlarının başındaki ve sonundaki boşluklar kırpılır; yalnızca boşluktan oluşan metin "girilmemiş" sayılır
(isteğe bağlı alanlarda `null` olarak saklanır).

### Proje No

Düz bir sıra numarası: `1`, `2`, `3`… Yeni talep, **yeni yazılım projesi akışındaki kayıtların en büyük numarası + 1**'i alır;
yıl değişince sıfırlanmaz. Numara bir kez verilir, değişmez. JSON'da ve veritabanında metin olarak tutulur (`"code": "14"`),
çünkü ek geliştirmeler (D3) ana projenin numarasıyla gösterilecek (`14 / EG-01`).

Süreç iyileştirme, kaynak planlama ve faturalama kayıtları bu sırayı kullanmaz; kendi kodlarını kendi işlerinde (E5, B6, B7) belirler.

İki talep aynı anda aynı numarayı alırsa (`Code` benzersiz) sunucu yeni numarayla **kendisi tekrar dener** (en fazla 3 kez);
yine olmazsa 409 döner. Açık konulardan farkı: talep formu uzun olduğu için kullanıcıdan tekrar göndermesi istenmez.

## Hata durumları

| Durum | Kod | Örnek `detail` |
| --- | --- | --- |
| Proje bulunamadı | `404` | "Proje bulunamadı." |
| Talebi İş Analisti olmayan biri açmaya çalıştı | `403` | "Talebi yalnızca İş Analisti açabilir." |
| Alanlar geçersiz | `400` | `errors.name`: "Proje adı zorunludur." |
| Teklif bilgisi gönderilmedi | `400` | `errors.requiresOffer`: "Teklif hazırlanıp hazırlanmayacağı belirtilmelidir." |
| Seçilen kişi uygun değil | `400` | "Seçilen iş analisti aktif bir İş Analisti değil." |
| Bitiş tarihi talep tarihinden önce | `400` | `errors.dueDate`: "Bitiş tarihi talep tarihinden önce olamaz." |
| Proje aktif değil (güncelleme) | `409` | "Proje aktif değil; bilgileri değiştirilemez." |
| Talep kaynağı / teklif bilgisi ilk koşullu adımdan sonra değişti | `409` | "Talep kaynağı ve teklif bilgisi 4. adıma gelindikten sonra değiştirilemez." |
| Aynı anda kaydedildi / numara çakıştı | `409` | "Proje bu arada değiştirildi. Sayfayı yenileyin." |

## Bildirim noktaları

`INotificationService` üzerinden ([process-api.md](process-api.md) → Bildirim noktaları):

| Olay | Kime | Mesaj örneği |
| --- | --- | --- |
| Projeye iş analisti veya proje yöneticisi atandı (yeni talep ya da değişiklik) | Atanan kişi (işlemi yapan kendisi değilse) | "19 · Bayi Sipariş Portalı projesine iş analisti olarak atandınız." |

## Kod yerleşimi

| Katman | Dosyalar |
| --- | --- |
| `Isdlc.Domain/Projects/` | `Project`, enum'lar (mevcut) |
| `Isdlc.Application/Projects/` | `IProjectService`, `ProjectService`, DTO'lar, `ProjectFilter`, Proje No üretimi |
| `Isdlc.Api/Controllers/` | `ProjectsController` (`/api/v1/projects`) |
| `Isdlc.Application/Identity/`, `Isdlc.Api/Controllers/` | `UserLookupDto`, `GET /users` (karar 5) |

## Ekranlar (frontend)

| Ekran | Yer | Not |
| --- | --- | --- |
| Proje listesi | `features/projects/project-list` | Filtreler, arama, sayfalama |
| Talep formu (yeni / düzenle) | `features/projects/project-form` | Talep Kayıt Formu'nun sırasıyla; doğrulama hataları alan altında. Kaynak ve teklif bilgisi değiştirilemiyorsa alan kilitli ve nedeni yazılı |
| Proje detayı | `features/projects/project-detail` | Üstte özet, altta sekmeler |

**Proje detayı sekmeleri:** her sekme `projectId` alan ayrı bir bileşendir; sahibi kendi modülünü yazar.

| Sekme | Bileşen | Sahibi |
| --- | --- | --- |
| Genel Bilgiler | `Projects` | Dide |
| Süreç | `app-project-process` | Dide (Faz 4) |
| Açık Konular | `features/open-issues` | Erva |
| Toplantı Tutanakları | `features/meetings` | Erva |
| Seviye & Kabul, Risk, Gereksinimler, Dokümanlar, Kayıt | Faz 2–3 | Modül sahibi |

## Açık konular

1. **Proje No nereden başlayacak?** Şirketin Proje Kayıt Listesi'nde (032) numara verilmiş projeler varsa uygulamanın
   numaraları onlarla çakışır. Şimdilik 1'den başlar; analistlerden mevcut son numara öğrenilirse başlangıç numarası
   ayar olarak eklenir.
2. **Talep türü (`requestType`)** formda yok. Ek geliştirme ayrı bir akış ve kayıt olduğu için (D3) `AdditionalModule`
   değeri gereksizleşebilir; D3'te ele alınır.
