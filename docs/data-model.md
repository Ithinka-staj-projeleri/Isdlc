# Veri Modeli — Temel Entity'ler (Faz 0)

Bu doküman Faz 1'in ihtiyaç duyduğu 5 temel entity'yi tanımlar: `ProcessStage`, `ProcessStep`,
`User`, `Project`, `StepRecord`. Diğer modüllerin entity'leri kendi fazlarında eklenir.
Kaynak: "ISDLC Analiz Merkezi" prototipi. Durum: **taslak — ekip onayı bekliyor.**

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
```

- **Ana veri:** `ProcessStage`, `ProcessStep`, `ProcessStepJump`. Seed ile yüklenir, kullanıcı tarafından değiştirilmez.
- **İşlem verisi:** `User`, `Project`, `StepRecord`. Uygulama kullanıldıkça oluşur.

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

## Süreç ilerleme kuralları

| İşlem | Sonuç |
| --- | --- |
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
