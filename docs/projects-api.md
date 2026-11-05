# Projects Modülü — API Sözleşmesi

Talep kaydı, proje listesi ve proje detayı. Veri modeli: [data-model.md](data-model.md) → `Project`.
Durum: **onaylandı** (5 Kasım 2026). Sorumlu: Dide.

Bütün uç noktalar AGENTS.md'deki ortak kurallara uyar: `/api/v1/` öneki, enum'lar metin, tarihler UTC / ISO 8601
(`DateOnly` alanları `2026-11-05` biçiminde), hatalar `ProblemDetails`, listeler `items` + `totalCount`,
JSON alanları camelCase, doğrulama hataları `errors.<alanAdı>` altında Türkçe.

## Ekip kararları

| # | Soru | Karar |
| --- | --- | --- |
| 1 | Talebi kim açabilir, kim düzenleyebilir? | **Yalnızca İş Analisti** talep açar (diğer roller 403). Düzenleme: giriş yapmış her aktif kullanıcı |
| 2 | Kapanmış (aktif olmayan) projenin temel bilgileri düzenlenebilir mi? | Hayır, 409. Proje kayıtlarındaki kuralla aynı |
| 3 | Prototipteki "Talepler" ve "Projeler" menüleri ayrı mı kalacak? | Tek liste, `phase` filtresiyle: aşama 1–3 → talep, 4–13 → proje (aşağıda) |
| 4 | Liste sütunları ve filtreler yeterli mi? | Evet: aşağıdaki `ProjectListItemDto` ve filtreler |
| 5 | Formdaki kullanıcı listesi (`GET /users`) kimde? | Dide yazar (`Identity` modülünün klasöründe); Baran onayladı |

## Erva ile alınan kararlar

- Yeni talep **`Active`** durumda ve akışın **ilk adımında (1.1)** oluşturulur. 1.1'in metni talep kaydının kendisidir;
  adımı tamamlamak `Process`'in işidir.
- `Status`, `CurrentStepId` ve `Priority` alanlarını **yalnızca `Process`** değiştirir; `Level`'i **yalnızca `Assessment`**.
  `Projects`'in hiçbir uç noktası bu dört alanı almaz ve değiştirmez.

## Ortak kurallar

- **Okuma:** giriş yapmış her kullanıcı.
- **Talep açma:** yalnızca aktif bir **İş Analisti** (karar 1). Diğer roller 403 alır.
- **Güncelleme:** giriş yapmış her aktif kullanıcı (karar 1), yalnızca aktif projede (karar 2).
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
| `phase` | `Request` → mevcut adımı aşama 1–3'te olan aktif projeler (talep); `Project` → aşama 4–13 (karar 3) |
| `search` | Kod, proje adı veya müşteri adında geçen metin (büyük/küçük harf duyarsız) |
| `businessAnalystId` | İş analisti |
| `page`, `pageSize` | Sayfalama |

**Sıralama:** en yeni talep önce (`Code` azalan).

## DTO'lar

### Okuma

```jsonc
// ProjectListItemDto
{
  "id": 1,
  "code": "ISDLC-2026-014",
  "name": "Saha Servis Mobil Uygulaması",
  "customerName": "Anadolu Enerji A.Ş. (örnek)",
  "requestType": "NewProject",
  "status": "Active",
  "currentStep": { "id": 30, "code": "7.4", "stageOrder": 7, "stageShortTitle": "Detaylı Analiz" }, // aktif değilse null
  "businessAnalyst": { "id": 1, "fullName": "Elif Yılmaz" },                                       // atanmadıysa null
  "level": 3,           // seviye belirlenmediyse null
  "priority": "High",   // proje kabul edilmediyse null
  "startDate": "2026-09-01",
  "dueDate": "2027-02-07"
}

// ProjectDto = ProjectListItemDto alanları +
{
  "description": "Saha teknisyenlerinin iş emirlerini mobilde alması...",
  "source": "External",
  "requestChannel": "Written",
  "requesterName": "Saha Operasyon Müdürü",
  "requiresNda": true,
  "projectManager": { "id": 3, "fullName": "Ayşe Demir" },  // atanmadıysa null
  "createdBy": { "id": 1, "fullName": "Elif Yılmaz" },
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
  "requestChannel": "Written",
  "requestType": "NewProject",
  "customerName": "Ege Gıda Dağıtım Ltd.",
  "requesterName": "Satış Direktörü",
  "requiresNda": false,
  "businessAnalystId": 1,     // isteğe bağlı
  "projectManagerId": null,   // isteğe bağlı
  "startDate": "2026-11-05",
  "dueDate": "2027-03-01"     // isteğe bağlı
}
```

| Alan | Kural |
| --- | --- |
| `name` | Zorunlu, en fazla 200 karakter |
| `description` | Zorunlu, en fazla 2000 karakter |
| `source`, `requestChannel`, `requestType` | Zorunlu |
| `customerName`, `requesterName` | Zorunlu, en fazla 200 karakter |
| `requiresNda` | Gönderilmezse `false` |
| `businessAnalystId` | İsteğe bağlı; aktif bir **İş Analisti** olmalı |
| `projectManagerId` | İsteğe bağlı; aktif bir **Proje Yöneticisi** olmalı |
| `startDate` | Zorunlu (form bugünü önerir) |
| `dueDate` | İsteğe bağlı; `startDate`'ten önce olamaz |

Metin alanlarının başındaki ve sonundaki boşluklar kırpılır; yalnızca boşluktan oluşan metin "girilmemiş" sayılır.

### Proje kodu

`ISDLC-{yıl}-{sıra}`: yıl, talebin **oluşturulduğu yıl** (UTC); sıra o yıl içinde artan 3 haneli numara (`ISDLC-2026-019`).
Yıl değişince sıra 001'den başlar. Kod bir kez verilir, değişmez.

İki talep aynı anda aynı numarayı alırsa (`Code` benzersiz) sunucu yeni numarayla **kendisi tekrar dener** (en fazla 3 kez);
yine olmazsa 409 döner. Açık konulardan farkı: talep formu uzun olduğu için kullanıcıdan tekrar göndermesi istenmez.

## Hata durumları

| Durum | Kod | Örnek `detail` |
| --- | --- | --- |
| Proje bulunamadı | `404` | "Proje bulunamadı." |
| Talebi İş Analisti olmayan biri açmaya çalıştı | `403` | "Talebi yalnızca İş Analisti açabilir." |
| Alanlar geçersiz | `400` | `errors.name`: "Proje adı zorunludur." |
| Seçilen kişi uygun değil | `400` | "Seçilen iş analisti aktif bir İş Analisti değil." |
| Bitiş tarihi başlangıçtan önce | `400` | `errors.dueDate`: "Bitiş tarihi başlangıç tarihinden önce olamaz." |
| Proje aktif değil (güncelleme) | `409` | "Proje aktif değil; bilgileri değiştirilemez." |
| Aynı anda kaydedildi / numara çakıştı | `409` | "Proje bu arada değiştirildi. Sayfayı yenileyin." |

## Bildirim noktaları

`INotificationService` üzerinden ([process-api.md](process-api.md) → Bildirim noktaları):

| Olay | Kime | Mesaj örneği |
| --- | --- | --- |
| Projeye iş analisti veya proje yöneticisi atandı (yeni talep ya da değişiklik) | Atanan kişi (işlemi yapan kendisi değilse) | "ISDLC-2026-019 · Bayi Sipariş Portalı projesine iş analisti olarak atandınız." |

## Kod yerleşimi

| Katman | Dosyalar |
| --- | --- |
| `Isdlc.Domain/Projects/` | `Project`, enum'lar (mevcut) |
| `Isdlc.Application/Projects/` | `IProjectService`, `ProjectService`, DTO'lar, `ProjectFilter`, kod üretimi |
| `Isdlc.Api/Controllers/` | `ProjectsController` (`/api/v1/projects`) |
| `Isdlc.Application/Identity/`, `Isdlc.Api/Controllers/` | `UserLookupDto`, `GET /users` (karar 5) |

## Ekranlar (frontend)

| Ekran | Yer | Not |
| --- | --- | --- |
| Proje listesi | `features/projects/project-list` | Filtreler, arama, sayfalama |
| Talep formu (yeni / düzenle) | `features/projects/project-form` | Doğrulama hataları alan altında |
| Proje detayı | `features/projects/project-detail` | Üstte özet, altta sekmeler |

**Proje detayı sekmeleri:** her sekme `projectId` alan ayrı bir bileşendir; sahibi kendi modülünü yazar.

| Sekme | Bileşen | Sahibi |
| --- | --- | --- |
| Genel Bilgiler | `Projects` | Dide |
| Süreç | `app-project-process` | Erva |
| Açık Konular | `features/open-issues` | Erva |
| Toplantı Tutanakları | `features/meetings` | Erva |
| Seviye & Kabul, Risk, Gereksinimler, Dokümanlar, Kayıt | Faz 2–3 | Modül sahibi |
