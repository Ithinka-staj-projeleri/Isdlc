# Şirkete Göre Ayarlanabilirlik — Sözleşme

Faz 7'nin uygulama ayrıntıları. Bu belgede şunlar yazılıdır: veri modeli, bugünkü verinin taşınması, kurallar, uç noktalar,
hata mesajları ve testler. Konular: yetkiler, adım ön koşulları, atlama kuralları, şirkete özel alanlar, akış düzenleme,
yapay zekâ modeli ve kullanımı. Plan, kararlar ve iş dağılımı: [phase-7-plan.md](phase-7-plan.md).
Durum: **onaylandı** (26 Kasım 2026, #178). Sorumlu: Erva (A1). Uygulama: A2–A9.
A2 (yetkiler, backend), A4 (adım ön koşulları), A5 (atlama kuralları), A6 (şirkete özel alanlar) ve A9 (yapay zekâ modeli ve kullanımı) uygulandı.

Kurallar uygulandıkça AGENTS.md'ye taşınır (Bölüm 10). Bölüm numaraları plandaki kararlara göndermede kullanılır:
"karar 8", plan Bölüm 3'teki 8 numaralı karardır.

## 1. Kavramlar

| Türkçe | Kodda | Açıklama |
| --- | --- | --- |
| Yetki | `Permission` (enum) | Akıştaki bir işi yapabilmek. Platformda sabit altı değer; her şirkette aynı (Bölüm 2) |
| Kullanıcının yetkileri | `UserPermission` | Kullanıcı başına sıfır ya da daha fazla yetki |
| Hazır yetki seti | `PermissionPresets` | Bugünkü unvanların karşılığı; formda kutucukları tek tıkla doldurur, kaydedilmez |
| Platform Yöneticisi | `User.IsPlatformAdmin` | Faz 6'daki `UserRole.PlatformAdmin`'in yerini alır; şirketi ve yetkisi yoktur |
| Şirket Yöneticisi | `User.IsTenantAdmin` | Faz 6'daki gibi; bir yetki değil, işarettir |
| Adımın sorumlusu / onaylayanı | `ProcessStep.ResponsiblePermission`, `ApproverPermission` | Unvan yerine yetki |
| Ön koşul | `ProcessStep.Prerequisites` | Adım tamamlanmadan önce projede olması gereken işler (Bölüm 4) |
| Atlama kuralı | `ProcessStep.Conditions` | Adımın bu projede yapılıp yapılmayacağı; bugünkü üç alanın yerini alır (Bölüm 5) |
| Şirkete özel alan | `CustomField`, `Project.CustomValues` | Talep Kayıt Formu'na şirketin eklediği alan ve projedeki değeri (Bölüm 6) |
| Akışın kullanımı | `ProcessFlow.UsedFor` | Hangi kayıtların bu akışla başladığı; `ProcessFlowCodes` sabitlerinin yerini alır (Bölüm 7) |
| Yapay zekâ kullanımı | `AiUsage` | Yapay zekâ servisine yapılan her çağrının kaydı (Bölüm 8) |

## 2. Yetkiler (A2, A3)

### Yetki listesi

| Değer | Ekranda | Neyi açar |
| --- | --- | --- |
| `Analysis` | Analiz adımları | Sorumlusu bu yetki olan adımlar; **talep açma** (yeni talep akışın ilk adımıdır); projenin iş analisti seçilebilme; e-postadan talep taslağı |
| `ProjectManagement` | Proje yönetimi adımları | Sorumlusu bu yetki olan adımlar; projenin proje yöneticisi seçilebilme |
| `Approval` | Onay ve karar | Onaylayanı bu yetki olan adımlar; talebi reddetme, erteleme, değerlendirmeye geri alma |
| `Sales` | Teklif ve anlaşma | Sorumlusu bu yetki olan adımlar |
| `Development` | Geliştirme | Sorumlusu bu yetki olan adımlar |
| `Testing` | Test | Sorumlusu bu yetki olan adımlar |

- Enum veritabanında metin olarak tutulur (AGENTS.md Bölüm 8). Ekrandaki adlar tek yerde: `PermissionNames.ToDisplayName()`
  (bugünkü `UserRoleNames`'in yerini alır). Frontend'de karşılığı `shared/models/permission.ts`.
- **Akış dışındaki işler yetki istemez** (karar 2a). Şirketin her aktif kullanıcısı risk, açık konu, tutanak, gereksinim,
  doküman, seviye ve uygunluk formları, panel, arama ve yapay zekâ yardımcılarını (e-postadan taslak hariç) kullanır.
  Bunlarda bugün unvana bakan bir kural varsa kaldırılır.

### Veri modeli

**`User`** (değişen alanlar)

| Alan | Tür | Kural |
| --- | --- | --- |
| `Role` | — | **Kaldırılır** |
| `IsPlatformAdmin` | bool | Yeni. Varsayılan `false` |
| `IsTenantAdmin` | bool | Değişmez |
| `Permissions` | `ICollection<UserPermission>` | Yeni |

Kısıt (bugünkü `CK_Users_TenantByRole`'ün yerini alır):
`CHECK ((IsPlatformAdmin = 1 AND TenantId IS NULL AND IsTenantAdmin = 0) OR (IsPlatformAdmin = 0 AND TenantId IS NOT NULL))`.
Platform Yöneticisinin yetkisi olamaz (interceptor reddeder, 500 ve log; Faz 6'daki gibi).

**`UserPermission`** — kullanıcının yetkisi (yeni tablo `UserPermissions`)

| Alan | Tür | Kural |
| --- | --- | --- |
| `Id` | int | |
| `TenantId` | int | `ITenantOwned`; kullanıcının şirketi |
| `UserId` | int (FK → User) | |
| `Permission` | `Permission` | Benzersiz: `(UserId, Permission)` |

Denetim kaydına yazılmaz (`User` gibi).

**`ProcessStep`** (değişen alanlar)

| Alan | Tür | Kural |
| --- | --- | --- |
| `ResponsibleRole` | — | **Kaldırılır** → `ResponsiblePermission` (`Permission`, zorunlu) |
| `ApproverRole` | — | **Kaldırılır** → `ApproverPermission` (`Permission?`, yalnızca onay adımında) |

Seed dosyalarında `responsibleRole` / `approverRole` alanları `responsiblePermission` / `approverPermission` olur.

### Bugünkü verinin taşınması (tek migration, A2)

| Bugün | Sonra |
| --- | --- |
| `Role = BusinessAnalyst` | `Analysis` |
| `Role = SoftwareDepartmentManager` | `Approval` |
| `Role = ProjectManager` | `ProjectManagement` |
| `Role = SalesRepresentative` | `Sales` |
| `Role = SoftwareDeveloper` | `Development` |
| `Role = TestSpecialist` | `Testing` |
| `Role = PlatformAdmin` | `IsPlatformAdmin = 1`, yetki yok |
| Adımın `ResponsibleRole` / `ApproverRole` | Aynı tabloyla `ResponsiblePermission` / `ApproverPermission` |

Migration bitince her kullanıcının ve her adımın eski rolüne karşılık gelen yetkiyi taşıdığı bir testle doğrulanır.

### Kim ne yapabilir

| Kural | Bugün | Sonra |
| --- | --- | --- |
| Adımda işlem | `ProcessProgress.CanActOnStep`: kullanıcının unvanı = adımın onaylayanı ya da sorumlusu | Kullanıcının yetkileri arasında `ApproverPermission ?? ResponsiblePermission` var |
| Talep açma | İş Analisti | `Analysis` |
| E-postadan talep taslağı | İş Analisti | `Analysis` |
| Projenin iş analisti | İş Analisti unvanlı aktif kullanıcı | `Analysis` yetkili aktif kullanıcı |
| Projenin proje yöneticisi | Proje Yöneticisi unvanlı aktif kullanıcı | `ProjectManagement` yetkili aktif kullanıcı |
| Reddet / ertele / geri al | Yazılım Departman Yöneticisi | `Approval` |
| Ekip iş yükünde bekleyen adım | Unvan eşleşir; İş Analisti ve Proje Yöneticisi adımları projeye atanan kişiye (atanmamışsa herkese) düşer | Yetki eşleşir; `Analysis` ve `ProjectManagement` adımları aynı kuralla projenin analistine ve yöneticisine düşer |
| Panelde "Bana bekleyen adımlar" (`DashboardService`) | Adımın unvanı kullanıcının unvanı; İş Analisti ve Proje Yöneticisi adımlarında yalnızca atandığı (ya da atanmamış) projeler | Adımın yetkisi kullanıcının yetkilerinden biri; aynı atama kuralı. Birden fazla yetkisi olanın listesi birleşimdir |
| Kullanıcılar ekranı, şirket ayarları, akış düzenleme, özel alanlar | Şirket Yöneticisi | Değişmez (`IsTenantAdmin`) |
| Şirketler | Platform Yöneticisi | `IsPlatformAdmin` |

Bugünkü gibi adımda işlemi projeye atanan kişiyle sınırlamayız; aynı yetkiye sahip herkes işlem yapabilir.
Atama yalnızca ekip iş yükü ve "Bana bekleyen adımlar" içindir.

### Giriş ve istek

- JWT'deki `role` claim'i kalkar; `platformAdmin` (`"true"` / `"false"`) eklenir. `tenant`, `tenantAdmin`, `mustChangePassword`
  değişmez.
- Yetkiler **token'a yazılmaz.** Şirket yöneticisi yetkiyi değiştirince bir sonraki istekte geçerli olsun diye servis gerekince
  veritabanından okur. *Uygulandı (A2):* `ICurrentUserService`'e metot eklenmedi; servisler `IApplicationDbContext` üzerindeki
  `LoadAccessAsync(userId)` (yetkiler ve şirket yöneticilerinin adları, `UserAccess`) ve `IsActiveUserWithAsync(userId, yetki)` ile
  okur (`Isdlc.Application/Identity/UserAccess.cs`). Şirket filtresinden geçtikleri için başka şirketin kullanıcısı bulunmaz;
  testlerde `TestUser` değişmeden çalışır. Süreç servisi kullanıcıyı zaten her komutta okuyordu, ayrıca önbellek gerekmedi.
- Giriş cevabının `user` nesnesinde `role` yerine `permissions: ["Analysis", …]` ve `isPlatformAdmin` döner.
- Frontend oturumdaki yetkileri `GET /api/v1/users/me` ile tazeler: sayfa açılınca ve 403 alınca.
  Cevap: `{ id, fullName, email, tenant, isTenantAdmin, isPlatformAdmin, permissions }`.

### Yetkisi olmayan kullanıcı (karar 2b)

Düğmeler gizlenmez; işlem yapılamıyorsa gerekçe yazar. Backend aynı metinle `403` döner.

| Durum | Mesaj |
| --- | --- |
| Adımı yapmak | "Bu adım için «{yetki}» yetkisi gerekir. Şirket yöneticinizden isteyin: {yöneticiler}." |
| Onay adımı | "Bu adımı onaylamak için «Onay ve karar» yetkisi gerekir. Şirket yöneticinizden isteyin: {yöneticiler}." |
| Reddetmek, ertelemek, geri almak | "Talep hakkında karar vermek için «Onay ve karar» yetkisi gerekir. Şirket yöneticinizden isteyin: {yöneticiler}." |
| Talep açmak | "Talep açmak için «Analiz adımları» yetkisi gerekir. Şirket yöneticinizden isteyin: {yöneticiler}." |
| Analist / yönetici olarak seçilen kişide yetki yok | `400` "Seçilen kişinin «Analiz adımları» yetkisi yok." / "Seçilen kişinin «Proje yönetimi adımları» yetkisi yok." |

- `{yöneticiler}`: şirketin aktif Şirket Yöneticilerinin adları, ada göre, virgülle. Hiç yoksa cümle "… Şirket yöneticinizden
  isteyin." diye biter.
- `AvailableActionsDto` (`GET /projects/{id}/process`) `BlockedReason`'ı bu metinle doldurur. Ayrıca yeni bir alan döner:
  `MissingPermission` (`Permission?`). Ekran bu alana bakarak yetki adını vurgular.
- "Yeni talep" düğmesi yetkisi olmayana da görünür; tıklayınca form yerine bu mesaj çıkar. Mesajın kaynağı `GET /users/me`
  ve `GET /api/v1/tenants/current` (yanına `admins: ["Deniz Aydın"]` eklenir).

### Hazır yetki setleri

Bu setler kaydedilmez; yalnızca formdaki kutucukları doldurur. `GET /api/v1/permissions` hem yetki listesini hem setleri döner,
böylece ekranda sabit liste kalmaz:

```jsonc
{
  "permissions": [ { "value": "Analysis", "name": "Analiz adımları" }, … ],
  "presets": [
    { "name": "İş Analisti", "permissions": ["Analysis"] },
    { "name": "Yazılım Departman Yöneticisi", "permissions": ["Approval"] },
    { "name": "Proje Yöneticisi", "permissions": ["ProjectManagement"] },
    { "name": "Satış Temsilcisi", "permissions": ["Sales"] },
    { "name": "Yazılım Geliştirici", "permissions": ["Development"] },
    { "name": "Test Uzmanı", "permissions": ["Testing"] }
  ]
}
```

### A2'de netleşenler

Sözleşmede yazmayan ama unvan kullanan yerler; A2 böyle uyguladı:

| Yer | Bugün | Sonra |
| --- | --- | --- |
| Onay bildirimi ("… onayınızı bekliyor") | `NotifyRoleAsync(unvan)` | `INotificationService.NotifyPermissionAsync(yetki)`: adımın onaylayan yetkisine sahip aktif kullanıcılar |
| `Seed/users.json` | `"role": "BusinessAnalyst"` | `"permissions": ["Analysis"]` (eşleme tablosu) |
| `GET /users` cevabı (`UserLookupDto`) | `role` | `permissions: Permission[]` |
| `TenantInterceptor`, `PlatformAdminSeeder` | `Role == PlatformAdmin` | `IsPlatformAdmin` |
| Ertelenmiş talepte yetkisiz kişiye mesaj | "… Yazılım Departman Yöneticisi değerlendirmeye geri alınca devam eder." | "Talep ertelendi; «Onay ve karar» yetkisi olan biri değerlendirmeye geri alınca devam eder." (`MissingPermission = Approval`) |
| Analist / yönetici seçimi, kişi pasif | "… aktif bir İş Analisti değil." | Yetkisi olmayanla aynı: "Seçilen kişinin «…» yetkisi yok." |
| Yetki alma | — | Kaldırılan yetkinin `UserPermissions` satırı silinir: kayıt değil ayardır, geçmiş ona bağlı değildir |
| `PUT /users/{id}` içinde `permissions` yok | — | Yetkiler değişmez (`null`); boş liste (`[]`) hepsini kaldırır. Yetki kutucukları olmayan eski form yetkileri silmesin diye |
| JWT | `role` | `platformAdmin`. Eski `role`'lü token'la Platform Yöneticisi yeniden giriş yapana kadar yönetici sayılmaz |

### Uç noktalar (değişenler)

| İstek | Değişiklik |
| --- | --- |
| `POST /tenants` | `admin` nesnesinden `role` kalkar: `{ fullName, email, temporaryPassword }`. Yönetici yetkisiz açılır |
| `GET /users/manage` · `POST /users` · `PUT /users/{id}` | `role` yerine `permissions: Permission[]` (boş olabilir). `PlatformAdmin` verilemez; bilinmeyen değer `400` |
| `GET /users?role=` | `GET /users?permission=` olur (form seçicileri: analist `Analysis`, yönetici `ProjectManagement`) |
| `GET /users/me` | Yeni (yukarıda) |
| `GET /permissions` | Yeni (yukarıda); giriş yapmış herkes |
| `GET /tenants/current` | `admins` eklenir |

Şirket Yöneticisi kendine de yetki verebilir. Son aktif yöneticiyi kaldırma kuralı Faz 6'daki gibi kalır.

## 3. Ekranlar (A3)

Uygulandı. Yetki adları ve yardımcılar `shared/models/permission.ts`'te; `user-role.ts` ve `role.guard.ts` kaldırıldı. Oturum,
sayfa açılınca (`LayoutComponent`) ve her 403'te (`errorInterceptor`) `GET /users/me` ile tazelenir (`AuthService.refreshMe`);
Şirket Yöneticisi kendi yetkisini değiştirince form da tazeler. "Yeni talep" herkese görünür; yetkisi olmayanda talep formunun
yerine mesaj çıkar (`newRequestBlockedMessage`, şirket yöneticileri `GET /tenants/current`'ten). Süreç sekmesi `BlockedReason`'daki
«yetki adı»nı kalın gösterir.

| Ekran | Değişiklik |
| --- | --- |
| Platform → Yeni şirket | "Rol" alanı kalkar |
| Yönetim → Kullanıcılar | Liste: "Rol" sütunu yerine "Yetkiler" (adlar virgülle; yoksa "Yetki yok"). Form: "Rol" yerine yetki kutucukları ve "Hazır set" seçimi (seçince kutucukları doldurur, değiştirilebilir) |
| Üst bar | "Elif Yılmaz · İş Analisti" yerine "Elif Yılmaz" ve şirket adı |
| Süreç sekmesi | `BlockedReason` ve `MissingPermission` (Bölüm 2); ön koşul satırları (Bölüm 4) |
| Talepler → "+ Yeni talep" | Yetkisi olmayan için mesaj (Bölüm 2) |
| Proje formu | Analist ve yönetici seçicileri `GET /users?permission=` ile dolar |
| Kişi profili, ekip iş yükü | Unvan yerine yetkiler (ad virgülle) |
| `role.guard.ts`, `user-role.ts` | Kaldırılır. Rota korumaları: `tenantAdminGuard`, `platformAdminGuard` kalır; iş rotaları korumasız açılır, işlem düğmede kontrol edilir |

## 4. Adım ön koşulları (A4)

### Veri

`ProcessStep.Prerequisites`: JSON kolon, boş liste varsayılan. Listedeki her ön koşul sağlanmalıdır (VE).

```jsonc
[ { "kind": "QuestionnaireComplete", "value": "customer-fit" } ]
```

| `kind` | `value` | Sağlanır, eğer… | Eksikse mesaj | Düğme → hedef |
| --- | --- | --- | --- | --- |
| `RequestFormComplete` | — | Talep Kayıt Formu'nun zorunlu alanları ve şirketin zorunlu özel alanları (Bölüm 6) dolu | "Talep Kayıt Formu'nun zorunlu alanları doldurulmalı." | "Talep formuna git" → `/projects/{id}/edit` |
| `QuestionnaireComplete` | soru seti kodu (`customer-fit`, `level`, `request-analysis`, `risk-analysis`, `internal-approval`) | Projedeki form tamamlandı (`QuestionnaireSummaryDto.IsComplete`) | "{Form adı} tamamlanmalı." | `customer-fit`, `level` → `?tab=assessment`; `request-analysis` → `?tab=request-analysis`; diğerleri → `?tab=assessment` |
| `LevelApplied` | — | `Project.Level` dolu | "Seviye projeye uygulanmalı." | "Seviye & Uygunluk'a git" → `?tab=assessment` |
| `MeetingExists` | — | Projede en az bir toplantı tutanağı var | "En az bir toplantı tutanağı girilmeli." | "Tutanaklara git" → `?tab=meetings` |
| `RiskExists` | — | Projede en az bir risk var | "En az bir risk girilmeli." | "Risklere git" → `?tab=risks` |
| `RequirementsApproved` | — | En az bir gereksinim var ve hiçbiri `Draft` ya da `InReview` değil | "Gereksinimler onaylanmalı; taslak ya da incelemede gereksinim kalmamalı." | "Gereksinimlere git" → `?tab=requirements` |
| `DocumentApproved` | doküman türü kodu | Projedeki o doküman `Approved` ya da `NotRequired` | "«{Doküman adı}» onaylanmalı." | "Dokümanlara git" → `?tab=documents` |

Bilinmeyen `kind` ya da şirkette olmayan `value` kaydedilemez (`400`, Bölüm 7).

### Kural

- Ön koşul **"Adımı tamamla"da ve karar adımında "Evet"te** kontrol edilir. "Hayır", reddetme ve erteleme kontrol etmez.
  Proje adıma geldiğinde değil, adım tamamlanırken bakılır. Bu yüzden ön koşul eklendiği anda yolun ortasındaki projeler
  takılmaz; geriye dönük kayıt da istenmez.
- Değerlendirme saf bir sınıftadır: `StepPrerequisites.Evaluate(prerequisites, ProjectFacts) → IReadOnlyList<UnmetPrerequisite>`.
  `ProjectFacts` projeden toplanan sayılar ve durumlardır; servis bunları bir kerede sorgular. Ekrandaki liste ile backend
  kontrolü aynı sonucu kullanır (`ProcessProgress.CanActOnStep` gibi).
- Önce yetki, sonra ön koşul kontrol edilir: yetkisi olmayan kişiye ön koşul gösterilmez.
- Eksik varsa backend `400` döner: "Bu adım tamamlanamaz: {ilk eksiğin mesajı}"

### API

`AvailableActionsDto`'ya eklenir:

```jsonc
"unmetPrerequisites": [
  { "kind": "QuestionnaireComplete", "message": "Müşteri Uygunluk Değerlendirme Anket Formu tamamlanmalı.",
    "linkText": "Seviye & Uygunluk'a git", "link": "/projects/11?tab=assessment" }
]
```

Liste boş değilse `canComplete` `false` olur ve `blockedReason` boş kalır (yetki varken engel ön koşuldur). Karar adımında
`canAnswer` `true` kalır, çünkü "Hayır" ön koşul istemez; ekran listeyi gösterir ve "Evet"i kapatır.
Süreç sekmesi listeyi, her eksiğin yanında "… git" düğmesiyle gösterir.

### Ithinka şablonu

`Seed/flows/new-software-project.json` (karar 10):

| Adım | Ön koşul |
| --- | --- |
| 3 · Talep kayda alınır | `RequestFormComplete` |
| 4 · Müşteri uygunluğu değerlendirilir | `QuestionnaireComplete: customer-fit` |
| 5 · Doküman seviyesi belirlenir | `LevelApplied` |
| 6 · Talep anlaşılır | `MeetingExists` |
| 8 · Riskler belirlenir | `RiskExists` |

Diğer adımlar ve diğer akışlar ekiple belirlenince dosyaya eklenir (plan Bölüm 8, açık konu 4). Şablon eşitlemesi,
değiştirilmemiş kopyalara ön koşulları da taşır.

### Testler

- `StepPrerequisites` için her tür, sağlanmış ve sağlanmamış durumlar veritabanısız test edilir.
- `ProcessService`: eksik ön koşulda tamamlama ve "Evet" `400`, "Hayır" ve reddetme çalışır.
- `TestProjects.CreateAsync(database, "4")` bugünkü gibi projeyi doğrudan adıma koyar. Adımı tamamlayan testler ön koşulu
  önce sağlar: `TestProjects.SatisfyAsync(database, projectId, kind)` (seviye, tutanak, risk). Formlar, gereksinimler ve
  dokümanlar gereken testte kendi servisleriyle doldurulur.

## 5. Atlama kuralları (A5)

### Veri

`ProcessStep.Conditions`: JSON kolon, boş liste varsayılan. Listedeki her kural sağlanırsa adım yapılır, biri sağlanmazsa
atlanır. Bugünkü `RequiresExternalRequest`, `RequiresOffer`, `MinLevel` kolonları **kaldırılır**.

```jsonc
[
  { "field": "source", "op": "equals", "value": "External" },
  { "field": "level", "op": "atLeast", "value": 2, "whenEmpty": "met" }
]
```

| `field` | Tür | Kaynağı |
| --- | --- | --- |
| `source` | seçim (`Internal`, `External`, `Affiliated`) | `Project.Source` |
| `requestType` | seçim (`RequestType` değerleri) | `Project.RequestType` |
| `requiresOffer` | evet-hayır | `Project.RequiresOffer` |
| `level` | sayı (1–3) | `Project.Level` |
| `custom:{key}` | özel alanın türü | `Project.CustomValues[key]` (Bölüm 6) |

| `op` | Kullanılabildiği türler | Anlamı |
| --- | --- | --- |
| `equals`, `notEquals` | hepsi | Değer eşit / eşit değil |
| `atLeast`, `atMost` | sayı, tarih | ≥ / ≤ |
| `isSet`, `isNotSet` | hepsi (`value` yok) | Alan dolu / boş |

- `whenEmpty`: alan boşken kuralın sonucu; `"notMet"` (varsayılan) ya da `"met"`. Bugünkü `MinLevel`, "seviye belirlenmemişse
  adım yapılır" dediği için `"met"` ile taşınır.
- Pasif özel alana ya da artık olmayan bir seçeneğe bakan kural **sağlanmamış** sayılır; akış ekranı bu kuralı uyarıyla gösterir.
- Değerlendirme saf sınıfta kalır: `StepConditions.Holds(conditions, ConditionContext)`. `ConditionContext` projenin dört alanını
  ve özel alan değerlerini taşır.

### Taşıma (migration, A5)

| Bugün | Kural |
| --- | --- |
| `RequiresExternalRequest = true` | `{ "field": "source", "op": "equals", "value": "External" }` |
| `RequiresOffer = true` | `{ "field": "requiresOffer", "op": "equals", "value": true }` |
| `MinLevel = n` | `{ "field": "level", "op": "atLeast", "value": n, "whenEmpty": "met" }` |

Seed dosyalarında `requiresExternalRequest`, `requiresOffer`, `minLevel` yerine `conditions` dizisi yazılır.
Ekranda (Süreç Kütüphanesi ve Süreç sekmesi) kural okunur metin olarak gösterilir: "Yalnızca: Talep kaynağı = Dış ·
Teklif hazırlanacak". Bugünkü etiketler bu metinden üretilir.

### Testler

Her alan türü × her karşılaştırma × boş değer, taşınan üç koşulun bugünkü sonuçlarla aynı olduğu ve bozuk kural
(pasif alan, olmayan seçenek) veritabanısız test edilir.

## 6. Şirkete özel alanlar (A6)

### `CustomField` — şirketin alan tanımı (yeni tablo `CustomFields`)

| Alan | Tür | Kural |
| --- | --- | --- |
| `Id` | int | |
| `TenantId` | int | `ITenantOwned` |
| `Key` | string | Küçük harf, rakam ve tire, 2–50 (`butce-onayli`). Şirket içinde benzersiz; **değişmez** |
| `Label` | string | 1–100 |
| `Type` | `CustomFieldType` | `Text`, `Number`, `Date`, `Select`, `Boolean`; **değişmez** |
| `Options` | string[] (JSON) | Yalnızca `Select`'te; 1–50 seçenek, her biri 1–100. Seçenek eklenebilir; atlama kuralında kullanılan seçenek çıkarılamaz |
| `IsRequired` | bool | Zorunluysa talep kaydedilirken ve `RequestFormComplete` ön koşulunda aranır |
| `HelpText` | string? | En fazla 300 |
| `Order` | int | Formdaki sıra |
| `IsActive` | bool | Alan silinmez, pasife alınır. Pasif alan formda görünmez; değeri projede saklı kalır |

`AuditableEntity`'den türer.

### `Project.CustomValues`

- `nvarchar(max)` JSON nesnesi: `{ "butce-onayli": true, "sozlesme-no": "S-2026-14" }`.
- Değerin türü alanın türüne göre: metin (en fazla 500), sayı, tarih (`yyyy-MM-dd`), seçim (seçeneklerden biri), evet-hayır.
- Projenin denetim kaydına tek alan olarak yazılır (`CustomValues`, eski ve yeni JSON).

### Uç noktalar

| İstek | Kim | Cevap |
| --- | --- | --- |
| `GET /custom-fields` | Herkes (aktifler); `?includeInactive=true` yalnızca Şirket Yöneticisi | `200` `CustomFieldDto[]`, `Order`'a göre |
| `POST /custom-fields` | Şirket Yöneticisi | `201` `CustomFieldDto` |
| `PUT /custom-fields/{id}` | Şirket Yöneticisi | `200`. `Key` ve `Type` gönderilmez |
| `POST /projects` · `PUT /projects/{id}` | Bugünkü gibi | `customValues` nesnesi eklenir. Zorunlu ve aktif bir alan boşsa `400`, anahtar `customValues.{key}` ("Bu alan zorunludur.") |
| `GET /projects/{id}` | Bugünkü gibi | `customValues` eklenir |

Ekran: talep formunda ve Genel Bilgiler'de "Şirkete özel bilgiler" bölümü (alan yoksa görünmez). Yönetim → Talep alanları:
liste, ekle, düzenle, pasife al.

## 7. Akış düzenleme (A7, A8)

### Veri

| Alan | Değişiklik |
| --- | --- |
| `ProcessFlow.UsedFor` | Yeni `ProcessFlowUse?`: `NewRequest`, `AdditionalModule`, `Improvement`, `ResourcePlanning`, `Billing`. Şirkette her değer en fazla bir akışta (filtreli benzersiz indeks). Boşsa akış hiçbir kaydı başlatmaz |
| `ProcessFlow.RowVersion` | Yeni; aynı akışın aynı anda düzenlenmesi `409` |
| `ProcessStage.IsActive`, `ProcessStep.IsActive` | Yeni, varsayılan `true` |

`ProcessFlowCodes` sabitleri kalkar. Servisler akışı `UsedFor` ile bulur: yeni talep `NewRequest`, ek geliştirme
`AdditionalModule`, süreç iyileştirme `Improvement`. Migration bugünkü beş akışı koduna göre işaretler.
Soru listelerinin iş tipi (`WorkType`) bugünkü gibi akışın şablon koduyla eşleşir. Kopyalanmış akış, kopyalandığı akışın
iş tipini kullanır.

### Adım numarası

Adımın `Code`'u ekranda "Adım 12" diye görünür. Akış düzenlenince aktif adımlar **sırayla yeniden numaralanır** (1, 2, 3…;
aşama aşama değil, akış boyunca). Pasif adımların kodu `P{Id}` olur. Kayıtlar adıma `Id` ile bağlı olduğu için süreç geçmişi
etkilenmez; geçmişte adımın bugünkü numarası görünür.

### Kurallar

- Yalnızca Şirket Yöneticisi düzenler; diğerleri `403` "Bu işlem için şirket yöneticisi olmalısınız."
- İlk düzenlemede akış `IsCustomized = true` olur. Bundan sonra açılıştaki şablon eşitlemesi bu akışa dokunmaz.
- **Pasife alma:** Adımda bekleyen aktif ya da ertelenmiş kayıt varsa `409` "Bu adımda bekleyen kayıtlar var: 20, 21." Başka
  bir adımın "Hayır" dönüşü bu adıma gidiyorsa `409` "Bu adıma «Hayır» dönüşü olan adımlar var: 10." Aşama ancak bütün adımları
  pasifse pasife alınır. Akışta en az bir aktif adım kalmalıdır.
- **Sıra:** Kayıtlar bulundukları adımda kalır. "Sonraki adım" yeni sıraya göre bulunur.
- **Adım formu kuralları:**
  - Karar adımında "Hayır" ya kaydı kapatır (`NoOutcome`) ya da geri döner (`NoJumps`); ikisi birden olmaz.
  - Dönüş yalnızca aynı akıştaki daha önceki aktif bir adıma yapılabilir.
  - Onaylayan yetkisi (`ApproverPermission`) yalnızca karar adımında verilebilir.
- **Doğrulama:** Ön koşul ve atlama kuralları Bölüm 4 ve 5'e göre doğrulanır; hata anahtarı listedeki yeridir
  (`conditions[1]`, `prerequisites[0]`). Uygulandı: `StepRuleValidation` (saf sınıf; A7'nin ilk parçası). Kurallar:
  - Bir adımda en fazla 10 kural ve 10 ön koşul.
  - Kural alanı şunlardan biri olmalı: kaydın dört alanı (`StepRuleValidation.BuiltInFields`) ya da şirketin aktif özel
    alanı (`custom:{key}`, A6 verir).
  - "En az / en çok" yalnızca sayı ve tarih alanlarında kullanılır. "Dolu / boş" değer almaz; diğerleri değer ister
    (en fazla 100 karakter).
  - Değer alanın türüne uymalı: seçimde seçeneklerden biri (büyük-küçük harf fark etmez), evet-hayırda `true` / `false`,
    sayıda sayı (seviye 1–3), tarihte `yyyy-MM-dd`.
  - Ön koşulda form ve doküman türü kodu şirkette olmalı; diğer türler değer almaz; aynı ön koşul iki kez eklenemez.
  - Seed dosyalarındaki bütün kurallar ve ön koşullar da bu kontrolden geçer (`SeedRuleValidationTests`).

### Uç noktalar

| İstek | Açıklama |
| --- | --- |
| `PUT /process-flows/{code}` | Ad, `usedFor`, `rowVersion` |
| `POST /process-flows/{code}/copy` | `{ code, name }` ile kopya; kopya `IsCustomized = true`, `UsedFor` boş |
| `POST /process-flows/{code}/stages` · `PUT /process-stages/{id}` | Aşama ekle / başlık, kısa başlık |
| `POST /process-flows/{code}/steps` | `{ stageId, afterStepId?, text, … }` adım ekle |
| `PUT /process-steps/{id}` | Metin, karar mı, "Hayır" metni, sorumlu ve onaylayan yetki, `NoOutcome`, `NoJumps`, `Prerequisites`, `Conditions` |
| `POST /process-steps/{id}/deactivate` · `/activate` | Pasife al / geri al |
| `POST /process-stages/{id}/deactivate` · `/activate` | Aynı |
| `PUT /process-flows/{code}/order` | `{ stages: [{ id, stepIds: [...] }], rowVersion }`; aşama ve adımların yeni sırası |

Her yazma isteği güncel akışı (`ProcessFlowDto`, `rowVersion` dahil) döner. `GET /process-flows/{code}` ve Süreç Kütüphanesi
pasif adımları yalnızca `?includeInactive=true` ile (Şirket Yöneticisi) gösterir.

### Ekran (A8)

Süreç Kütüphanesi'nde Şirket Yöneticisine "Akışı düzenle" düğmesi çıkar. Düzenleme sayfası şunlardan oluşur:
- Aşama ve adım listesi; yukarı / aşağı ile sıralama ve "Kaydet sırayı".
- Adım formu: metin, sorumlu yetki, karar mı, onaylayan yetki, "Hayır" (kapanış ya da dönüş), ön koşullar, atlama kuralları.
- Pasife alma, pasifleri gösterme ve akış ayarları (ad, kullanım).

Sürükle-bırak isteğe bağlıdır; yukarı / aşağı yeterlidir.

## 8. Yapay zekâ modeli ve kullanımı (A9)

### Veri

| Alan | Kural |
| --- | --- |
| `Tenant.AiModel` | string?, en fazla 100. Boşsa servisin varsayılanı |
| `Tenant.AiMonthlyTokenLimit` | int?; boşsa sınırsız (açık konu 6) |

**`AiUsage`** — her yapay zekâ çağrısı (yeni tablo `AiUsages`, `ITenantOwned`, denetim kaydına yazılmaz)

| Alan | Tür | Kural |
| --- | --- | --- |
| `Id` | long | |
| `TenantId` | int | |
| `UserId` | int (FK → User) | |
| `ProjectId` | int? (FK → Project) | E-postadan taslakta boş |
| `Assistant` | `AiAssistant` | `MeetingSuggestions`, `RiskSuggestions`, `RiskAssessment`, `RequestDraft`, `Transcription` |
| `Model` | string | Cevap veren model (yedek model devreye girdiyse o) |
| `InputTokens`, `OutputTokens` | int? | Ses kaydında boş |
| `AudioSeconds` | int? | Yalnızca ses kaydında |
| `DurationMs` | int | |
| `Succeeded` | bool | Başarısız çağrı da yazılır |
| `CreatedAt` | DateTime (UTC) | |

### Servis (`ai/`)

- Her isteğe isteğe bağlı `model` alanı eklenir. Servis önce bu modeli, sonra kendi yedek modellerini dener. Servis durumsuz kalır.
- Her cevaba `usage` eklenir: `{ "model": "...", "inputTokens": 812, "outputTokens": 240 }`. Ses kaydında
  `{ "model": "small", "audioSeconds": 61 }`. Bilgi bugün `llm.py`'de toplanıyor; yalnızca cevaba taşınır.
- `GET /v1/models`: servisin kullanabileceği metin modelleri (`list_text_models`).

### Kurallar ve uç noktalar

- Backend her çağrıda şirketin modelini gönderir ve kullanımı yazar. Kayıt asıl işlemi bozmaz; yazılamazsa loglanır.
- Sınır doluysa servis çağrılmadan `503` "Şirketinizin bu ayki yapay zekâ kullanım sınırı doldu." Ay, UTC takvim ayıdır;
  sayılan değer girdi + çıktı token'larıdır.
- `GET /agents/models` (Şirket Yöneticisi): servisten gelen model listesi. Servis kapalıysa boş liste.
- `GET` / `PUT /tenants/current/settings`: `aiModel` ve `aiMonthlyTokenLimit` eklenir. Listede olmayan model `400`
  "Seçilen model kullanılamıyor."
- `GET /tenants/current/ai-usage?month=2026-11` (Şirket Yöneticisi):
  `{ month, requests, inputTokens, outputTokens, audioSeconds, limit, byAssistant: [{ assistant, requests, tokens }] }`.
- Ekran: Şirket Ayarları'na "Model" seçimi, "Aylık sınır (token)" ve "Bu ayın kullanımı" kartı eklenir.

## 9. Hata mesajları (yeni)

| Durum | Kod | `detail` |
| --- | --- | --- |
| Adım için yetki yok | `403` | "Bu adım için «{yetki}» yetkisi gerekir. Şirket yöneticinizden isteyin: {yöneticiler}." |
| Onay için yetki yok | `403` | "Bu adımı onaylamak için «Onay ve karar» yetkisi gerekir. …" |
| Talep kararı için yetki yok | `403` | "Talep hakkında karar vermek için «Onay ve karar» yetkisi gerekir. …" |
| Talep açmak için yetki yok | `403` | "Talep açmak için «Analiz adımları» yetkisi gerekir. …" |
| Seçilen kişide yetki yok | `400` | "Seçilen kişinin «{yetki}» yetkisi yok." |
| Ön koşul eksik | `400` | "Bu adım tamamlanamaz: {eksik}" |
| Adımda bekleyen kayıt var | `409` | "Bu adımda bekleyen kayıtlar var: {kodlar}." |
| Adıma dönüş var | `409` | "Bu adıma «Hayır» dönüşü olan adımlar var: {adımlar}." |
| Akışta aktif adım kalmıyor | `400` | "Akışta en az bir aktif adım olmalıdır." |
| Akış başkası tarafından değişti | `409` | "Akış bu arada değişti; sayfayı yenileyin." |
| Kullanım zaten başka akışta | `409` | "Bu kullanım başka bir akışta seçili: {akış}." |
| Özel alan anahtarı kullanılıyor | `409` | "Bu anahtarla bir alan zaten var." |
| Özel alan anahtarı biçimi | `400` | "Anahtar küçük harf, rakam ve tireden oluşmalıdır (2–50 karakter)." |
| Kuralda kullanılan seçenek çıkarılıyor | `409` | "Bu seçenek bir atlama kuralında kullanılıyor: {akış} · Adım {no}." |
| Bilinmeyen ön koşul ya da kural | `400` | "Geçersiz ön koşul." / "Geçersiz atlama kuralı." |
| Kuralın alanı yok / bilinmiyor | `400` | "Kural alanı seçilmelidir." / "Bilinmeyen alan: «{alan}»." |
| Karşılaştırma alana uymuyor | `400` | "«{alan adı}» için «en az» ve «en çok» kullanılamaz." |
| Kuralın değeri | `400` | "Değer girilmelidir." / "«Dolu» ve «boş» karşılaştırmalarında değer girilmez." / "Değer en fazla 100 karakter olabilir." / "«{değer}», «{alan adı}» alanının seçeneklerinden biri değil." / "Değer «Evet» ya da «Hayır» olmalıdır." / "Değer bir sayı olmalıdır." / "Seviye 1 ile 3 arasında olmalıdır." / "Tarih yyyy-aa-gg biçiminde olmalıdır." |
| Ön koşulun değeri | `400` | "Bu ön koşul için form seçilmelidir." / "Bu ön koşul için doküman seçilmelidir." / "Şirkette «{kod}» kodlu bir form yok." / "Şirkette «{kod}» kodlu bir doküman türü yok." / "Bu ön koşul bir değer almaz." / "Aynı ön koşul iki kez eklenemez." |
| Çok fazla kural / ön koşul | `400` | "Bir adımda en fazla 10 kural olabilir." / "Bir adımda en fazla 10 ön koşul olabilir." |
| Model kullanılamıyor | `400` | "Seçilen model kullanılamıyor." |
| Aylık sınır doldu | `503` | "Şirketinizin bu ayki yapay zekâ kullanım sınırı doldu." |

## 10. AGENTS.md'ye taşınacaklar

Her iş kendi PR'ında taşır:
- **A2:** Bölüm 6'da `UserRole` satırları yerine `Permission`, `UserPermission`, `IsPlatformAdmin`. Bölüm 8'de "Giriş yapan
  kullanıcı" ve `HasPermissionAsync`. "Akış dışındaki işler yetki istemez" kuralı.
- **A4:** Bölüm 6'da ön koşul; Bölüm 9'da `TestProjects.SatisfyAsync`.
- **A5:** Bölüm 6'daki "Atlanan adım" satırı `Conditions` ile değişir.
- **A6:** Bölüm 6'da `CustomField`, `CustomValues`.
- **A7:** Bölüm 8 → Seed'deki "adım eklemek migration ister" cümlesi değişir: şirketin düzenlediği akış eşitlenmez; şablona
  adım eklemek yalnızca düzenlenmemiş kopyalara gider.
- **A9:** Bölüm 8 → Yapay zekâ servisinde model ve kullanım.

## 11. Açık konular

1. **Adım numarası:** Yeniden numaralama (Bölüm 7) geçmişteki "Adım 12" yazılarını değiştirir. Alternatif: kod hiç değişmez,
   yeni adım en büyük + 1 alır. A7'de karar verilir.
2. **Ön koşul listesi:** Bölüm 4'teki yedi tür bugün şirketin süreç dokümanında geçen işlerdir; yeni tür istenirse A4'te eklenir.
3. **Özel alanlar talep listesinde görünsün mü?** Bu fazda hayır (plan Bölüm 7).
