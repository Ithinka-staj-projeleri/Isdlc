# AGENTS.md — Ortak Geliştirme Yönergesi

Bu dosya projede çalışan bütün kodlama ajanları ve geliştiriciler için ortak kaynaktır.
Ajan her oturumun başında bu dosyayı okur. Bir kural değişirse önce bu dosya güncellenir.

## 1. Proje

ISDLC (Yazılım Geliştirme Yaşam Döngüsü) sürecini yöneten, şirket içi kullanılacak bir iş analizi platformu.
Şu anki hedef basit bir **MVP**'dir.

Ürün adı henüz kesin değildir. Kodda kök ad olarak `Isdlc` kullanılır.

## 2. Teknoloji yığını

| Katman | Teknoloji |
| --- | --- |
| Frontend | Angular, TypeScript |
| Backend | ASP.NET Core Web API (.NET) |
| Veritabanı | MSSQL (lokalde Docker ile) |
| Veri erişimi | Entity Framework Core, code-first migrations |
| Kod ve iş takibi | GitHub |
| Yapay zekâ ajanları | Python (FastAPI), ayrı servis (`ai/`); yalnızca backend çağırır, veritabanına erişmez |

Yığına yeni bir kütüphane veya paket eklenmeden önce geliştiriciye sorulur.

## 3. Planlanan modüller

Aşağıdaki liste kesin değildir; kapsam netleştikçe güncellenir.

**MVP'de yapılması planlananlar**

| Modül | Kod adı | Kısaca |
| --- | --- | --- |
| Giriş ve kullanıcı/rol yönetimi | `Identity` | Kullanıcı girişi, roller |
| Talepler ve projeler | `Projects` | Talep kaydı, proje listesi ve detayı |
| Süreç akışı | `Process` | 13 aşamalı ISDLC adımları, Evet/Hayır kararları, onaylar |
| Seviye ve kabul | `Assessment` | Seviye belirleme, proje kabul puanlaması |
| Doküman takibi | `Documents` | Proje dokümanlarının durumu |
| Gereksinimler | `Requirements` | Gereksinim / User Story kayıtları |
| Risk yönetimi | `Risks` | Risk kaydı, olasılık × etki skoru |
| Açık konular | `OpenIssues` | Soru, aksiyon, karar, değişiklik talebi kayıtları |
| Toplantı tutanakları | `Meetings` | Tutanak, aksiyonların açık konulara aktarılması |
| Gösterge paneli | `Dashboard` | Özet göstergeler |
| Bildirimler | `Notifications` | Uygulama içi bildirimler |
| Denetim kaydı | `Audit` | Yapılan değişikliklerin kaydı |
| Yapay zekâ yardımcıları | `Agents` | Toplantı notlarından karar ve aksiyon önerisi (ekip onayıyla MVP'ye alındı); metinden risk, risk için puan ve önleyici aksiyon önerisi |

**Sonraki sürümlere bırakılanlar:** Test & UAT, Teklif & Maliyet, Kaynak Planlama, Faturalama,
Proje Devir, Otomasyon, toplantı ve risk yardımcıları dışındaki AI özellikleri (bkz. [docs/ai-vision.md](docs/ai-vision.md)).

## 4. Klasör yapısı

```
/
├─ AGENTS.md
├─ README.md
├─ docker-compose.yml
├─ .env.example
├─ backend/
│  ├─ Isdlc.sln
│  ├─ src/
│  │  ├─ Isdlc.Api/              # controller'lar, middleware, Program.cs
│  │  ├─ Isdlc.Application/      # servisler, DTO'lar, doğrulama, iş kuralları
│  │  ├─ Isdlc.Domain/           # entity'ler, enum'lar
│  │  └─ Isdlc.Infrastructure/   # DbContext, EF yapılandırmaları, migrations, seed
│  └─ tests/
│     └─ Isdlc.Application.Tests/
├─ ai/                         # yapay zekâ ajanları (Python): isdlc_ai/server.py HTTP servisi, prompt'lar, eval'ler
├─ frontend/
│  └─ src/app/
│     ├─ core/                   # kimlik doğrulama, interceptor, guard, sayfa düzeni
│     ├─ shared/                 # ortak bileşenler, pipe'lar
│     └─ features/<modul>/       # her modülün ekranları, servisleri, modelleri
└─ docs/
```

Backend'de her katmanın içinde modül klasörleri bulunur (`Isdlc.Application/Risks/` gibi).
Klasör adları Bölüm 3'teki kod adlarıdır.
Frontend'de `features/` altındaki klasörler aynı modüllerin kebab-case halidir (`open-issues`).

## 5. İsimlendirme kuralları

**Genel**
- Arayüzde görünen bütün metinler **Türkçe**.
- Kod, dosya, tablo, kolon, API yolu ve commit mesajları **İngilizce**.

**Backend (C#)**
| Öğe | Kural | Örnek |
| --- | --- | --- |
| Sınıf, metot, property | PascalCase | `RiskService`, `CalculateScore()` |
| Arayüz | `I` + PascalCase | `IRiskService` |
| Yerel değişken, parametre | camelCase | `projectId` |
| Private alan | `_` + camelCase | `_dbContext` |
| DTO | Amaç + `Dto` | `CreateRiskDto`, `RiskListItemDto` |
| Controller | Çoğul kaynak + `Controller` | `RisksController` |
| Entity | Tekil | `Risk`, `OpenIssue` |
| Enum ve değerleri | PascalCase | `ProjectStatus.Active` |

**Veritabanı**
| Öğe | Kural | Örnek |
| --- | --- | --- |
| Tablo | Çoğul, PascalCase | `Risks`, `OpenIssues` |
| Kolon | PascalCase | `ProjectId`, `CreatedAt` |
| Birincil anahtar | `Id` | `Id` |
| Yabancı anahtar | `<Entity>Id` | `ProjectId` |
| Kullanıcıya gösterilen kod | `Code` kolonu | `ISDLC-2026-014`, `RSK-001` |
| Migration | Ne yaptığını anlatan PascalCase | `AddRisksTable` |

**API**
- Yol: `/api/v1/<kaynak>`, kaynak çoğul ve kebab-case: `/api/v1/open-issues`
- Alt kaynak: `/api/v1/projects/{id}/risks`
- Ayrıntılı kurallar için Bölüm 7.

**Frontend (Angular)**
| Öğe | Kural | Örnek |
| --- | --- | --- |
| Dosya adı | kebab-case + tür | `risk-list.component.ts`, `risk.service.ts` |
| Sınıf | PascalCase + tür | `RiskListComponent`, `RiskService` |
| Model (interface) | PascalCase, tür eki yok | `Risk`, `OpenIssue` |
| Değişken, metot | camelCase | `loadRisks()` |
| Bileşen seçici | `app-` + kebab-case | `app-risk-list` |

**Git**
- Dal: `feature/kisa-ad`, `fix/kisa-ad` (ör. `feature/login-page`). GitHub Issue açılmaz.
- Commit: Conventional Commits, kapsam modül adı: `feat(risks): add risk score calculation`
- `main` dalına doğrudan push yapılmaz; değişiklikler PR ile gelir.
- PR, CI (`.github/workflows/ci.yml`) yeşil olmadan birleştirilmez. CI şunları kontrol eder:
  - **Backend:** derleme (uyarılar hata sayılır), testler, entity değişikliği için migration üretilmiş mi
  - **Frontend:** derleme, testler
  - **AI service:** Python testleri (gerçek model çağrılmaz)
- PR açmadan önce aynı kontroller lokalde çalıştırılabilir:
  - `dotnet build backend/Isdlc.sln -warnaserror` ve `dotnet test backend/Isdlc.sln` (Docker açık olmalı; bkz. Bölüm 9)
  - `dotnet ef migrations has-pending-model-changes --project backend/src/Isdlc.Infrastructure --startup-project backend/src/Isdlc.Api`
  - `frontend` klasöründe: `npm run build` ve `npx ng test --watch=false`
  - `ai` klasöründe: `.venv/Scripts/python -m pytest` (kurulum: [ai/README.md](ai/README.md))

## 6. Alan sözlüğü

Aynı kavram için herkesin aynı ismi kullanması amacıyla:

| Türkçe | Kodda |
| --- | --- |
| Talep / Proje | `Project` |
| Talep Sahibi | `Requester` (`Project.RequesterName`; sistem kullanıcısı değildir) |
| Talep kaynağı / kanalı / türü | `RequestSource`, `RequestChannel`, `RequestType` |
| Proje durumu | `ProjectStatus` (`Active`, `Closed`, `Rejected`, `Lost`, `Redirected`) |
| Öncelik | `ProjectPriority` |
| Kullanıcı | `User` |
| Rol | `UserRole` (kullanıcı başına tek rol) |
| İş akışı (yeni yazılım projesi, ek geliştirme, süreç iyileştirme, kaynak planlama, faturalama) | `ProcessFlow` (`Project.ProcessFlowId`; türü `ProcessFlowKind`: `Request`, `Improvement`, `ResourcePlanning`, `Billing`) |
| Aşama (akıştaki "bölüm") | `ProcessStage` |
| Adım | `ProcessStep` |
| Atlanan adım (yalnızca dış talepte / teklif hazırlanacaksa / seviye ≥ N) | `ProcessStep.RequiresExternalRequest`, `RequiresOffer`, `MinLevel` |
| Teklif hazırlanacak mı | `Project.RequiresOffer` |
| "Hayır" cevabında dönülecek adım | `ProcessStepJump` |
| Adımda yapılan işlem | `StepRecord` |
| İşlem türü (Tamamlandı, Evet, Hayır, Reddedildi) | `StepAction` |
| Oluşturan / güncelleyen bilgisi | `AuditableEntity` (`CreatedAt`, `CreatedById`, `UpdatedAt`, `UpdatedById`) |
| Seviye | `Level` (`Project.Level`, 1–3; yalnızca `Assessment` uygular) |
| Seviye belirleme ve kabul puanlaması | `ProjectAssessment` (seviye cevapları 1–3, kabul puanları 1–5) |
| Kabul önerisi | `AcceptanceRecommendation` (`Accept`, `Conditional`, `RejectOrPostpone`; saklanmaz, hesaplanır) |
| Doküman durumu | `DocumentStatus` (`NotStarted`, `InProgress`, `InReview`, `Approved`, `NotRequired`) |
| Doküman türü (takip edilen doküman) / projedeki dokümanı | `DocumentType` / `ProjectDocument` |
| Gereksinim (User Story dahil) | `Requirement` |
| Gereksinim türü / önceliği / durumu | `RequirementType` (`Business`, `Functional`, `NonFunctional`), `RequirementPriority` (`MustHave`, `ShouldHave`, `CouldHave`, `WontHave`), `RequirementStatus` (`Draft`, `InReview`, `Approved`, `Cancelled`) |
| Kabul ölçütleri | `AcceptanceCriteria` |
| Risk | `Risk` (`Probability`, `Impact`, `Score`; skor saklanmaz, hesaplanır) |
| Risk kategorisi / durumu / seviyesi | `RiskCategory`, `RiskStatus` (`Open`, `Monitoring`, `Closed`), `RiskSeverity` (`Low`, `Medium`, `High`) |
| Önleyici aksiyon | `Risk.Mitigation` |
| Talep Sahibi ile paylaşılır | `Risk.IsSharedWithRequester` |
| Açık konu | `OpenIssue` |
| Açık konu türü / durumu | `OpenIssueType` (`Question`, `Action`, `Decision`, `ChangeRequest`, `Dependency`, `Assumption`), `OpenIssueStatus` (`Open`, `InProgress`, `Closed`) |
| Sorumlu (açık konu) | `OwnerUserId` (ekip üyesi) ya da `OwnerName` (sistem dışı kişi) |
| Termin | `DueDate` |
| Toplantı tutanağı | `Meeting` |
| Toplantı türü | `MeetingType` |
| Katılımcılar / gündem | `Participants`, `Agenda` |
| Tutanaktan aktarılan kayıt | `OpenIssue.SourceMeetingId` |
| Bildirim | `Notification` (alıcı başına bir satır; `ReadAt` okunma zamanı). Modüller yalnızca `INotificationService` ile gönderir |
| Denetim kaydı | `AuditLog` |
| Yapay zekâ yardımcısı / önerisi | `Agents` modülü; tutanak önerileri `MeetingSuggestionsDto`, risk önerileri `RiskSuggestionsDto` ve `RiskAssessmentSuggestionDto` (kaydedilmez, formu doldurur) |

Yeni bir kavram gerektiğinde önce bu tabloya eklenir.

## 7. Ortak API kuralları

Backend iskeletinde uygulanmıştır; yeni kod bu yapıları kullanır.

| Konu | Kural | Backend'de nerede |
| --- | --- | --- |
| Yol | Controller'lar `ApiControllerBase`'den türer. Yol, controller adından otomatik üretilir: `OpenIssuesController` → `/api/v1/open-issues` | `Isdlc.Api/Common/ApiControllerBase.cs` |
| Hatalar | Her hata `ProblemDetails` biçiminde döner (`title`, `status`, `detail`, `traceId`) | `Isdlc.Api/Common/GlobalExceptionHandler.cs` |
| Kayıt yok | Servis `NotFoundException` fırlatır → 404 | `Isdlc.Application/Common/Exceptions/` |
| İş kuralı ihlali | Servis `BusinessRuleException` fırlatır → 400. Mesaj kullanıcıya gösterilir, Türkçe yazılır | `Isdlc.Application/Common/Exceptions/` |
| Yetki yok | Servis `ForbiddenException` fırlatır → 403 | `Isdlc.Application/Common/Exceptions/` |
| Servis kullanılamıyor | Bağlı olunan servis (ör. yapay zekâ servisi) kapalıysa `ServiceUnavailableException` → 503. Mesaj Türkçe | `Isdlc.Application/Common/Exceptions/` |
| Çakışma | Servis `ConflictException` fırlatır → 409. `RowVersion` uyuşmazlığı (`DbUpdateConcurrencyException`) da otomatik 409 olur | `Isdlc.Application/Common/Exceptions/` |
| Beklenmeyen hata | 500 ve genel bir mesaj döner; ayrıntı yalnızca loga yazılır | `GlobalExceptionHandler` |
| Enum | JSON'da metin olarak gönderilir: `"Active"` | `Program.cs` |
| Tarih | `DateTime` UTC tutulur, ISO 8601 ve `Z` ekiyle gönderilir: `2026-11-04T12:00:00Z`. Veritabanından okunan tarihler otomatik UTC işaretlenir | `Persistence/Converters/UtcDateTimeConverter.cs` |
| Sayfalama (istek) | Liste uç noktaları `?page=1&pageSize=20` alır: `List([FromQuery] PageRequest pageRequest)`. Sayfa boyutu en fazla 100. **Parametreye `page` adını verme**, değerler okunmaz. Serviste sıralanmış sorguya `.ApplyPaging(pageRequest)` uygulanır | `Isdlc.Application/Common/Models/PageRequest.cs`, `PagingExtensions.cs` |
| Sayfalama (cevap) | Liste uç noktaları `PagedResult<T>` döner: `{ "items": [...], "totalCount": 42 }` | `Isdlc.Application/Common/Models/PagedResult.cs` |
| JSON alan adları | camelCase (`totalCount`, `projectId`) | ASP.NET Core varsayılanı |

Controller'lar `try/catch` yazmaz; hatayı servis fırlatır, `GlobalExceptionHandler` çevirir.

**Doğrulama hataları (400)** DTO'daki özniteliklerden (`[Required]`, `[MaxLength]`, `[Range]` vb.) otomatik üretilir
(`Isdlc.Api/Common/ValidationConfiguration.cs`):

```json
{ "title": "Girilen bilgiler geçersiz.", "status": 400,
  "errors": { "expectedStepId": ["Mevcut adım belirtilmelidir."], "note": ["En fazla 1000 karakter olabilir."] } }
```

- `errors` anahtarları JSON alan adıdır (camelCase); frontend hatayı aynı adlı form alanının altında gösterir.
  Okunamayan bir istek gövdesi (bozuk JSON) boş anahtarla (`""`) döner.
- Özniteliğe yazılan `ErrorMessage` kullanılır. Yazılmamışsa Türkçe varsayılan mesaj çıkar ("Bu alan zorunludur.",
  "En fazla 1000 karakter olabilir."). Alana özel bir açıklama gerekiyorsa `ErrorMessage` Türkçe yazılır.

## 8. Veritabanı kuralları

Veri modeli: [docs/data-model.md](docs/data-model.md).

- **Entity yeri:** `Isdlc.Domain/<Modul>/`. EF ayarları `Isdlc.Infrastructure/<Modul>/<Entity>Configuration.cs`
  (`IEntityTypeConfiguration<T>`); DbContext bu sınıfları kendiliğinden bulur. Yeni entity için `IsdlcDbContext`'e bir `DbSet` eklenir.
- **Veri erişimi:** Application servisleri veritabanına `IApplicationDbContext` üzerinden erişir
  (`Isdlc.Application/Common/Interfaces/`); `IsdlcDbContext`'i doğrudan kullanmaz. Yeni entity'nin `DbSet`'i
  hem `IsdlcDbContext`'e hem `IApplicationDbContext`'e eklenir. Yalnızca okuyan sorgular `AsNoTracking()` ile
  yazılır ve `Select` ile doğrudan DTO'ya çevrilir.
- **Servis kaydı:** Her modül kendi servisini `Isdlc.Application/DependencyInjection.cs` içindeki `AddApplication()`'a ekler.
- **Metin kolonları** her zaman `HasMaxLength` ile sınırlanır.
- **Enum'lar** veritabanında metin olarak tutulur (`"Active"`); ayrıca ayar gerekmez.
- **Kayıt silinmez.** Bütün ilişkiler `Restrict`'tir; kullanıcı pasife alınır, süreç geçmişi korunur.
- **Denetim alanları:** İşlem verisi tutan entity'ler `AuditableEntity`'den türer ve yapılandırmada `builder.ConfigureAuditFields()`
  çağrılır. Alanları `AuditableEntityInterceptor` kaydederken doldurur; kodda elle atanmaz.
- **Denetim kaydı (`AuditLog`):** Proje verisi tutan entity'ler `IAudited` arayüzünü taşır; projeye ait kayıtlar
  (Risk, Requirement vb.) ayrıca `IProjectRecord` taşır. Bu entity'lerdeki her değişiklik, alan bazında eski/yeni
  değerleriyle `AuditLogs` tablosuna otomatik yazılır (`AuditLogInterceptor`). Seed tabloları, `StepRecord` ve `User` loglanmaz.
- **Giriş yapan kullanıcı:** `ICurrentUserService.UserId`. API'de JWT'nin `sub` claim'inden okunur
  (`HttpCurrentUserService`). Giriş: `POST /api/v1/auth/login` (`{ email, password }`), cevap
  `{ token, expiresAt, user }`; hatalı giriş, pasif kullanıcı ve bilinmeyen e-posta aynı 401'i alır.
  Girişsiz hiçbir uç nokta çalışmaz (`[AllowAnonymous]` olanlar hariç). Swagger'da token'ı "Authorize"
  düğmesine yapıştırın. Lokal test parolası ve JWT anahtarı `dotnet user-secrets` ile verilir
  (`Identity:TestPassword`, `Jwt:SigningKey`; bkz. README); repoya yazılmaz. Servis testlerinde kullanıcı
  `TestUser` ile verilir (Bölüm 9).
- **Bildirimler:** Modüller bildirimi yalnızca `INotificationService` ile gönderir (`Isdlc.Application/Notifications/`).
  `NotificationService` bildirimi alıcıya yazar (zil ve `/notifications`); kimse kendi işlemi için bildirim almaz ve bildirim
  kaydedilemezse asıl işlem bozulmaz (bkz. [docs/notifications-api.md](docs/notifications-api.md)).
- **Yapay zekâ servisi:** Backend, `ai/` servisini `Agents:ServiceUrl` adresinden `Agents:ServiceKey` anahtarıyla çağırır
  (`Isdlc.Infrastructure/Agents/`). Adres boşsa ya da servis cevap vermiyorsa `GET /api/v1/agents/status` kapalı döner ve
  ekranlar yapay zekâ butonlarını göstermez. Ajan kayıt oluşturmaz; önerileri kişi inceleyip normal ekrandan kaydeder.
  Yalnızca sahte veriyle çalışılır (şirketin veri gönderme kararı bekleniyor).
- **Eşzamanlılık:** `Project.RowVersion` aynı kaydın aynı anda güncellenmesini yakalar ve 409 döner.
- **Seed:** `Isdlc.Infrastructure/Seed/*.json` dosyaları, tablo boşsa uygulama açılırken yüklenir. Örnek projeler yalnızca geliştirme ortamında yüklenir.
  Süreç akışı (`process-flow.json`) ayrıca her açılışta mevcut veritabanıyla eşitlenir: metin, rol ve dönüş değişiklikleri için
  yalnızca dosya değiştirilir; adım eklemek, çıkarmak veya sıralamak migration ister (bkz. data-model.md → Seed verisi).
- **Migration:** Geliştirme ortamında API açılırken otomatik uygulanır. Üretmek için (repo kökünde):
  `dotnet ef migrations add <Ad> --project backend/src/Isdlc.Infrastructure --startup-project backend/src/Isdlc.Api --output-dir Persistence/Migrations`

## 9. Backend testleri

- Testler `backend/tests/Isdlc.Application.Tests` içinde, test edilen modülün klasöründe durur (`Process/ProcessServiceTests.cs`).
- Çapraz test kuralı için: PLANNING.md → Birlikte çalışma kuralları.
- **Veritabanı kullanan testler** gerçek SQL Server ile çalışır (Testcontainers). Test sınıfına `[Collection("Database")]`
  yazılır ve kurucuda `DatabaseFixture` alınır. Servis, `database.CreateScope()` ile açılan kapsamdan alınır.
- Fixture test çalışması başına bir kez Docker'da SQL Server açar, gerçek migration'ları ve seed'i yükler
  (süreç akışı, kullanıcılar, örnek projeler). Bu yüzden `dotnet test` için **Docker açık olmalıdır**.
- Bütün testler aynı veritabanını paylaşır. Veri değiştiren bir test, başka testlerin kullandığı seed kayıtlarını
  değiştirmez; kendi kaydını oluşturup onunla çalışır.
- Ortak yardımcılar (`tests/Isdlc.Application.Tests/Common/`):
  - `TestProjects.CreateAsync(database, "4.7")`: istenen adımda bekleyen yeni bir proje; seed kullanıcılarının e-postaları da burada.
  - `TestUser`: servisi belirli bir kullanıcı giriş yapmış gibi kurmak için (`new ProcessService(db, new TestUser(id), ...)`).
  - `RecordingNotificationService`: gönderilen bildirimleri listede tutar; test ne gönderildiğini kontrol eder.
- Saf kural sınıfları (`ProcessProgress`, `ProcessTransitions` gibi) veritabanısız test edilir; bütün kombinasyonlar burada denenir.
- Test adı neyi kontrol ettiğini söyler: `Metot_Durum_Beklenen` (ör. `GetStages_Returns13StagesInOrder`).
