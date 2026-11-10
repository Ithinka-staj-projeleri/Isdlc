# Veri Modeli

Faz 0'ın temel entity'leri (`ProcessStage`, `ProcessStep`, `User`, `Project`, `StepRecord`), denetim kaydı
ve Faz 2'nin proje kayıtları (`OpenIssue`, `Meeting`). Diğer modüllerin entity'leri kendi fazlarında eklenir.
Kaynak: "ISDLC Analiz Merkezi" prototipi.
Durum: temel entity'ler uygulandı; `OpenIssue` ve `Meeting` **uygulandı, ekip onayı bekliyor.**

İsimlendirme AGENTS.md'ye uyar: entity tekil, tablo çoğul; `?` alanın boş bırakılabileceğini (nullable) gösterir.

## Genel bakış

```
ProcessStage 1 ──── * ProcessStep
ProcessStep  1 ──── * StepRecord * ──── 1 Project
User         1 ──── * StepRecord          (PerformedById)
User         1 ──── * Project             (BusinessAnalystId, ProjectManagerId)
ProcessStep  1 ──── * Project             (CurrentStepId)
ProcessStep  1 ──── * ProcessStepJump * ── 1 ProcessStep   (TargetStepId: "Hayır" ile dönülecek adım)
ProcessStep  1 ──── * StepRecord          (JumpedToStepId: geri dönüşte seçilen adım)
Project      1 ──── * OpenIssue
Project      1 ──── * Meeting
Meeting      1 ──── * OpenIssue           (SourceMeetingId: tutanaktan aktarılan karar ve aksiyonlar)
User         1 ──── * OpenIssue           (OwnerUserId: sorumlu ekip üyesi)
Project      1 ──── * Requirement
Project      1 ──── * ProjectDocument * ── 1 DocumentType
ProcessStage 1 ──── * DocumentType        (dokümanın ilk hazırlandığı aşama)
```

- **Ana veri:** `ProcessStage`, `ProcessStep`, `ProcessStepJump`, `DocumentType`. Seed ile yüklenir, kullanıcı tarafından değiştirilmez.
- **İşlem verisi:** `User`, `Project`, `StepRecord`, `OpenIssue`, `Meeting`, `Requirement`, `ProjectDocument`. Uygulama kullanıldıkça oluşur.

## Ortak temel sınıf: `AuditableEntity`

İşlem verisi tutan entity'ler (`Project`, `StepRecord`; `User` hariç) bu sınıftan türer.
Alanları denetim kaydı altyapısı `SaveChanges` sırasında otomatik doldurur; kodda elle atanmaz.

| Alan | Tür | Not |
| --- | --- | --- |
| `CreatedAt` | DateTime (UTC) | |
| `CreatedById` | int (FK → User) | |
| `UpdatedAt` | DateTime? (UTC) | |
| `UpdatedById` | int? (FK → User) | |

## Ana veri

### `ProcessStage` — 13 aşama

| Alan | Tür | Not |
| --- | --- | --- |
| `Id` | int | |
| `Order` | int | 1–13, benzersiz |
| `Title` | string | "Talebin Kayda Alınması" |
| `ShortTitle` | string | "Talep Kaydı" (menü ve listelerde) |

### `ProcessStep` — aşamalardaki adımlar

| Alan | Tür | Not |
| --- | --- | --- |
| `Id` | int | |
| `ProcessStageId` | int (FK → ProcessStage) | |
| `Code` | string | "4.7", benzersiz |
| `Order` | int | Bütün akış içindeki sıra; sonraki adım `Order + 1`'dir |
| `Text` | string | Adımın açıklaması |
| `IsDecision` | bool | Evet/Hayır sorusu mu |
| `NoBranchText` | string? | "Hayır ise" açıklaması |
| `ResponsibleRole` | `UserRole` | Adımı yapan rol |
| `ApproverRole` | `UserRole`? | Yalnızca onay adımlarında dolu |
| `NoOutcome` | `ProjectStatus`? | "Hayır" talebi kapatıyorsa sonuç |

Kurallar:
- Sorumlu rol, prototipteki gibi kodla hesaplanmaz; her adım için seed'de açıkça yazılır.
- `ApproverRole` doluysa adımı (karar adımı olsun olmasın) yalnızca o roldeki kullanıcı tamamlayabilir veya cevaplayabilir.
  Örnek 5.1: teklifi Satış Temsilcisi hazırlar (`ResponsibleRole`), Yazılım Departman Yöneticisi onaylayarak adımı kapatır (`ApproverRole`).
- Bir karar adımında `NoOutcome` ve dönüş seçenekleri (`ProcessStepJump`) birlikte bulunmaz.
- İkisi de yoksa "Hayır" cevabı projeyi aynı adımda bırakır (eksikler tamamlanınca tekrar cevaplanır).

### `ProcessStepJump` — "Hayır" cevabında dönülebilecek adımlar

Bir karar adımının birden fazla dönüş seçeneği olabilir. Örnek 5.4: kapsam değişiyorsa 4.1'e,
yalnızca fiyat veya takvim değişiyorsa 4.9'a dönülür. "Hayır" cevabı veren kullanıcı seçeneklerden birini seçer.

| Alan | Tür | Not |
| --- | --- | --- |
| `Id` | int | |
| `ProcessStepId` | int (FK → ProcessStep) | Karar adımı |
| `TargetStepId` | int (FK → ProcessStep) | Dönülecek adım |
| `Label` | string | Seçenek metni: "Kapsam değişiyor" |

## İşlem verisi

### `User`

| Alan | Tür | Not |
| --- | --- | --- |
| `Id` | int | |
| `FullName` | string | |
| `Email` | string | Benzersiz |
| `PasswordHash` | string | `Identity` modülünde kullanılır |
| `Role` | `UserRole` | Kullanıcı başına tek rol (MVP kararı) |
| `IsActive` | bool | Kullanıcı silinmez, pasife alınır |
| `CreatedAt` | DateTime (UTC) | |

### `Project` — talep ve proje aynı kayıttır

| Alan | Tür | Not |
| --- | --- | --- |
| `Id` | int | |
| `Code` | string | "ISDLC-2026-014"; yıl + yıl içinde artan 3 haneli sıra, otomatik verilir |
| `Name` | string | |
| `Description` | string | |
| `Source` | `RequestSource` | |
| `RequestChannel` | `RequestChannel` | |
| `RequestType` | `RequestType` | |
| `CustomerName` | string | Müşteri veya iç birim adı (MVP'de ayrı tablo yok) |
| `RequesterName` | string | Talep sahibi kişi veya unvan; sisteme giriş yapan bir kullanıcı değildir |
| `RequiresNda` | bool | Görüşme öncesi gizlilik sözleşmesi gerekiyor mu |
| `BusinessAnalystId` | int? (FK → User) | |
| `ProjectManagerId` | int? (FK → User) | |
| `Level` | int? | 1–3; seviye belirlenene kadar boş |
| `Priority` | `ProjectPriority`? | 3.2'de proje kabul edilirken verilir |
| `Status` | `ProjectStatus` | |
| `CurrentStepId` | int? (FK → ProcessStep) | Proje aktif değilse boş |
| `StartDate` | DateOnly | |
| `DueDate` | DateOnly? | |
| `RowVersion` | byte[] | EF Core eşzamanlılık belirteci (SQL Server `rowversion`); bkz. [process-api.md](process-api.md) |
| + `AuditableEntity` alanları | | |

### `StepRecord` — bir projede bir adımda yapılan her işlem

| Alan | Tür | Not |
| --- | --- | --- |
| `Id` | int | |
| `ProjectId` | int (FK → Project) | |
| `ProcessStepId` | int (FK → ProcessStep) | |
| `Action` | `StepAction` | |
| `JumpedToStepId` | int? (FK → ProcessStep) | Yalnızca geri dönen "Hayır" kayıtlarında dolu |
| `Note` | string? | `Rejected` işleminde gerekçe zorunludur |
| `PerformedById` | int (FK → User) | |
| `PerformedAt` | DateTime (UTC) | |
| + `AuditableEntity` alanları | | |

Aynı proje ve adım için birden fazla kayıt olabilir; bu, akıştaki geri dönüşleri (ör. 4.7 → 4.1) destekler.
Kayıtlar değiştirilmez ve silinmez; süreç geçmişi bu tablodan okunur.

### `AuditLog` — proje verisindeki her değişiklik

`IAudited` arayüzünü taşıyan entity'ler kaydedilirken otomatik yazılır; kodda elle eklenmez. Değiştirilmez ve silinmez.

| Alan | Tür | Not |
| --- | --- | --- |
| `Id` | long | |
| `EntityName` | string | "Project", "Risk" |
| `EntityId` | int | Değişen kaydın `Id`'si |
| `ProjectId` | int? (FK → Project) | `Project` için kendi `Id`'si; `IProjectRecord` kayıtlarında bağlı olduğu proje |
| `Action` | `AuditAction` | |
| `Changes` | string (JSON) | Değişen alanlar: `{"Status": {"old": "Active", "new": "Lost"}}` |
| `UserId` | int (FK → User) | Kaydın `CreatedById` / `UpdatedById` alanından |
| `CreatedAt` | DateTime (UTC) | Kaydın `CreatedAt` / `UpdatedAt` alanından |

- Loglanan: `Project` ve ileride eklenecek proje kayıtları (Risk, Requirement, OpenIssue…).
- Loglanmayan: ana veri (`ProcessStage`, `ProcessStep`, `ProcessStepJump`), `StepRecord` (kendisi zaten geçmiş tablosu), `User`.
- `Changes` içinde yer almayan alanlar: `Id`, `PasswordHash`, `RowVersion` ve `AuditableEntity` alanları (bilgileri log satırında var).
- Değeri gerçekten değişmeyen alan yazılmaz; hiçbir alan değişmediyse log satırı oluşmaz.
- Alan adları C# adlarıdır (`Status`); Türkçe karşılıklarını Audit ekranı gösterir ([audit-api.md](audit-api.md)).
- Kayıt ve logu aynı transaction'da yazılır: log yazılamazsa değişiklik de geri alınır.

## Proje kayıtları (Faz 2)

Bir projeye bağlı kayıtlar (`OpenIssue`, `Meeting`; ileride `Risk`, `Requirement`…) şu ortak kurallara uyar:

- `AuditableEntity`'den türer, `IAudited` ve `IProjectRecord` taşır: her değişiklik `AuditLogs`'a projesiyle birlikte yazılır.
- **Kod:** Kullanıcıya gösterilen `Code`, proje içinde artan 3 haneli numaradır (`AK-001`, `TT-001`).
  Yeni kayıtta projenin en büyük numarasının bir fazlası verilir. `(ProjectId, Code)` benzersizdir;
  iki kayıt aynı anda aynı numarayı alırsa ikincisi 409 ile reddedilir ve kullanıcı tekrar dener.
- **Yalnızca aktif projede** eklenir ve değiştirilir (`Project.Status = Active`); kapanmış projede okunur.
- **Silinmez.** Açık konu kapatılır; yanlış girilmiş bir kayıt da gerekçesi açıklamaya yazılarak kapatılır.
- Giriş yapmış her aktif kullanıcı ekleyip düzenleyebilir (MVP kararı).

### `OpenIssue` — açık konu (Açık Konular Listesi, akış 6.3)

Talep Sahibi ile ortak kullanılan soru, aksiyon, karar, değişiklik talebi, bağımlılık ve varsayım kayıtları.

| Alan | Tür | Not |
| --- | --- | --- |
| `Id` | int | |
| `ProjectId` | int (FK → Project) | |
| `Code` | string | `AK-001`, proje içinde artan |
| `Type` | `OpenIssueType` | |
| `Title` | string | En fazla 300 karakter |
| `Description` | string? | En fazla 2000 karakter |
| `OwnerUserId` | int? (FK → User) | Sorumlu bir ekip üyesiyse |
| `OwnerName` | string? | Sorumlu sistem dışından biriyse ("Müşteri BT", "Depo Müdürü"); en fazla 200 karakter |
| `DueDate` | DateOnly? | Termin |
| `Status` | `OpenIssueStatus` | |
| `ClosedAt` | DateTime? (UTC) | `Closed` olunca otomatik dolar, yeniden açılınca boşalır |
| `SourceMeetingId` | int? (FK → Meeting) | Kayıt bir tutanaktan aktarıldıysa |
| + `AuditableEntity` alanları | | |

- `OwnerUserId` ve `OwnerName`'den **tam olarak biri** doludur. `OwnerUserId` aktif bir kullanıcı olmalıdır.
- **Gecikme** saklanmaz, hesaplanır: `Status != Closed` ve `DueDate` bugünden önce.
- Kapatılan kayıt yeniden açılabilir (`Open`).

### `Meeting` — toplantı tutanağı

| Alan | Tür | Not |
| --- | --- | --- |
| `Id` | int | |
| `ProjectId` | int (FK → Project) | |
| `Code` | string | `TT-001`, proje içinde artan |
| `Type` | `MeetingType` | |
| `Date` | DateOnly | Toplantı tarihi |
| `Participants` | string | Serbest metin ("Elif Yılmaz, Can Öztürk, Müşteri BT"); en fazla 1000 karakter |
| `Agenda` | string | En fazla 4000 karakter |
| + `AuditableEntity` alanları | | |

- Toplantının **kararları ve aksiyonları tutanakta ayrıca metin olarak tutulmaz**; tutanak kaydedilirken
  `OpenIssue` olarak oluşturulur ve `SourceMeetingId` ile tutanağa bağlanır:
  - her **aksiyon** → `Type = Action`, `Status = Open`, verilen sorumlu ve terminle;
  - her **karar** → `Type = Decision`, `Status = Closed`, sorumlu tutanağı kaydeden kullanıcı, termin toplantı tarihi.
- Tutanak ve aktarılan kayıtlar tek `SaveChanges` ile kaydedilir: ya hepsi ya hiçbiri.
- Tutanak detayında kararlar ve aksiyonlar `SourceMeetingId` üzerinden, güncel durumlarıyla listelenir.
  Türü `Decision` olanlar karar, diğerleri aksiyon sayılır (aksiyonun türü sonradan değiştirilse bile tutanakta kalır).
- Tutanak sonradan düzenlenebilir (tür, tarih, katılımcılar, gündem); kararlar ve aksiyonlar artık
  açık konu olduğu için Açık Konular üzerinden yönetilir.

### `Requirement` — gereksinim / User Story

API ve kararlar: [requirements-documents-api.md](requirements-documents-api.md).

| Alan | Tür | Not |
| --- | --- | --- |
| `Id` | int | |
| `ProjectId` | int (FK → Project) | |
| `Code` | string | `GR-001`, proje içinde artan |
| `Type` | `RequirementType` | |
| `Title` | string | En fazla 300 karakter |
| `Description` | string? | User Story kalıbı buraya yazılır; en fazla 4000 karakter |
| `AcceptanceCriteria` | string? | Kabul ölçütleri; en fazla 4000 karakter |
| `Priority` | `RequirementPriority` | MoSCoW |
| `Status` | `RequirementStatus` | Yeni kayıt `Draft` başlar |
| + `AuditableEntity` alanları | | |

- Silinmez; vazgeçilen gereksinim `Cancelled` yapılır ve yeniden `Draft` yapılabilir.

### `DocumentType` — takip edilen doküman (ana veri)

| Alan | Tür | Not |
| --- | --- | --- |
| `Id` | int | |
| `Code` | string | `PKD`; benzersiz, en fazla 10 karakter |
| `Name` | string | "Proje Kapsam Dokümanı"; en fazla 200 karakter |
| `Description` | string? | En fazla 1000 karakter |
| `ProcessStageId` | int (FK → ProcessStage) | Dokümanın ilk hazırlandığı aşama |
| `Order` | int | Aşama içindeki sıra |

- Seed ile yüklenir (`document-types.json`), kullanıcı tarafından değiştirilmez, loglanmaz.

### `ProjectDocument` — projedeki doküman durumu

| Alan | Tür | Not |
| --- | --- | --- |
| `Id` | int | |
| `ProjectId` | int (FK → Project) | |
| `DocumentTypeId` | int (FK → DocumentType) | `(ProjectId, DocumentTypeId)` benzersiz |
| `Status` | `DocumentStatus` | |
| `Note` | string? | En fazla 1000 karakter |
| `Link` | string? | Dokümanın bulunduğu adres (SharePoint vb.); en fazla 1000 karakter |
| + `AuditableEntity` alanları | | |

- Kendi `Code`'u yoktur; kullanıcı dokümanı türünün koduyla görür.
- Kayıt, dokümanın durumu projede ilk kez kaydedildiğinde oluşur. Kaydı olmayan doküman `NotStarted` sayılır.
- `IAudited` ve `IProjectRecord` taşır; durum değişiklikleri denetim kaydına projesiyle yazılır.

## Enum'lar

| Enum | Değerler |
| --- | --- |
| `UserRole` | `BusinessAnalyst` (İş Analisti), `SoftwareDepartmentManager` (Yazılım Departman Yöneticisi), `ProjectManager` (Proje Yöneticisi), `SoftwareDeveloper` (Yazılım Geliştirici), `TestSpecialist` (Test Uzmanı), `SalesRepresentative` (Satış Temsilcisi) |
| `RequestSource` | `Internal` (İç), `External` (Dış) |
| `RequestChannel` | `Written` (Yazılı), `Verbal` (Sözlü) |
| `RequestType` | `NewProject` (Yeni yazılım projesi), `AdditionalModule` (Devam eden projeye ek modül), `CompanyProduct` (Şirket ürünü), `Support` (Destek / ticket) |
| `ProjectPriority` | `Low`, `Medium`, `High` |
| `ProjectStatus` | `Active` (Aktif), `Closed` (Kapandı), `Rejected` (Reddedildi), `Lost` (Kaybedildi), `Redirected` (Yönlendirildi) |
| `StepAction` | `Completed` (Tamamlandı), `Yes` (Evet), `No` (Hayır), `Rejected` (Yönetici kararıyla reddedildi) |
| `AuditAction` | `Created` (Oluşturuldu), `Updated` (Güncellendi), `Deleted` (Silindi) |
| `OpenIssueType` | `Question` (Soru), `Action` (Aksiyon), `Decision` (Karar), `ChangeRequest` (Değişiklik Talebi), `Dependency` (Bağımlılık), `Assumption` (Varsayım) |
| `OpenIssueStatus` | `Open` (Açık), `InProgress` (Devam ediyor), `Closed` (Kapandı) |
| `MeetingType` | `RequestInterview` (Talep Görüşmesi), `AnalysisSession` (Analiz Oturumu), `KickOff` (Proje Başlangıç / Kick-off), `Review` (Gözden Geçirme), `Internal` (Departman İçi), `CustomerVisit` (Müşteri Ziyareti), `Handover` (Devir Görüşmesi), `ProjectEvaluation` (Proje Değerlendirme) |
| `RequirementType` | `Business` (İş gereksinimi), `Functional` (Fonksiyonel), `NonFunctional` (Fonksiyonel olmayan) |
| `RequirementPriority` | `MustHave` (Olmazsa olmaz), `ShouldHave` (Olmalı), `CouldHave` (Olabilir), `WontHave` (Bu sürümde yok) |
| `RequirementStatus` | `Draft` (Taslak), `InReview` (İncelemede), `Approved` (Onaylandı), `Cancelled` (İptal edildi) |
| `DocumentStatus` | `NotStarted` (Başlanmadı), `InProgress` (Hazırlanıyor), `InReview` (İncelemede), `Approved` (Onaylandı), `NotRequired` (Gerekmiyor) |

## Süreç ilerleme kuralları

| İşlem | Sonuç |
| --- | --- |
| Yeni talep açılır (`Projects`) | `Status` = `Active`, `CurrentStepId` = akışın ilk adımı (`Order` en küçük, 1.1); `StepRecord` yazılmaz. 1.1'in metni talep kaydının kendisidir; tamamlanınca 1.2'ye geçilir |
| Karar olmayan adım tamamlanır | `StepRecord(Completed)`; `CurrentStepId` sonraki adıma geçer |
| Karar adımında "Evet" | `StepRecord(Yes)`; `CurrentStepId` sonraki adıma geçer |
| "Hayır" + `NoOutcome` dolu | `StepRecord(No)`; `Status` = `NoOutcome`, `CurrentStepId` = boş |
| "Hayır" + dönüş seçeneği var | Kullanıcı seçeneği seçer; `StepRecord(No, JumpedToStepId)`; `CurrentStepId` = seçilen adım |
| "Hayır" + sonuç da dönüş de yok | `StepRecord(No)`; `CurrentStepId` değişmez |
| Son adım (13.5) tamamlanır | `Status` = `Closed`, `CurrentStepId` = boş |
| Onay adımı | İşlemi yalnızca `ApproverRole` rolündeki kullanıcı yapabilir |
| Talebi reddet (herhangi bir adımda) | Yalnızca Yazılım Departman Yöneticisi; gerekçe zorunlu. `StepRecord(Rejected)` mevcut adıma yazılır; `Status` = `Rejected`, `CurrentStepId` = boş |

"Talebi reddet" işlemi, prototipte 2.2'nin "Hayır" açıklamasında geçen 2.3 kuralını karşılar
("Bir aydır cevap alınamıyorsa talep Yazılım Departman Yöneticisi kararıyla reddedilir").
2.3 ayrı bir adım olarak tanımlanmaz.

## Alınan kararlar

1. Kullanıcı başına tek rol (enum). İleride gerekirse rol tablosuna çevrilir.
2. Talep sahibi bir `User` değildir; `Project.RequesterName` metin alanında tutulur.
3. Mevcut adım `Project.CurrentStepId` alanında tutulur; sorgular kolaylaşır.
4. Müşteri ayrı bir tablo değildir; `Project.CustomerName` metin alanında tutulur.
5. Bir karar adımının birden fazla dönüş seçeneği olabilir; seçenekler `ProcessStepJump` tablosunda tutulur.
6. 2.3 bir adım değildir; yöneticinin her adımda kullanabileceği "Talebi reddet" işlemiyle karşılanır.
7. `ApproverRole` dolu olan adımı yalnızca o rol kapatabilir (5.1: Satış hazırlar, yönetici onaylar).
8. Denetim kaydı alan bazında eski/yeni değer tutar ve proje bazında listelenebilir (`AuditLog.ProjectId`); `StepRecord` loglanmaz.
9. Açık konunun sorumlusu bir kullanıcı (`OwnerUserId`) **ya da** sistem dışından biri (`OwnerName`) olabilir.
10. Proje kayıtlarının kodu proje içinde artar (`AK-001`, `TT-001`).
11. Proje kayıtları yalnızca aktif projede eklenir ve değiştirilir; silinmez.
12. Tutanaktaki kararlar ve aksiyonlar ayrı metin olarak değil, tutanağa bağlı açık konu olarak tutulur.
13. Gereksinim ve User Story tek kayıttır (`Requirement`); User Story açıklamaya yazılır.
14. Takip edilen dokümanlar seviyeye bağlı değildir; projede gerekmeyen doküman `NotRequired` işaretlenir.

## Seed verisi

Seed, EF Core'un `HasData` yöntemiyle değil JSON dosyalarıyla yapılır. Uygulama açılırken
tablo boşsa bir seeder sınıfı dosyayı okuyup tabloya yazar. Böylece metin düzeltmeleri migration gerektirmez.

**`backend/src/Isdlc.Infrastructure/Seed/process-flow.json`** (hazır)
- `stages`: 13 aşama (`order`, `title`, `shortTitle`).
- `steps`: 75 adım (`code`, `stageOrder`, `order`, `text`, `isDecision`, `noBranchText`,
  `responsibleRole`, `approverRole`, `noOutcome`, `noJumps`).
- Adımlar birbirine ve aşamalara `Id` ile değil `code` ve `stageOrder` ile bağlanır;
  seeder bunları kaydederken `Id`'lere çevirir. Enum değerleri metin olarak yazılır.

Sorumlu roller prototipin kuralına göre atanmıştır: Aşama 8 Yazılım Geliştirici; 9.1–9.5 Test Uzmanı,
9.6–9.9 İş Analisti; Aşama 10–11 Proje Yöneticisi; 5.1 Satış Temsilcisi; diğer adımlar İş Analisti.
Ekip gerekirse dosyada adım adım değiştirebilir.

**`backend/src/Isdlc.Infrastructure/Seed/document-types.json`** (hazır)
- `documentTypes`: takip edilen 18 doküman (`code`, `name`, `description`, `stageOrder`, `order`).
- Aşamaya `stageOrder` ile bağlanır. Liste [requirements-documents-api.md](requirements-documents-api.md) → "Doküman listesi".
- Tablo boşsa yüklenir; listeyi sonradan değiştirmek için tabloyu boşaltmak (veritabanını sıfırlamak) gerekir.

**`backend/src/Isdlc.Infrastructure/Seed/users.json`** (hazır)
- Prototipteki 7 kişi; her rolden en az bir kullanıcı (`fullName`, `email`, `role`).
- Dosyada parola yoktur. Test parolasını `Identity` modülü belirler ve yalnızca geliştirme ortamında
  yapılandırmadan (`.env` / `appsettings.Development.json`) okur; depoya gerçek parola yazılmaz.
- `Identity` bitene kadar API'nin kullandığı sabit kullanıcı: `elif.yilmaz@isdlc.local` (İş Analisti;
  adımların çoğu bu role ait).

**`backend/src/Isdlc.Infrastructure/Seed/sample-projects.json`** (hazır, yalnızca geliştirme ortamı)
- 9 örnek proje; bütün durumlar temsil edilir: 5 `Active` (1.1, 2.2, 4.5, 7.4, 9.6 adımlarında),
  1 `Closed`, 1 `Lost` (5.3), 1 `Rejected` (3.2), 1 `Redirected` (1.2).
  İlk 7'si prototipten, `ISDLC-2026-012` ve `-013` eksik durumları göstermek için eklenmiştir.
- Kullanıcılara `Id` ile değil e-posta ile bağlanır (`businessAnalystEmail`, `projectManagerEmail`).
- Aktif projede `currentStepCode`, kapanmış projede `closedAtStepCode` bulunur.
- Seviye 2.4'ten, öncelik 3.2'den sonraki projelerde doludur.

**Örnek projelerin süreç geçmişi** JSON'da tek tek yazılmaz; seeder şu kurala göre `StepRecord` üretir:
- Mevcut (veya kapanış) adımından önceki her adım için bir kayıt: karar adımıysa `Yes`, değilse `Completed`.
- Kapanmış projelerde kapanış adımı için de bir kayıt: `Closed` ise `Completed` (13.5), diğerlerinde `No`.
- `PerformedById`: adımın `ApproverRole`'ü varsa o rolden, yoksa `ResponsibleRole`'den seed'deki ilk kullanıcı.
- `PerformedAt`: proje `StartDate`'inden başlayarak adım başına bir gün artar, bugünü geçmez.
