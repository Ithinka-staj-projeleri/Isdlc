# Process Modülü — API Sözleşmesi

`Process` modülünün uç noktaları, DTO'ları, iş kuralları ve hata durumları.
Veri modeli ve süreç ilerleme kuralları için: [data-model.md](data-model.md).
Durum: 13 aşamalı tek akış **uygulandı.** Birden çok akış (Faz 4, iş E1) **önerildi**, bkz. [Akışlar](#akışlar-faz-4).
Yetki kuralı ekip kararı bekliyor (aşağıda). Sorumlu: Erva; Faz 4'te Dide ([phase-4-plan.md](phase-4-plan.md)).

Bütün uç noktalar AGENTS.md'deki ve Faz 0'daki ortak kurallara uyar: `/api/v1/` öneki, enum'lar metin,
tarihler UTC / ISO 8601, hatalar `ProblemDetails`, listeler `items` + `totalCount`.
JSON alan adları camelCase'dir.

## Akışlar (Faz 4)

Şirketin her iş türü için ayrı bir akışı var ve sonradan yenileri gelecek (BT destek, Ar-Ge destek…). Bu yüzden akış
koda gömülmez, **veri** olarak tutulur: aşamalar ve adımlar bir akışa (`ProcessFlow`) bağlanır, her kayıt hangi akışı
izlediğini bilir (`Project.ProcessFlowId`). Yeni bir akış eklemek = yeni bir seed dosyası; migration gerekmez.

| Akış (`Code`) | Ad | Tür (`Kind`) | Bölüm / adım | Projeye dönüştüğü bölüm |
| --- | --- | --- | --- | --- |
| `new-software-project` | Yeni Yazılım Projesi | `Request` | 4 / 19 | 3 (Proje başlatma ve geliştirme) |
| `add-on` | Devam Eden Proje Ek Modül veya Geliştirme | `Request` | 4 / 17 | — (ana projenin altında görünür, bkz. D3) |
| `process-improvement` | Süreç İyileştirme | `Improvement` | 4 / 8 | — |
| `isdlc-v1` | ISDLC v12 (eski, 13 aşama) | `Request` | 13 / 75 | 4 · `IsActive = false` |

- **Eski akış silinmez** (kayıt silinmez kuralı). Pasife alınır: mevcut projeler onda kalır ve sonuna kadar ilerleyebilir,
  yeni talep ona açılamaz.
- **Haftalık kaynak planlama ve aylık faturalama** bu motoru kullanmaz. Bir talebe bağlı değiller, her hafta / ay yeniden
  başlıyorlar; kendi kayıtlarında basit bir durum alanı tutarlar (B6, B7).
- **Süreç iyileştirme önerisi** de bir `Project` kaydıdır (ekip kararı, 11.11.2026). Müşteri ve talep sahibi alanları bu akışta
  boş kalabilir; kayıt Talepler ve Projeler listelerinde görünmez, Süreç İyileştirme menüsünde listelenir (E5). Böylece süreç
  geçmişi, bildirimler ve denetim kaydı değişmeden çalışır.

**Hangi kayıt hangi akışa girer**

| Kayıt nereden açılır | Akış |
| --- | --- |
| Talepler → Yeni talep (`POST /projects`) | `new-software-project` |
| Proje detayı → Ek Geliştirmeler → Yeni (D3) | `add-on` |
| Süreç İyileştirme → Yeni öneri (E5) | `process-improvement` |

İleride gelen akışlar için talep formuna "iş tipi" seçimi eklenir; seçilen tip akışı belirler. `RequestType` alanı
bilgi amaçlı kalır, akışı seçmez.

### Adım koşulları (atlanan adımlar)

Bazı adımlar her kayıtta yapılmaz. Adımda en fazla üç koşul tutulur; hepsi sağlanmıyorsa adım **atlanır**:
akış o adıma hiç uğramaz ve `StepRecord` yazılmaz.

| Koşul (`ProcessStep`) | Adım yapılır, eğer… | Örnek |
| --- | --- | --- |
| `RequiresExternalRequest` | `Project.Source = External` | Yeni proje 4: Müşteri uygunluğu (iç talepte "doğrudan proje", 3 → 5) |
| `RequiresOffer` | `Project.RequiresOffer = true` (talep kaydında "teklif var / yok") | Yeni proje 10–11: Teklif ve anlaşma (teklifsizde 9 → 12) |
| `MinLevel` | `Project.Level ≥ MinLevel` | Ek geliştirme 8: Kapsam ve efor (Seviye 2–3); 9: Teklif (Seviye 3, 8 → 10) |

- `Project.Level` henüz belirlenmediyse `MinLevel` koşulu **sağlanmış sayılır** (adım atlanmaz). Seviye, koşullu adımlardan
  önceki bir adımda belirlendiği için normal akışta bu durum olmaz.
- Koşullar her geçişte projenin o anki değerleriyle değerlendirilir. Talep sırasında "teklif yok" "teklif var"a dönerse
  teklif adımları artık atlanmaz.
- Geri dönüşlerde (`ProcessStepJump`) hedef adımın koşulu kontrol edilmez; seed yalnızca anlamlı dönüşleri tanımlar.

**Sonraki adım:** aynı akışta, `Order`'ı mevcut adımdan büyük ve koşulları sağlanan **ilk** adım. Yoksa kayıt `Closed` olur.
**İlk adım:** talep açılırken akışın koşulları sağlanan ilk adımı.

### Talep mi, proje mi

Talepler ve Projeler menüleri aynı `Project` tablosunu gösterir. Hangisinde görüneceğini akış belirler
(`ProcessFlow.ProjectStageOrder`): kaydın mevcut adımının bölümü bu değere ulaştıysa kayıt **proje**, ulaşmadıysa **talep**dir.
Değer boşsa kayıt hiçbir zaman projeye dönüşmez. Kapanmış kayıtlar Projeler listesinin "tümü" görünümündedir (bugünkü gibi).
`Improvement` türündeki akışların kayıtları iki listede de görünmez.

Bugün `ProjectService` ve `DashboardService` içindeki "aşama 1–3 taleptir" sabiti (`LastRequestStageOrder = 3`) bu kurala
çevrilir.

## Uç noktalar

| İstek | Açıklama | Başarılı cevap |
| --- | --- | --- |
| `GET /api/v1/process-flows` | Akışlar (adımsız). `?includeInactive=true` pasifleri de getirir | `200` `ProcessFlowListItemDto[]` |
| `GET /api/v1/process-flows/{code}` | Akışın bölümleri ve adımları (Süreç Kütüphanesi) | `200` `ProcessFlowDto` |
| ~~`GET /api/v1/process-stages`~~ | **Kalkar** (Faz 4): aşamalar artık bir akışa ait; yerine `process-flows/{code}` | — |
| `GET /api/v1/projects/{projectId}/process` | Projenin süreç durumu | `200` `ProjectProcessDto` |
| `GET /api/v1/projects/{projectId}/step-records` | Süreç geçmişi (sayfalı) | `200` `PagedResult<StepRecordDto>` |
| `POST /api/v1/projects/{projectId}/process/complete` | Karar olmayan mevcut adımı tamamlar | `200` `ProjectProcessDto` |
| `POST /api/v1/projects/{projectId}/process/answer` | Mevcut karar adımına Evet/Hayır cevabı verir | `200` `ProjectProcessDto` |
| `POST /api/v1/projects/{projectId}/process/reject` | Talebi yönetici kararıyla reddeder | `200` `ProjectProcessDto` |

İşlem uç noktaları (`POST`) güncel süreç durumunu döndürür; ekranın ayrıca `GET` yapmasına gerek kalmaz.

## DTO'lar

### Okuma

```jsonc
// ProcessFlowListItemDto — GET /process-flows (Faz 4)
{
  "id": 2,
  "code": "new-software-project",
  "name": "Yeni Yazılım Projesi",
  "kind": "Request",            // ProcessFlowKind: Request, Improvement
  "isActive": true,
  "projectStageOrder": 3,       // null: kayıt hiç projeye dönüşmez
  "stepCount": 19
}

// ProcessFlowDto — GET /process-flows/{code} (Faz 4)
{
  "id": 2,
  "code": "new-software-project",
  "name": "Yeni Yazılım Projesi",
  "kind": "Request",
  "isActive": true,
  "projectStageOrder": 3,
  "stages": [ /* ProcessStageDto[] */ ]
}

// ProcessStageDto — Faz 4'te "bölüm"; order akış içinde 1'den başlar
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
  "noBranchText": "Revizyon talepleri tutanağa veya e-postaya yazılır; ... Akış 4.9’a dönülür.",
  "responsibleRole": "BusinessAnalyst",
  "approverRole": null,
  "noOutcome": null,
  "noJumps": [
    { "targetStepId": 17, "targetStepCode": "4.9", "label": "Akış 4.9 adımına dön" }
  ],
  // Faz 4: hepsi false / null ise adım her kayıtta yapılır
  "condition": { "requiresExternalRequest": false, "requiresOffer": false, "minLevel": null }
}

// ProjectProcessDto
{
  "projectId": 3,
  "projectCode": "ISDLC-2026-016",   // Faz 4'te Proje No sayıya döner (D1); alan adı o PR'da değişir
  "flow": { "code": "new-software-project", "name": "Yeni Yazılım Projesi" },   // Faz 4
  "skippedStepIds": [104, 110, 111], // Faz 4: koşulu sağlanmayan adımlar; ekran "atlanır" diye gösterir
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
  Faz 4: yalnızca kaydın akışındaki ve **atlanmayan** adımlar sayılır (mevcut adımdan önceki atlanmayan adım sayısı / atlanmayan adım sayısı).
- `skippedStepIds` (Faz 4): kaydın akışında, projenin o anki değerlerine göre koşulu sağlanmayan adımlar
  (bkz. [Adım koşulları](#adım-koşulları-atlanan-adımlar)).
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
hiçbir şey yapmayan bir uygulama kaydediliyordu; artık `NotificationService` bildirimleri kullanıcılara yazar
([notifications-api.md](notifications-api.md)).

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

Bildirimler kayıt başarıyla kaydedildikten sonra gönderilir. Kullanıcı bildirimi üst bardaki zilde görür;
kendi yaptığı işlem için bildirim almaz.

## Kod yerleşimi

| Katman | Dosyalar |
| --- | --- |
| `Isdlc.Domain/Process/` | `ProcessFlow` (Faz 4), `ProcessStage`, `ProcessStep`, `ProcessStepJump`, `StepRecord`, enum'lar |
| `Isdlc.Application/Process/` | `IProcessService`, `ProcessService`, DTO'lar; veritabanısız kurallar: `ProcessProgress` (ilerleme, aşama durumu, yetki), `ProcessTransitions` (işlemden sonraki durum ve adım), `StepConditions` (Faz 4: adım koşulları, sonraki / ilk adım), `ProcessMappings` (adım → DTO ifadesi) |
| `Isdlc.Application/Notifications/` | `INotificationService`, `NotificationService` (Notifications modülü) |
| `Isdlc.Infrastructure/Process/` | EF yapılandırmaları, seeder |
| `Isdlc.Api/Controllers/` | `ProcessStagesController` (`/process-stages`), `ProjectProcessController` (`/projects/{projectId}/process`, `.../process/complete`, `.../process/answer`, `.../process/reject`, `/projects/{projectId}/step-records`) |

İş kuralları (ilerleme, yetki, doğrulama) yalnızca `ProcessService` içindedir; controller'lar yalnızca isteği servise iletir.

## Ekranlar (frontend)

| Ekran | Yer | Not |
| --- | --- | --- |
| Süreç Kütüphanesi | `features/process/process-library` | Faz 4: üstte akış seçimi (`GET /process-flows`), altta seçilen akışın bölümleri ve adımları (`GET /process-flows/{code}`). Koşullu adımlarda etiket: "Yalnızca dış talepte", "Teklif hazırlanacaksa", "Seviye 2 ve üstü" |
| Proje detayı → Süreç sekmesi | `features/process/project-process` (`app-project-process`) | Aşama listesi, adımlar, mevcut adımdaki işlem butonları. Faz 4: akışın adı başlıkta; `skippedStepIds`'teki adımlar soluk ve "Atlanır" etiketiyle |
| Süreç geçmişi | `features/process/step-history` (`app-step-history`) | Süreç sekmesinin içinde |

Proje detay sayfası `Projects` modülüne (Dide) aittir. Süreç sekmesi bu sayfaya `projectId` girdisi alan
`app-project-process` bileşeni olarak eklenir; iki modül arasındaki tek bağlantı budur.
