# Veri Modeli

Faz 0'ın temel entity'leri (`ProcessStage`, `ProcessStep`, `User`, `Project`, `StepRecord`), denetim kaydı
ve Faz 2'nin proje kayıtları (`OpenIssue`, `Meeting`, `Requirement`, `ProjectDocument`, `Risk`, `ProjectAssessment`). Diğer modüllerin entity'leri kendi fazlarında eklenir.
Kaynak: "ISDLC Analiz Merkezi" prototipi.
Durum: temel entity'ler uygulandı; `OpenIssue` ve `Meeting` **uygulandı, ekip onayı bekliyor**; `Risk` **uygulandı**; `ProjectAssessment` **uygulandı**; `Notification` (Faz 3) **uygulandı**; soru bankası (Faz 4 · B1) **taslak, ekip onayı bekliyor**; tutanak ve ziyaret alanları (Faz 4 · E4) **taslak, ekip onayı bekliyor.**

İsimlendirme AGENTS.md'ye uyar: entity tekil, tablo çoğul; `?` alanın boş bırakılabileceğini (nullable) gösterir.

## Genel bakış

```
ProcessFlow  1 ──── * ProcessStage        (Faz 4: her aşama bir akışa aittir)
ProcessFlow  1 ──── * Project             (Faz 4: kaydın izlediği akış)
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
Meeting      ── Participants: MeetingParticipant[] (Faz 4 · E4: ad ve rol, JSON kolon)
User         1 ──── * OpenIssue           (OwnerUserId: sorumlu ekip üyesi)
Project      1 ──── * Requirement
Project      1 ──── * ProjectDocument * ── 1 DocumentType
ProcessStage 1 ──── * DocumentType        (dokümanın ilk hazırlandığı aşama)
Project      1 ──── * Risk
Project      1 ──── 0..1 ProjectAssessment (seviye cevapları ve kabul puanları)
User         1 ──── * Notification        (UserId: alıcı)
Project      1 ──── * Notification        (bildirimin ilgili olduğu proje)
User         1 ──── * Risk                (OwnerUserId: sorumlu ekip üyesi)
```

- **Ana veri:** `ProcessFlow`, `ProcessStage`, `ProcessStep`, `ProcessStepJump`, `DocumentType`. Seed ile yüklenir, kullanıcı tarafından değiştirilmez.
- **İşlem verisi:** `User`, `Project`, `StepRecord`, `OpenIssue`, `Meeting`, `Requirement`, `ProjectDocument`, `Risk`, `ProjectAssessment`, `Notification`. Uygulama kullanıldıkça oluşur.

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

### `ProcessFlow` — iş akışı (Faz 4)

Kurallar ve akış listesi: [process-api.md → Akışlar](process-api.md#akışlar-faz-4).

| Alan | Tür | Not |
| --- | --- | --- |
| `Id` | int | |
| `Code` | string | `new-software-project`, `add-on`, `process-improvement`, `resource-planning`, `billing`; benzersiz, en fazla 50 |
| `Name` | string | "Yeni Yazılım Projesi"; en fazla 100 |
| `Kind` | `ProcessFlowKind` | Kaydın listelendiği menü: `Request` (Talepler / Projeler), `Improvement` (Süreç İyileştirme), `ResourcePlanning` (Kaynak Planlama), `Billing` (Faturalama) |
| `ProjectStageOrder` | int? | Kaydın proje sayılmaya başladığı bölüm; boşsa kayıt hiç projeye dönüşmez |

### `ProcessStage` — akışın aşamaları (Faz 4'te "bölüm")

| Alan | Tür | Not |
| --- | --- | --- |
| `Id` | int | |
| `ProcessFlowId` | int (FK → ProcessFlow) | Faz 4 |
| `Order` | int | Akış içinde 1'den başlar; (`ProcessFlowId`, `Order`) benzersiz. Eski akışta 1–13 |
| `Title` | string | "Talebin Kayda Alınması" |
| `ShortTitle` | string | "Talep Kaydı" (menü ve listelerde) |

### `ProcessStep` — aşamalardaki adımlar

| Alan | Tür | Not |
| --- | --- | --- |
| `Id` | int | |
| `ProcessStageId` | int (FK → ProcessStage) | |
| `ProcessFlowId` | int (FK → ProcessFlow) | Faz 4. Aşamanın akışıyla aynıdır; benzersizlik ve "sonraki adım" sorgusu için adımda da tutulur |
| `Code` | string | Eski akışta "4.7", yeni akışlarda adım numarası ("12"). (`ProcessFlowId`, `Code`) benzersiz |
| `Order` | int | Akış içindeki sıra; (`ProcessFlowId`, `Order`) benzersiz. Faz 4'te sonraki adım `Order + 1` değil, koşulları sağlanan ilk büyük `Order`'dır |
| `RequiresExternalRequest` | bool | Faz 4. Adım yalnızca dış talepte yapılır |
| `RequiresOffer` | bool | Faz 4. Adım yalnızca `Project.RequiresOffer` ise yapılır |
| `MinLevel` | int? | Faz 4. Adım yalnızca `Project.Level ≥ MinLevel` ise (veya seviye henüz belirlenmediyse) yapılır |
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

Bir karar adımının birden fazla dönüş seçeneği olabilir; "Hayır" cevabı veren kullanıcı seçeneklerden birini seçer.
Güncel akışta (ISDLC v12.0, Ekim 2026) her karar adımının tek dönüş seçeneği var (ör. 4.7 → 4.1, 5.4 → 4.9);
yapı birden fazlasına izin verir.

| Alan | Tür | Not |
| --- | --- | --- |
| `Id` | int | |
| `ProcessStepId` | int (FK → ProcessStep) | Karar adımı |
| `TargetStepId` | int (FK → ProcessStep) | Dönülecek adım |
| `Label` | string | Seçenek metni: "Akış 4.9 adımına dön" |

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
| `Code` | string | Proje No: düz sıra numarası, metin olarak ("14"); yeni yazılım projesi akışında en büyük numara + 1, otomatik verilir, yıl değişince sıfırlanmaz. Faz 4 (D1) öncesindeki "ISDLC-2026-014" kodları migration ile çevrildi |
| `Name` | string | |
| `Description` | string | |
| `Source` | `RequestSource` | |
| `RequestChannel` | `RequestChannel` | |
| `RequestType` | `RequestType` | |
| `CustomerName` | string | Talep sahibi: müşteri firma veya iç birim adı (MVP'de ayrı tablo yok) |
| `RequesterName` | string | Talebi ileten kişi veya unvan; sisteme giriş yapan bir kullanıcı değildir |
| `RequesterContact` | string? | Faz 4. Talebi iletenin e-postası ya da telefonu; serbest metin, en fazla 200 |
| `Notes` | string? | Faz 4. Talep kaydındaki notlar, en fazla 2000 |
| `RequiresNda` | bool | Görüşme öncesi gizlilik sözleşmesi gerekiyor mu |
| `BusinessAnalystId` | int? (FK → User) | |
| `ProjectManagerId` | int? (FK → User) | |
| `Level` | int? | 1–3; seviye belirlenene kadar boş. Yalnızca `Assessment` değiştirir ("Seviyeyi uygula") |
| `Priority` | `ProjectPriority`? | Proje kabul edilirken verilir |
| `Status` | `ProjectStatus` | |
| `ProcessFlowId` | int (FK → ProcessFlow) | Faz 4. Kaydın izlediği akış; açılırken verilir, sonra değişmez |
| `RequiresOffer` | bool | Faz 4. Talep kaydındaki "teklif hazırlanacak mı"; teklif adımlarının yapılıp yapılmayacağını belirler. `Source` ile birlikte, kayıt akışın bunlara bağlı ilk adımına gelince değiştirilemez (projects-api.md, D1-3) |
| `CurrentStepId` | int? (FK → ProcessStep) | Proje aktif değilse boş. Faz 4: her zaman kaydın akışındaki bir adım |
| `RequestDate` | DateOnly | Talep tarihi (Faz 4'ten önce `StartDate`). Talebi kayda alan kişi `CreatedById`'dir |
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
- Loglanmayan: ana veri (`ProcessStage`, `ProcessStep`, `ProcessStepJump`), `StepRecord` (kendisi zaten geçmiş tablosu), `User`, `Notification`.
- `Changes` içinde yer almayan alanlar: `Id`, `PasswordHash`, `RowVersion` ve `AuditableEntity` alanları (bilgileri log satırında var).
- Değeri gerçekten değişmeyen alan yazılmaz; hiçbir alan değişmediyse log satırı oluşmaz.
- Alan adları C# adlarıdır (`Status`); Türkçe karşılıklarını Audit ekranı gösterir ([audit-api.md](audit-api.md)).
- Kayıt ve logu aynı transaction'da yazılır: log yazılamazsa değişiklik de geri alınır.

## Proje kayıtları (Faz 2)

Bir projeye bağlı kayıtlar (`OpenIssue`, `Meeting`, `Requirement`, `Risk`…) şu ortak kurallara uyar:

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
| `Participants` | string | Faz 4 · E4 öncesi serbest metin (en fazla 1000 karakter). *Faz 4 · E4: `MeetingParticipant` listesi, JSON olarak (aşağıda)* |
| `Agenda` | string | En fazla 4000 karakter. Ziyarette "Ana gündem" |
| `RecordedById` | int (FK → User) | *Faz 4 · E4.* Tutanağı yazan; mevcut kayıtlarda oluşturan kişi |
| `Summary` | string? | *Faz 4 · E4.* Toplantı özeti / notlar; en fazla 4000 karakter |
| `CompletedStages` | string? | *Faz 4 · E4.* Tamamlanan aşamalar; en fazla 2000 karakter. Formda süreçten önerilir, toplantı anındaki hâliyle saklanır |
| `OngoingStages` | string? | *Faz 4 · E4.* Devam eden aşamalar; en fazla 2000 karakter |
| `NextSteps` | string? | *Faz 4 · E4.* Sonraki adımlar; en fazla 2000 karakter |
| `ReturnDate` | DateOnly? | *Faz 4 · E4.* Yalnızca `CustomerVisit`: dönüş tarihi, `Date` (gidiş) ile aynı ya da sonra; ziyarette zorunlu |
| `PlannedActivities` | string? | *Faz 4 · E4.* Yalnızca `CustomerVisit`; en fazla 4000 karakter |
| `CompletedActivities` | string? | *Faz 4 · E4.* Yalnızca `CustomerVisit`; en fazla 4000 karakter |
| + `AuditableEntity` alanları | | |

### `MeetingParticipant` — tutanağın katılımcısı (Faz 4 · E4, taslak)

Ayrı bir tablo değildir: `Meeting.Participants` kolonunda JSON listesi olarak tutulur (`[{ "name": "...", "role": "..." }]`).
Böylece tutanak güncellenirken satır silinmez ve değişiklik tutanağın denetim kaydında tek alan olarak görünür.

| Alan | Tür | Not |
| --- | --- | --- |
| `Name` | string | En fazla 100 karakter. Sistem kullanıcısına bağlanmaz (müşteri tarafı da yazılır) |
| `Role` | string? | Rol / birim / kurum ("Depo Müdürü (Demo Lojistik)"); en fazla 100 karakter |

- Tutanak başına 1–30 katılımcı; sırası korunur.
- Migration, eski `Participants` metnini virgülden bölüp sırasıyla listeye çevirir (rol boş).
- Ziyaret tutanağı ayrı bir entity değildir; `Type = CustomerVisit` olan `Meeting`'dir. Sözleşme:
  [open-issues-meetings-api.md](open-issues-meetings-api.md) → "Faz 4 · E4".

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

### `Risk` — proje riski (Risk Analiz Formu, akış 4.8)

API sözleşmesi: [risks-api.md](risks-api.md).

| Alan | Tür | Not |
| --- | --- | --- |
| `Id` | int | |
| `ProjectId` | int (FK → Project) | |
| `Code` | string | `RSK-001`, proje içinde artan |
| `Title` | string | Risk tanımı; en fazla 300 karakter |
| `Description` | string? | En fazla 2000 karakter |
| `Category` | `RiskCategory` | |
| `Probability` | int | Olasılık, 1–5 |
| `Impact` | int | Etki, 1–5 |
| `Mitigation` | string? | Önleyici aksiyon; en fazla 2000 karakter |
| `OwnerUserId` | int (FK → User) | Sorumlu ekip üyesi; aktif bir kullanıcı olmalı |
| `IsSharedWithRequester` | bool | Talep Sahibi ile paylaşılır mı (listede "Paylaşılır / Dahili") |
| `Status` | `RiskStatus` | |
| + `AuditableEntity` alanları | | |

- **Skor** saklanmaz, hesaplanır: `Probability × Impact` (1–25).
- **Seviye** (`RiskSeverity`) skordan türetilir: 1–7 `Low`, 8–14 `Medium`, 15–25 `High`. Saklanmaz.
- Veritabanında `Probability` ve `Impact` için 1–5 kontrolü (check constraint) bulunur.
- Kapatılan risk yeniden açılabilir (`Open`).

### `ProjectAssessment` — seviye belirleme ve proje kabul puanlaması (akış 2.4 ve 3.1)

API sözleşmesi, sorular ve ağırlıklar: [assessment-api.md](assessment-api.md).

| Alan | Tür | Not |
| --- | --- | --- |
| `Id` | int | |
| `ProjectId` | int (FK → Project) | Benzersiz: projenin tek değerlendirmesi |
| `Effort` | int? | Seviye sorusu, 1–3; boş = cevaplanmadı |
| `ScreensAndModules` | int? | Seviye sorusu, 1–3 |
| `Integrations` | int? | Seviye sorusu, 1–3 |
| `UserRoles` | int? | Seviye sorusu, 1–3 |
| `DataSensitivity` | int? | Seviye sorusu, 1–3 |
| `HardwareDependency` | int? | Seviye sorusu, 1–3 |
| `StrategicFit` | int? | Kabul ölçütü, 1–5 (%20) |
| `CommercialValue` | int? | Kabul ölçütü, 1–5 (%20) |
| `TechnicalFeasibility` | int? | Kabul ölçütü, 1–5 (%20) |
| `ResourceAndSchedule` | int? | Kabul ölçütü, 1–5 (%15) |
| `CustomerFit` | int? | Kabul ölçütü, 1–5 (%15) |
| `RiskLevel` | int? | Kabul ölçütü, 1–5 (%10); yüksek puan = düşük risk |
| + `AuditableEntity` alanları | | |

- Kendi `Code`'u yoktur; projenin bir parçası olarak görünür. İlk cevap kaydedilince oluşur, silinmez.
- **Puan, önerilen seviye, ağırlıklı ortalama ve öneri saklanmaz**, cevaplardan hesaplanır.
- Uygulanan seviye `Project.Level`'de tutulur; cevaplar değişince kendiliğinden değişmez.
- Veritabanında her seviye sorusu için 1–3, her kabul ölçütü için 1–5 kontrolü (check constraint) bulunur.
- `IAudited` ve `IProjectRecord` taşır; cevap değişiklikleri denetim kaydına projesiyle yazılır.

## Bildirimler (Faz 3)

### `Notification` — uygulama içi bildirim

API sözleşmesi ve gönderim kuralları: [notifications-api.md](notifications-api.md).

| Alan | Tür | Not |
| --- | --- | --- |
| `Id` | long | |
| `UserId` | int (FK → User) | Alıcı |
| `ProjectId` | int (FK → Project) | Bildirimin ilgili olduğu proje (tıklayınca açılır) |
| `Message` | string | Gönderen modülün Türkçe metni; en fazla 500 karakter |
| `CreatedAt` | DateTime (UTC) | |
| `ReadAt` | DateTime? (UTC) | Okunduysa; ilk okunma zamanı değişmez |

- Proje verisi değildir: `AuditableEntity`'den türemez, denetim kaydına yazılmaz.
- Rol bildirimi gönderildiği andaki her aktif kullanıcı için ayrı bir satırdır.
- Silinmez. `(UserId, ReadAt)` ve `(UserId, CreatedAt)` üzerinde indeks bulunur (okunmamış sayısı ve liste için).

## Soru bankası (Faz 4 · B1)

Dört şirket formunun (Seviye, Talep Analizi, Risk Analizi, İç Onay) ortak yapısı. API sözleşmesi, formlardan çıkan yapı ve
seed biçimi: [question-bank-api.md](question-bank-api.md).

```
QuestionSet  1 ──── * Question ──── * QuestionOption   (soruya özgü seçenek: seviye)
QuestionSet  1 ──── * QuestionOption                   (sete ait ortak seçenek: diğer üç form)
Project      1 ──── * Questionnaire * ──── 1 QuestionSet   (projede bir set bir kez doldurulur)
Questionnaire 1 ──── * QuestionAnswer * ──── 1 Question
QuestionAnswer * ──── 0..1 QuestionOption
```

**Ana veri** (seed: `Seed/question-sets/*.json`; kullanıcı değiştirmez, denetim kaydına yazılmaz):

| Entity | Alanlar |
| --- | --- |
| `QuestionSet` | `Id`, `Code` (`level`, `request-analysis`, `risk-analysis`, `internal-approval`; benzersiz), `Name`, `SourceDocument`, `Version`, `AnswerMode` (`SharedOptions` / `QuestionOptions`), `Bands` (formlara özgü eşikler, JSON) |
| `Question` | `Id`, `QuestionSetId`, `Key` (kalıcı anahtar, benzersiz), `WorkType` (akış anahtarı), `Subcategory` (`RequestSource?`; boş = bütün alt kategoriler), `No`, `Order`, `Section?`, `Group?`, `Topic?`, `Text` (2000), `Requirement` (`QuestionRequirement?`), `ExpectedInfo?`, `RiskDescription?`, `SuggestedAction?`, `Reference?` (1000), `IsActive` |
| `QuestionOption` | `Id`, `QuestionSetId`, `QuestionId?` (doluysa soruya özgü), `Key` (kalıcı anahtar, benzersiz: ortak seçenekte `{set}/{kod}`, soruya özgüde `{soru anahtarı}/{sıra}`), `Label` (500), `Points?`, `Meaning?` (`OptionMeaning`), `IsScored`, `RequiresNote` (form açıklama bekliyor), `Order` |

**İşlem verisi** (`AuditableEntity`, `IAudited`, `IProjectRecord`):

| Entity | Alanlar |
| --- | --- |
| `Questionnaire` | `Id`, `ProjectId`, `QuestionSetId` (`(ProjectId, QuestionSetId)` benzersiz), `WorkType`, `Subcategory` (`RequestSource`), `Participants?` (1000), `AssessedOn?` (DateOnly), `ApprovedById?` (FK → User), `ApprovedOn?` |
| `QuestionAnswer` | `Id`, `QuestionnaireId`, `ProjectId` (denetim kaydı için formunkinin aynısı), `QuestionId` (`(QuestionnaireId, QuestionId)` benzersiz), `OptionId?`, `Text?` (4000), `Note?` (2000), `Probability?` (1–3), `Severity?` (1–3) |

- Formun sonucu (seviye, uygunluk oranı, risk derecesi) ve özet **saklanmaz**, cevaplardan hesaplanır.
- Dosyadan çıkan soru silinmez, `IsActive = false` olur; cevabı korunur.
- Seed her açılışta dosyalarla eşitlenir (`QuestionSetSeeder`); dosyalar önce doğrulanır, bozuksa uygulama açılmaz.
  Dosya değişmediyse veritabanına hiçbir şey yazılmaz.
- `RequestSource`'a `Affiliated` (İlişkili şirket) eklendi; soruların üçüncü alt kategorisi. Talep formundaki karşılığı D1 ile gelir.
- Veritabanında `Probability` ve `Severity` için 1–3 kontrolü (check constraint) bulunur.

## Enum'lar

| Enum | Değerler |
| --- | --- |
| `UserRole` | `BusinessAnalyst` (İş Analisti), `SoftwareDepartmentManager` (Yazılım Departman Yöneticisi), `ProjectManager` (Proje Yöneticisi), `SoftwareDeveloper` (Yazılım Geliştirici), `TestSpecialist` (Test Uzmanı), `SalesRepresentative` (Satış Temsilcisi) |
| `RequestSource` | `Internal` (Kurum içi), `External` (Dış, müşteri), `Affiliated` (İlişkili şirket) |
| `RequestChannel` | `Email` (E-posta), `Phone` (Telefon). Faz 4 öncesindeki `Written` / `Verbal` migration ile çevrildi |
| `RequestType` | `NewProject` (Yeni yazılım projesi), `AdditionalModule` (Devam eden projeye ek modül), `CompanyProduct` (Şirket ürünü), `Support` (Destek / ticket) |
| `ProjectPriority` | `Low`, `Medium`, `High` |
| `ProjectStatus` | `Active` (Aktif), `Closed` (Kapandı), `Rejected` (Reddedildi), `Lost` (Kaybedildi), `Redirected` (Yönlendirildi) |
| `ProcessFlowKind` | *(Faz 4)* `Request` (Talep / proje), `Improvement` (Süreç iyileştirme), `ResourcePlanning` (Kaynak planlama), `Billing` (Faturalama) |
| `StepAction` | `Completed` (Tamamlandı), `Yes` (Evet), `No` (Hayır), `Rejected` (Yönetici kararıyla reddedildi) |
| `AuditAction` | `Created` (Oluşturuldu), `Updated` (Güncellendi), `Deleted` (Silindi) |
| `OpenIssueType` | `Question` (Soru), `Action` (Aksiyon), `Decision` (Karar), `ChangeRequest` (Değişiklik Talebi), `Dependency` (Bağımlılık), `Assumption` (Varsayım) |
| `OpenIssueStatus` | `Open` (Açık), `InProgress` (Devam ediyor), `Closed` (Kapandı) |
| `RiskCategory` | `Technical` (Teknik), `Scope` (Kapsam), `Stakeholder` (Paydaş), `Schedule` (Takvim), `Cost` (Maliyet), `Data` (Veri), `SecurityPrivacy` (Güvenlik / KVKK), `Supplier` (Tedarikçi) |
| `RiskStatus` | `Open` (Açık), `Monitoring` (İzleniyor), `Closed` (Kapandı) |
| `RiskSeverity` | `Low` (Düşük, skor 1–7), `Medium` (Orta, 8–14), `High` (Yüksek, 15–25). Saklanmaz, yalnızca DTO'da ve filtrede |
| `QuestionRequirement` | `Required` (Zorunlu), `Conditional` (Duruma bağlı) |
| `OptionMeaning` | `Complete`, `Partial`, `Open`, `Negative`, `NotApplicable`; formlardaki karşılıkları: [question-bank-api.md](question-bank-api.md) → Kavramlar |
| `AnswerMode` | `SharedOptions` (seçenekler setin), `QuestionOptions` (her sorunun kendi seçenekleri) |
| `AcceptanceRecommendation` | `Accept` (Kabul önerilir, ≥ 3,50), `Conditional` (Koşullu kabul, 2,50–3,49), `RejectOrPostpone` (Ret / erteleme önerilir, < 2,50). Saklanmaz, yalnızca DTO'da |
| `MeetingType` | `RequestInterview` (Talep Görüşmesi), `AnalysisSession` (Analiz Oturumu), `KickOff` (Proje Başlangıç / Kick-off), `Review` (Gözden Geçirme), `Internal` (Departman İçi), `CustomerVisit` (Müşteri Ziyareti), `Handover` (Devir Görüşmesi), `ProjectEvaluation` (Proje Değerlendirme) |
| `RequirementType` | `Business` (İş gereksinimi), `Functional` (Fonksiyonel), `NonFunctional` (Fonksiyonel olmayan) |
| `RequirementPriority` | `MustHave` (Olmazsa olmaz), `ShouldHave` (Olmalı), `CouldHave` (Olabilir), `WontHave` (Bu sürümde yok) |
| `RequirementStatus` | `Draft` (Taslak), `InReview` (İncelemede), `Approved` (Onaylandı), `Cancelled` (İptal edildi) |
| `DocumentStatus` | `NotStarted` (Başlanmadı), `InProgress` (Hazırlanıyor), `InReview` (İncelemede), `Approved` (Onaylandı), `NotRequired` (Gerekmiyor) |

## Süreç ilerleme kuralları

| İşlem | Sonuç |
| --- | --- |
| Yeni talep açılır (`Projects`) | `Status` = `Active`, `CurrentStepId` = akışın ilk adımı (`Order` en küçük, 1.1); `StepRecord` yazılmaz. 1.1'in metni talep kaydının kendisidir; tamamlanınca 1.2'ye geçilir. Faz 4: kaydın akışında, koşulları sağlanan ilk adım |
| Karar olmayan adım tamamlanır | `StepRecord(Completed)`; `CurrentStepId` sonraki adıma geçer. Faz 4: aynı akışta koşulları sağlanan sonraki adım; atlanan adımlara `StepRecord` yazılmaz |
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
2. Talebi ileten kişi bir `User` değildir; `Project.RequesterName` metin alanında tutulur.
3. Mevcut adım `Project.CurrentStepId` alanında tutulur; sorgular kolaylaşır.
4. Talep sahibi (müşteri firma / birim) ayrı bir tablo değildir; `Project.CustomerName` metin alanında tutulur.
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
15. *(Taslak)* Risk skoru ve seviyesi saklanmaz, olasılık ve etkiden hesaplanır; riskin sorumlusu yalnızca ekip üyesidir.
    Ayrıntılar ve onay bekleyen diğer kararlar: [risks-api.md](risks-api.md) → "Ekip onayı bekleyen kararlar".
16. *(Taslak)* Seviye cevapları ve kabul puanları projenin tek `ProjectAssessment` satırında sütun olarak tutulur;
    seviye yalnızca "Seviyeyi uygula" ile `Project.Level`'e yazılır. Ayrıntılar: [assessment-api.md](assessment-api.md) → "Ekip onayı bekleyen kararlar".
17. *(Taslak)* Bildirimler kişi başına satırdır; gönderen modüller `INotificationService`'i değiştirmeden kullanır.
    Ayrıntılar: [notifications-api.md](notifications-api.md) → "Ekip onayı bekleyen kararlar".
18. *(Faz 4)* Akışlar veri olarak tutulur (`ProcessFlow`); her kayıt tek bir akışı izler. Akışlar şirketin beş akışıdır;
    eski 13 aşamalı akış geçersizdir ve kaldırılır (uygulama canlıda olmadığı için veritabanı yeni akışlarla baştan kurulur).
19. *(Faz 4)* Adımın yapılıp yapılmayacağı üç koşulla belirlenir (dış talep, teklif, en düşük seviye); koşulu sağlanmayan adım atlanır ve kaydı tutulmaz.
20. *(Faz 4)* Süreç iyileştirme önerisi, kaynak planlama haftası ve faturalama ayı ayrı tablo değil, kendi akışını izleyen
    birer `Project` kaydıdır; akışın türü (`ProcessFlowKind`) kaydın hangi menüde listeleneceğini belirler.
21. *(Faz 4, taslak)* Dört şirket formu tek bir soru bankası yapısında tutulur; alt kategori talep kaynağıdır, iş tipi akışın
    anahtarıdır (`ProcessFlow.Code`). Ayrıntılar: [question-bank-api.md](question-bank-api.md) → "Ekip onayı bekleyen kararlar".

## Seed verisi

Seed, EF Core'un `HasData` yöntemiyle değil JSON dosyalarıyla yapılır. Uygulama açılırken
tablo boşsa bir seeder sınıfı dosyayı okuyup tabloya yazar.

**Akışlar her açılışta dosyalarıyla eşitlenir.** Her akış ayrı bir dosyadır: `Seed/flows/<code>.json` (beş dosya; kaynak:
`Ithinka Proje Dokümanları/Akışlar`, 11.11.2026). Veritabanında olmayan bir akış dosyası **eklenir** (içinde henüz kayıt
olmadığı için migration gerekmez). Var olan bir akışta seeder aşamaları sırasına, adımları koduna göre eşleştirir;
başlık, adım metni, "Hayır" açıklaması, roller, kapanış sonucu, koşullar ve dönüş seçenekleri dosyadaki gibi yapılır.
`Id`'ler değişmez; projeler ve süreç geçmişi etkilenmez. Böylece şirket bir akışı güncellediğinde yalnızca dosyası değişir.
Var olan akışta adım eklemek, çıkarmak veya sırasını değiştirmek bu yolla yapılmaz (projelerin bulunduğu adımı kaydırır);
böyle bir fark logda uyarı olarak görünür ve ayrı bir migration gerektirir.

Dosyanın alanları:
- Akış: `code`, `order`, `name`, `kind`, `projectStageOrder`.
- `stages`: 4 bölüm (`order`, `title`, `shortTitle`).
- `steps`: `code`, `stageOrder`, `order`, `text`, `isDecision`, `noBranchText`, `responsibleRole`, `approverRole`,
  `noOutcome`, `noJumps`, `requiresExternalRequest`, `requiresOffer`, `minLevel`.
- Adımlar birbirine ve bölümlere `Id` ile değil `code` ve `stageOrder` ile bağlanır; seeder bunları kaydederken
  `Id`'lere çevirir. Enum değerleri metin olarak yazılır.

**Akış şemasında olmayan, eklenen yorumlar** (ekip gözden geçirmeli):
- Şemada "karar" olarak çizilmeyen ama iki yolu olan adımlar Evet/Hayır kararı yapıldı: yeni proje 4 (uygun değil → reddedildi),
  10 (kapsam değişti → 9), 11 (ret → kaybedildi), 17 (hata → 16); ek geliştirme 8 (Proje Yöneticisi onayı; onay yoksa adımda
  kalınır) ve 10 (kapsam değişti → 9); süreç iyileştirme 2, 4 ve 7; kaynak planlama 7; faturalama 4.
- Bir adımın hem kapanış sonucu hem dönüşü olamadığı için (karar 5) ret / erteleme, dönüşü olan adımlarda yöneticinin
  "Talebi reddet" işlemiyle yapılır. Erteleme durumu D2 ile gelir.
- Sorumlu roller şemadaki açıklamalardan çıkarıldı: analiz adımları İş Analisti; zaman ve kaynak planı, proje başlatma,
  bakım ve kapanış Proje Yöneticisi; teklif ve anlaşma Satış Temsilcisi (teklifi Yazılım Departman Yöneticisi onaylar);
  geliştirme ve canlıya alma Yazılım Geliştirici; test Test Uzmanı.
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
- 8 örnek proje, hepsi yeni yazılım projesi akışında: 5 `Active` (1, 6, 10, 14, 16. adımlarda), 1 `Closed`,
  1 `Lost` (11: teklif kabul edilmedi), 1 `Rejected` (4: müşteri uygun değil).
  15 numaralı proje teklifsiz bir iç taleptir ("doğrudan proje"): 4, 10 ve 11. adımlar onda atlanır.
- Kullanıcılara `Id` ile değil e-posta ile bağlanır (`businessAnalystEmail`, `projectManagerEmail`).
- Aktif projede `currentStepCode`, kapanmış projede `closedAtStepCode` bulunur; `requiresOffer` teklif hazırlanıp hazırlanmayacağıdır.
- Seviye 5. adımdan sonraki projelerde doludur.

**Örnek projelerin süreç geçmişi** JSON'da tek tek yazılmaz; seeder şu kurala göre `StepRecord` üretir:
- Mevcut (veya kapanış) adımından önceki her adım için bir kayıt: karar adımıysa `Yes`, değilse `Completed`. Projede atlanan adımlara kayıt yazılmaz.
- Kapanmış projelerde kapanış adımı için de bir kayıt: `Closed` ise `Completed` (19), diğerlerinde `No`.
- `PerformedById`: adımın `ApproverRole`'ü varsa o rolden, yoksa `ResponsibleRole`'den seed'deki ilk kullanıcı.
- `PerformedAt`: projenin `RequestDate`'inden başlayarak adım başına bir gün artar, bugünü geçmez.
