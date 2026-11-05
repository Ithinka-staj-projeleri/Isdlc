# Process Modülü — API Sözleşmesi

`Process` modülünün uç noktaları, DTO'ları, iş kuralları ve hata durumları.
Veri modeli ve süreç ilerleme kuralları için: [data-model.md](data-model.md).
Durum: **uygulandı.** Okuma uç noktaları ve `complete` main'de; `answer` (#27) ve `reject` (#29) PR'da.
Yetki kuralı ekip kararı bekliyor (aşağıda). Sorumlu: Erva.

Bütün uç noktalar AGENTS.md'deki ve Faz 0'daki ortak kurallara uyar: `/api/v1/` öneki, enum'lar metin,
tarihler UTC / ISO 8601, hatalar `ProblemDetails`, listeler `items` + `totalCount`.
JSON alan adları camelCase'dir.

## Uç noktalar

| İstek | Açıklama | Başarılı cevap |
| --- | --- | --- |
| `GET /api/v1/process-stages` | 13 aşama ve adımları | `200` `ProcessStageDto[]` |
| `GET /api/v1/projects/{projectId}/process` | Projenin süreç durumu | `200` `ProjectProcessDto` |
| `GET /api/v1/projects/{projectId}/step-records` | Süreç geçmişi (sayfalı) | `200` `PagedResult<StepRecordDto>` |
| `POST /api/v1/projects/{projectId}/process/complete` | Karar olmayan mevcut adımı tamamlar | `200` `ProjectProcessDto` |
| `POST /api/v1/projects/{projectId}/process/answer` | Mevcut karar adımına Evet/Hayır cevabı verir | `200` `ProjectProcessDto` |
| `POST /api/v1/projects/{projectId}/process/reject` | Talebi yönetici kararıyla reddeder | `200` `ProjectProcessDto` |

İşlem uç noktaları (`POST`) güncel süreç durumunu döndürür; ekranın ayrıca `GET` yapmasına gerek kalmaz.

## DTO'lar

### Okuma

```jsonc
// ProcessStageDto
{
  "id": 4,
  "order": 4,
  "title": "Ön Analiz, Talep Sahibi Teyidi ve Tahmini Plan",
  "shortTitle": "Ön Analiz & Plan",
  "steps": [ /* ProcessStepDto[] */ ]
}

// ProcessStepDto
{
  "id": 27,
  "code": "5.4",
  "order": 27,
  "text": "Teklif revizyon istenmeden onaylandı mı?",
  "isDecision": true,
  "noBranchText": "Revizyon talepleri tutanağa ... Akış 4.1'e, ... Akış 4.9'a dönülür.",
  "responsibleRole": "BusinessAnalyst",
  "approverRole": null,
  "noOutcome": null,
  "noJumps": [
    { "targetStepId": 9,  "targetStepCode": "4.1", "label": "Kapsam değişiyor" },
    { "targetStepId": 17, "targetStepCode": "4.9", "label": "Yalnızca fiyat veya takvim değişiyor" }
  ]
}

// ProjectProcessDto
{
  "projectId": 3,
  "projectCode": "ISDLC-2026-016",
  "status": "Active",
  "currentStep": { /* ProcessStepDto, proje aktif değilse null */ },
  "progressPercent": 20,
  "stages": [
    { "stageId": 1, "order": 1, "shortTitle": "Talep Kaydı", "stepCount": 3, "state": "Completed" },
    { "stageId": 4, "order": 4, "shortTitle": "Ön Analiz & Plan", "stepCount": 9, "state": "Current" },
    { "stageId": 5, "order": 5, "shortTitle": "Teklif & Onay", "stepCount": 4, "state": "Pending" }
  ],
  "availableActions": {
    "canComplete": false,
    "canAnswer": true,
    "canReject": false,
    "blockedReason": null
  }
}

// StepRecordDto
{
  "id": 120,
  "stepId": 21,
  "stepCode": "4.7",
  "action": "No",
  "jumpedToStepCode": "4.1",
  "note": "Müşteri iki ekranda düzeltme istedi.",
  "performedBy": { "id": 1, "fullName": "Elif Yılmaz" },
  "performedAt": "2026-11-03T11:05:00Z"
}
```

**Hesaplanan alanlar**
- `progressPercent`: `Closed` ise 100. Aktifse `(currentStep.order - 1) / adım sayısı × 100`, aşağı yuvarlanır.
  Aktif değilse son `StepRecord`'un adımına göre aynı formül.
- `stages[].state` (`ProcessStageState`):
  - Aktif proje: mevcut adımın aşamasından öncekiler `Completed`, mevcut aşama `Current`, sonrakiler `Pending`.
  - `Closed` proje: hepsi `Completed`.
  - `Rejected` / `Lost` / `Redirected` proje: son `StepRecord`'un aşaması `Stopped`, öncekiler `Completed`, sonrakiler `Pending`.
- `availableActions`: giriş yapan kullanıcıya göre hesaplanır (bkz. Yetki kuralları).
  Kullanıcı mevcut adımda işlem yapamıyorsa `blockedReason` dolar, ör.
  "Bu adım Yazılım Departman Yöneticisi tarafından onaylanır."

**`step-records` sorgu parametreleri:** `page` (varsayılan 1), `pageSize` (varsayılan 20, en fazla 100).
Sıralama: en yeni kayıt önce.

### İşlem istekleri

```jsonc
// CompleteStepDto — POST .../process/complete
{ "expectedStepId": 15, "note": "Kavramsal tasarım hazırlandı." }

// AnswerStepDto — POST .../process/answer
{ "expectedStepId": 27, "answer": "No", "jumpTargetStepId": 17, "note": "Yalnızca fiyat revize edilecek." }

// RejectProjectDto — POST .../process/reject
{ "expectedStepId": 5, "reason": "Bir aydır talep sahibinden cevap alınamadı (2.3)." }
```

| Alan | Kural |
| --- | --- |
| `expectedStepId` | Zorunlu. Kullanıcının ekranda gördüğü mevcut adım (`currentStep.id`) |
| `answer` | Zorunlu. `StepAnswer` enum'u: `Yes`, `No`. Gönderilmezse 400 (sessizce `Yes` sayılmaz) |
| `jumpTargetStepId` | Yalnızca `answer = No` ve adımın dönüş seçenekleri varken zorunlu ve `currentStep.noJumps[].targetStepId` değerlerinden biri olmalı; aksi halde gönderilmez |
| `note` | İsteğe bağlı, en fazla 1000 karakter. Baştaki ve sondaki boşluklar kırpılır; boşsa kaydedilmez |
| `reason` | Zorunlu (yalnızca boşluk da kabul edilmez), en fazla 1000 karakter; kırpılarak `StepRecord.Note` alanına yazılır |

**Kontrollerin sırası** (üç işlemde aynı): proje var mı (404) → aktif mi (409) → `expectedStepId` mevcut adım mı (409)
→ kullanıcı bu işlemi yapabilir mi (403) → istek bu adıma uygun mu (400) → kaydet (`RowVersion` çakışması 409).

## Yetki kuralları

| İşlem | Kim yapabilir |
| --- | --- |
| Okuma uç noktaları | Giriş yapmış her kullanıcı |
| `complete` / `answer` — `ApproverRole` dolu adım | Yalnızca `ApproverRole` rolündeki kullanıcı |
| `complete` / `answer` — diğer adımlar | Yalnızca adımın `ResponsibleRole` rolündeki kullanıcı |
| `reject` | Yalnızca `SoftwareDepartmentManager` |

> **Ekip kararı bekleniyor:** Prototipte onay adımları dışında herkes her adımı işleyebiliyordu.
> Uygulanan kural daha sıkı (yalnızca sorumlu rol). Daha esnek bir kural istenirse
> (ör. projenin iş analisti her adımı işleyebilir) yalnızca bu tablo ve `ProcessProgress` değişir.

Kurallar `ProcessProgress` içinde tek yerdedir (`CanActOnStep`, `CanReject`). Hem `availableActions` (butonlar)
hem işlemlerdeki 403 kontrolü aynı fonksiyonları kullanır; ikisi birbirinden farklı sonuç veremez.
Yönetici her adımda reddedebilir, ama bu, sorumlu rolü başka olan bir adımı işleyebileceği anlamına gelmez.

## Hata durumları

Bütün hatalar `ProblemDetails` biçimindedir; `detail` alanı kullanıcıya gösterilebilecek Türkçe bir mesajdır.

| Durum | Kod | Örnek `detail` |
| --- | --- | --- |
| Proje bulunamadı | `404` | "Proje bulunamadı." |
| İstek alanları geçersiz (otomatik doğrulama) | `400` | `errors` alanında: "Gerekçe zorunludur.", "Cevap (Evet/Hayır) belirtilmelidir." |
| `complete` bir karar adımına gönderildi | `400` | "Bu adım bir karar adımıdır; Evet/Hayır cevabı verilmelidir." |
| `answer` karar olmayan adıma gönderildi | `400` | "Bu adım bir karar adımı değil; Evet/Hayır cevabı verilmez, adım tamamlanır." |
| `Yes` ile birlikte `jumpTargetStepId` | `400` | "Evet cevabında dönüş adımı seçilmez." |
| `No`, adımın seçenekleri var ama `jumpTargetStepId` eksik veya listede değil | `400` | "Geçerli bir dönüş seçeneği seçilmelidir." |
| `No`, adımın seçeneği yok ama `jumpTargetStepId` gönderildi | `400` | "Bu adımda dönüş seçeneği yok; dönüş adımı seçilmez." |
| Giriş yapılmamış | `403` | "İşlem yapmak için giriş yapılmalıdır." |
| Kullanıcının rolü adıma yetmiyor | `403` | "Bu adım Yazılım Departman Yöneticisi tarafından onaylanır." / "Bu adım İş Analisti tarafından yapılır." |
| Yönetici olmayan kullanıcı reddediyor | `403` | "Talebi yalnızca Yazılım Departman Yöneticisi reddedebilir." |
| Proje aktif değil | `409` | "Proje aktif değil; süreçte işlem yapılamaz." |
| `expectedStepId` mevcut adımla aynı değil | `409` | "Proje bu arada başka bir adıma geçti. Sayfayı yenileyin." |
| Aynı anda yapılan başka bir işlem kaydı değiştirdi (`RowVersion`) | `409` | "Kayıt bu arada başka biri tarafından değiştirildi. Sayfayı yenileyin." |

> Otomatik doğrulama hatalarının başlığı şimdilik ASP.NET varsayılanı ("One or more validation errors occurred.")
> ve alan adları PascalCase (`ExpectedStepId`); ortak bir düzeltme bekliyor (bkz. #25).

## Eş zamanlı işlem koruması

İki kullanıcı aynı adımı aynı anda işlerse adım iki kez ilerleyebilir. İki katmanlı koruma:

1. **`expectedStepId` kontrolü:** İstekteki adım, projenin `CurrentStepId`'si değilse `409` döner.
   Kullanıcının eski bir ekrana bakarak işlem yapmasını yakalar.
2. **`Project.RowVersion`:** İki istek kontrolü aynı anda geçebilir. Bunun için `Project`'te EF Core
   eşzamanlılık belirteci (`[Timestamp]` / SQL Server `rowversion`) bulunur. Kayıt okunduktan sonra
   başka biri değiştirdiyse `SaveChanges` `DbUpdateConcurrencyException` fırlatır; bu da `409`'a çevrilir.

3. **Proje her işlemde "değişti" işaretlenir:** Aynı adımda kalan bir "Hayır" projede hiçbir alanı değiştirmez;
   EF Core da değişmeyen kaydı güncellemediği için `RowVersion` kontrolü atlanırdı. Bu yüzden `CurrentStepId`
   her işlemde "değişti" işaretlenir ve güncelleme `RowVersion` kontrolüyle her zaman gönderilir. Değer değişmediği
   için denetim kaydına boş satır düşmez.

`StepRecord` eklenmesi ve `Project` güncellenmesi tek bir `SaveChanges` çağrısında yapılır; ya ikisi de kaydedilir ya hiçbiri.
Lokal denemede (aynı anda iki istek) ikinci isteğin `RowVersion` ile 409 aldığı görüldü.

## Bildirim noktaları

`Process`, `Notifications` modülüne yalnızca aşağıdaki arayüz üzerinden bağlanır.
Arayüz `Isdlc.Application` katmanında tanımlanır. `Notifications` modülü (Baran) hazır olana kadar
hiçbir şey yapmayan bir uygulama (`NullNotificationService`) kaydedilir.

```csharp
public interface INotificationService
{
    Task NotifyRoleAsync(UserRole role, int projectId, string message, CancellationToken cancellationToken);
    Task NotifyUsersAsync(IEnumerable<int> userIds, int projectId, string message, CancellationToken cancellationToken);
}
```

| Olay | Kime | Mesaj örneği |
| --- | --- | --- |
| Proje `ApproverRole`'ü olan bir adıma geldi (3.2, 4.5, 5.1) | O roldeki kullanıcılar | "ISDLC-2026-016 · 4.5 onayınızı bekliyor." |
| "Hayır" ile önceki bir adıma dönüldü | Projenin iş analisti | "ISDLC-2026-016 · 4.7 adımından 4.1 adımına dönüldü." |
| Proje kapandı (`Closed`, `Rejected`, `Lost`, `Redirected`) | Projenin iş analisti ve proje yöneticisi (atanmışsa) | "ISDLC-2026-011 kapandı: Kaybedildi." |

Bildirimler kayıt başarıyla kaydedildikten sonra gönderilir. `NullNotificationService` şimdilik yalnızca loga yazar
("Notification not sent ..."); lokal denemede bildirimin doğru anda tetiklendiği buradan görülebilir.

## Kod yerleşimi

| Katman | Dosyalar |
| --- | --- |
| `Isdlc.Domain/Process/` | `ProcessStage`, `ProcessStep`, `ProcessStepJump`, `StepRecord`, enum'lar |
| `Isdlc.Application/Process/` | `IProcessService`, `ProcessService`, DTO'lar; veritabanısız kurallar: `ProcessProgress` (ilerleme, aşama durumu, yetki), `ProcessTransitions` (işlemden sonraki durum ve adım), `ProcessMappings` (adım → DTO ifadesi) |
| `Isdlc.Application/Notifications/` | `INotificationService`, `NullNotificationService` |
| `Isdlc.Infrastructure/Process/` | EF yapılandırmaları, seeder |
| `Isdlc.Api/Controllers/` | `ProcessStagesController` (`/process-stages`), `ProjectProcessController` (`/projects/{projectId}/process`, `.../process/complete`, `.../process/answer`, `.../process/reject`, `/projects/{projectId}/step-records`) |

İş kuralları (ilerleme, yetki, doğrulama) yalnızca `ProcessService` içindedir; controller'lar yalnızca isteği servise iletir.

## Ekranlar (frontend)

| Ekran | Yer | Not |
| --- | --- | --- |
| Süreç Kütüphanesi | `features/process/process-library` | 13 aşama ve adımlar; `GET /process-stages` |
| Proje detayı → Süreç sekmesi | `features/process/project-process` (`app-project-process`) | Aşama listesi, adımlar, mevcut adımdaki işlem butonları |
| Süreç geçmişi | `features/process/step-history` (`app-step-history`) | Süreç sekmesinin içinde |

Proje detay sayfası `Projects` modülüne (Dide) aittir. Süreç sekmesi bu sayfaya `projectId` girdisi alan
`app-project-process` bileşeni olarak eklenir; iki modül arasındaki tek bağlantı budur.
