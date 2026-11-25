# Çok Şirketli Yapı — Sözleşme

Tek kurulumun birden fazla şirkete (tenant) hizmet etmesi: veri modeli, kimlik, okuma ve yazma kuralları, seed, yönetim
uç noktaları ve testler. Plan ve iş dağılımı: [phase-6-plan.md](phase-6-plan.md).
Durum: **onaylandı** (24 Kasım 2026). Sorumlu: Erva (T1). Uygulama: T2–T7. Bölüm 12'deki kurallar T2 ile AGENTS.md'ye taşındı.
T4 uygulandı: kimlik (`PlatformAdmin`, `IsTenantAdmin`, `MustChangePassword`, parola değiştirme, her istekte aktiflik kontrolü; #166),
şirket içi kullanıcı yönetimi, şirket açma / düzenleme ve Platform Yöneticisinin seed'i.

Bu belge onaylanınca kurallar T2 ile birlikte AGENTS.md'ye (Bölüm 6, 7, 8, 9) taşınır (Bölüm 12).

## 1. Kavramlar

| Türkçe | Kodda | Açıklama |
| --- | --- | --- |
| Şirket | `Tenant` | Uygulamayı kullanan kurum. Kendi kullanıcıları, akışları, soru listeleri, doküman türleri ve kayıtları vardır |
| Şirket kimliği | `TenantId` | Şirkete ait her satırda bulunur |
| Şirkete ait kayıt | `ITenantOwned` | `int TenantId` taşıyan her entity bu arayüzü uygular. `User` uygulamaz; kuralı ayrıdır (Bölüm 2 → `Users`) |
| İsteğin şirketi | `ITenantContext` | Filtrenin ve interceptor'ın kullandığı şirket. İstek (kapsam) başına bir tane; API'de girişten, seed ve şirket açarken koddan ayarlanır (Bölüm 3) |
| Şablon | `Seed/` dosyaları | Yeni şirkete kopyalanan başlangıç akışları, soru setleri ve doküman türleri |
| Şablondan gelen kayıt | `TemplateCode`, `IsCustomized` | Kopyanın hangi şablondan geldiği ve şirketin onu değiştirip değiştirmediği |
| Platform Yöneticisi | `UserRole.PlatformAdmin` | Hiçbir şirkete ait değildir; şirket açar ve pasife alır. Şirketlerin kayıtlarını göremez |
| Şirket Yöneticisi | `User.IsTenantAdmin` | Kendi şirketinin kullanıcılarını ve ayarlarını yönetir. Rol değil işarettir: bir İş Analisti aynı zamanda şirket yöneticisi olabilir (kullanıcı başına tek rol kuralı değişmez) |

## 2. Veri modeli

### `Tenant` — şirket (yeni tablo `Tenants`)

| Alan | Tür | Kural |
| --- | --- | --- |
| `Id` | int | |
| `Code` | string | Kısa ad, küçük harf ve tire (`ithinka`, `ornek-lojistik`); 2–50 karakter, benzersiz, sonra değişmez |
| `Name` | string | Görünen ad, en fazla 200 |
| `IsActive` | bool | Pasif şirketin kullanıcıları giriş yapamaz (Bölüm 4) |
| `AiEnabled` | bool | Yapay zekâ yardımcıları açık mı. Varsayılan `false` |
| `AiDataSharingAllowed` | bool | Metnin (notlar, e-posta, risk metni) dış model sağlayıcısına gönderilmesine izin var mı. Varsayılan `false` |
| `CreatedAt` | DateTime | |

`Tenant` silinmez (AGENTS.md: kayıt silinmez); pasife alınır. `Tenant` kendisi `ITenantOwned` değildir ve denetim kaydına yazılmaz.

### `TenantId` taşıyan tablolar

**Bütün tablolar**, `Tenants` hariç: `Users`, `Projects`, `ProcessFlows`, `ProcessStages`, `ProcessSteps`,
`ProcessStepJumps`, `StepRecords`, `RequestDecisions`, `AuditLogs`, `OpenIssues`, `Meetings`, `Requirements`,
`DocumentTypes`, `ProjectDocuments`, `Risks`, `Notifications`, `QuestionSets`, `Questions`,
`QuestionOptions`, `Questionnaires`, `QuestionAnswers`, `ProcessImprovements`.

- `TenantId` int, zorunlu, `Tenants.Id`'ye `Restrict` yabancı anahtar ve indeksli.
- **Tek istisna:** `Users.TenantId` yalnızca Platform Yöneticisi için boştur (aşağıda `Users`).
- Alt kayıtlar (risk, açık konu, adım kaydı…) da `TenantId` taşır, projeden türetilmez. Böylece her sorgu kendi
  tablosunda filtrelenir; filtrenin bir join'e bağlı olması gerekmez.

### `Users` değişiklikleri

| Alan | Tür | Kural |
| --- | --- | --- |
| `TenantId` | int? | Platform Yöneticisinde boş, diğer herkeste dolu |
| `IsTenantAdmin` | bool | Şirket Yöneticisi işareti. Varsayılan `false`; Platform Yöneticisinde hep `false` |
| `MustChangePassword` | bool | Geçici parolayla açıldıysa `true`; parola değişince `false` (Bölüm 4) |
| `Role` | `UserRole` | Yeni değer `PlatformAdmin` |

- Kural veritabanında da tutulur:
  `CHECK ((Role = 'PlatformAdmin' AND TenantId IS NULL AND IsTenantAdmin = 0) OR (Role <> 'PlatformAdmin' AND TenantId IS NOT NULL))`.
- `User`, `TenantId`'si boş olabildiği için `ITenantOwned` uygulamaz. Filtresi ve interceptor kuralı ayrıca yazılır
  (Bölüm 3 ve 5). Böylece Platform Yöneticisi, şirketsiz kayıt yasağına takılmadan seed'den açılabilir.

### Benzersizlik

| Bugün | Faz 6'da |
| --- | --- |
| `Projects.Code` | `(TenantId, Code)` |
| `ProcessFlows.Code` | `(TenantId, Code)` |
| `DocumentTypes.Code` | `(TenantId, Code)` |
| `QuestionSets.Code` | `(TenantId, Code)` |
| `Questions.Key` | `(TenantId, Key)` |
| `QuestionOptions.Key` | `(TenantId, Key)` |
| `Users.Email` | **Değişmez**, bütün sistemde benzersiz (giriş şirketi e-postadan bulur, Bölüm 4) |
| Proje içi kodlar (`(ProjectId, Code)`), `(ProcessFlowId, …)`, `ProjectId` benzersizlikleri | Değişmez; üst kayıt zaten tek şirkete ait |

Soru bankasının üç anahtarı (`QuestionBankConfigurations.cs`) bugün bütün sistemde benzersiz. Şirkete göre ayrılmazlarsa
ikinci şirkete şablon kopyalanırken aynı anahtar ikinci kez yazılır ve şirket açılamaz.

### Şablondan gelen kayıtlar

`ProcessFlow`, `DocumentType` ve `QuestionSet` iki alan alır:

| Alan | Tür | Kural |
| --- | --- | --- |
| `TemplateCode` | string? | Kopyalandığı şablonun kodu (`new-software-project`, `level`…); şirketin kendi eklediği kayıtta boş |
| `IsCustomized` | bool | Şirket bu kaydı (veya akışın aşama / adımlarını, setin sorularını) değiştirdiyse `true`. Faz 6'da düzenleme ekranı olmadığı için hep `false`; alan sonraki faz için şimdiden eklenir |

### Mevcut verinin taşınması (tek migration)

1. `Tenants` tablosu açılır ve **Ithinka** (`Id = 1`, `Code = "ithinka"`, `AiEnabled` ve `AiDataSharingAllowed` bugünkü
   davranış korunsun diye `true`) eklenir. `Id` kimlik (identity) kolonu olduğu için bu satır `SET IDENTITY_INSERT` ile yazılır.
2. Bütün tablolara `TenantId` boş olarak eklenir, bütün satırlar `1` yapılır, sonra kolon zorunlu olur.
3. Benzersizlik indeksleri yukarıdaki tabloya göre yeniden kurulur.
4. `TemplateCode` bugünkü akış, doküman türü ve soru seti kodlarıyla doldurulur; `IsCustomized = false`.

Canlı veri yok; migration yine de geri alınabilir yazılır (`Down`).

### Sonraki migration'larda akış değişikliği

Faz 6'dan sonra bir akışa adım eklemek, çıkarmak ya da sıralamak, o şablondan gelen **bütün şirketlerin** kopyasını
değiştirir. Migration kopyaları koddan bulur, sabit id kullanmaz:

```sql
INSERT INTO ProcessSteps (TenantId, ProcessFlowId, ProcessStageId, Code, ...)
SELECT f.TenantId, f.Id, s.Id, '4.8', ...
FROM ProcessFlows f
JOIN ProcessStages s ON s.ProcessFlowId = f.Id AND s.[Order] = 4
WHERE f.TemplateCode = 'new-software-project' AND f.IsCustomized = 0;
```

Şirketin değiştirdiği kopyalara (`IsCustomized = 1`) dokunulmaz (karar 8).

## 3. Okuma kuralı: global sorgu filtresi

`IsdlcDbContext`, `ITenantOwned` olan her entity için şu filtreyi ekler:

```csharp
builder.Entity<T>().HasQueryFilter(e => e.TenantId == _tenant.TenantId);

// Users: TenantId boş olabildiği için açık bir boşluk kontrolüyle
builder.Entity<User>().HasQueryFilter(u => _tenant.TenantId != null && u.TenantId == _tenant.TenantId);
```

- `_tenant` istek başına çözülen `ITenantContext`'tir (Bölüm 4). Giriş yapılmamışsa ya da kullanıcı Platform Yöneticisiyse
  `TenantId` boştur ve filtre **hiçbir satır döndürmez**: Platform Yöneticisi şirket verisini yapısal olarak göremez.
- `Users` filtresindeki `!= null` kontrolü şart: EF Core C#'taki `null == null → true` anlamını korur. Kontrol olmasa
  Platform Yöneticisi (şirketi boş) `Users`'ta şirketi boş olan satırları, yani diğer platform yöneticilerini görürdü.
- DbContext havuzlanmaz (`AddDbContext`, `AddDbContextPool` değil). Filtre istek başına `ITenantContext`'i kullandığı
  için bu bir kuraldır.
- Servisler değişmez; `dbContext.Projects` zaten yalnızca kullanıcının şirketini görür. Sayaçlar ("en büyük + 1",
  `RecordCodes.NextNumberAsync`) da kendiliğinden şirket içinde sayar.
- **`IgnoreQueryFilters()` yalnızca şu yerlerde** kullanılır; her kullanımın üstüne `// tenant: <neden>` yorumu yazılır ve
  PR açıklamasında belirtilir:

| Yer | Neden |
| --- | --- |
| Giriş (`AuthService`) | E-postadan kullanıcıyı ve şirketini bulmak; o anda şirket bilinmiyor |
| E-posta benzersizliği kontrolü | E-posta bütün sistemde benzersiz |
| Platform Yöneticisinin şirket listesi (`TenantService`) | Kullanıcı sayısı (`TenantDto.userCount`); yalnızca sayı okunur |
| Her istekte aktiflik kontrolü (`AccountStatusService`) | Giriş yapanın **kendi** satırı, token'daki id ile. Platform Yöneticisinin şirketi boş olduğu için filtre onu bulamaz (T4) |
| Parola değiştirme (`AuthService.ChangePasswordAsync`) | Aynı neden: giriş yapanın kendi satırı, token'daki id ile (T4) |
| Platform Yöneticisinin seed'i (`PlatformAdminSeeder`) | Yalnızca bir Platform Yöneticisi var mı diye bakılır; şirketi olmadığı için filtre onu bulamaz (T4) |

**Seed ve şirket açma filtreyi atlamaz**, şirketi ayarlar: `ITenantContext`'i sırayla her aktif şirkete (seed) ya da yeni
şirkete (`POST /tenants`) ayarlayıp mevcut kodu çalıştırır. Nedeni:

- Bugünkü seed "tablo boşsa yükle" diye çalışıyor (`DatabaseSeeder.cs`: `db.Users.AnyAsync()`, `db.Projects.AnyAsync()`,
  `db.DocumentTypes.AnyAsync()`). Açılışta giriş yapan kimse olmadığı için filtre boş döner, bu kontroller hep "boş" der ve
  seed her açılışta yeniden çalışıp e-posta benzersizliğine takılır.
- Filtre atlanırsa da akış ve soru seti eşitlemesi kodla arar (`SingleOrDefault(f => f.Code == …)`,
  `ToDictionaryAsync(s => s.Code)`); birden fazla şirkette aynı kod bulunur ve hata verir.
- Şirket ayarlanınca bu kontroller kendiliğinden "bu şirket boşsa" ve "bu şirketin akışı" anlamına gelir; seed kodu
  neredeyse değişmez.

- **Ham SQL** (`FromSql`, `ExecuteSql`) uygulama kodunda kullanılmaz; filtreyi atlar. Yalnızca migration'larda.
- `ExecuteUpdate` / `ExecuteDelete` filtreye uyar; yine de bu PR'larda şirket testi yazılır.

## 4. Kimlik: şirket nereden gelir

### Giriş

`POST /api/v1/auth/login` değişmez (`{ email, password }`). Akış:

1. Kullanıcı e-postayla bulunur (bütün şirketlerde, `IgnoreQueryFilters`).
2. Kullanıcı pasifse, şirketi pasifse ya da parola yanlışsa **aynı 401** döner ("E-posta veya parola hatalı.");
   pasif şirketin varlığı dışarıya söylenmez.
3. Token'a mevcut claim'lere ek olarak `tenant` (şirket `Id`'si, Platform Yöneticisinde yok), `tenantAdmin` (`true`/`false`)
   ve parola değiştirilmesi gerekiyorsa `mustChangePassword: true` yazılır.
4. Cevaptaki `user` nesnesine `tenant: { id, code, name }` (Platform Yöneticisinde `null`) ve `isTenantAdmin` eklenir;
   cevabın kendisine `mustChangePassword` (`true` / `false`) eklenir. Parola değiştirme de aynı biçimde cevap döner.

### İstek başına

```csharp
public interface ICurrentUserService
{
    int? UserId { get; }
    int? TenantId { get; }      // JWT "tenant"; Platform Yöneticisinde ve girişsiz istekte null
    bool IsTenantAdmin { get; }
    bool IsPlatformAdmin { get; }
}

/// <summary>Global filtrenin ve interceptor'ın kullandığı şirket. API'de ICurrentUserService'ten, seed'de elle verilir.</summary>
public interface ITenantContext
{
    int? TenantId { get; }
}
```

- `ITenantContext` kapsam başına (scoped) kaydedilir. Uygulaması değiştirilebilir: API'de istek başında
  `ICurrentUserService.TenantId` ile dolar; seed ve şirket açma, işe başlamadan önce kendisi ayarlar (Bölüm 3).
  Servisler `ITenantContext`'i değiştirmez.
- **Pasife alınan şirket ya da kullanıcı:** her istekte kullanıcının ve şirketinin aktif olduğu kontrol edilir
  (1 dakika önbellekli). Biri pasifse `401` döner ve ekran girişe yönlenir. Böylece açık oturumlar token süresini
  beklemeden kapanır. Bugün kullanıcının aktifliği yalnızca girişte kontrol ediliyor (`AuthService`); pasife alınan
  kullanıcının token'ı süresi dolana kadar çalışıyor. Şirket için eklenen kontrol kullanıcıyı da kapsar.

### Parola

Bugün parolalar yalnızca seed'den (`Identity:TestPassword`) geliyor. Yeni şirket ve kullanıcı açılabilmesi için:

- Yeni kullanıcıyı açan (Platform ya da Şirket Yöneticisi) bir **geçici parola** verir (en az 12 karakter).
- Kullanıcıda `MustChangePassword = true` olur. Giriş başarılı olur ama cevapta `mustChangePassword: true` döner; ekran
  parola değiştirme sayfasına yönlenir. Parola değişene kadar `POST /auth/change-password` dışındaki her istek `403` alır
  ("Devam etmek için parolanızı değiştirin."). Kontrol, token'daki `mustChangePassword` claim'inden yapılır; her istekte
  veritabanı okunmaz.
- `POST /api/v1/auth/change-password` (`{ currentPassword, newPassword }`): yeni parola en az 12 karakter ve
  eskisinden farklı olmalıdır. Başarılıysa `mustChangePassword` claim'i olmayan yeni bir token döner.
- Seed kullanıcıları ve testler etkilenmez (`MustChangePassword = false`).

## 5. Yazma kuralı: `TenantInterceptor`

`AuditableEntityInterceptor`'ın yanına yeni bir `SaveChangesInterceptor`:

| Durum | Davranış |
| --- | --- |
| Yeni `ITenantOwned` kayıt, `TenantId` boş (0) | `ITenantContext.TenantId` yazılır |
| Yeni kayıt, `TenantId` dolu ve mevcut şirketten farklı | **Hata** (`InvalidOperationException` → 500, loga yazılır). Yalnızca seed ve şirket yönetimi başka şirkete yazabilir; onlar `ITenantContext`'i o şirkete ayarlar |
| Var olan kaydın `TenantId`'si değiştirilmiş | **Hata** (aynı) |
| Şirket bilinmiyor (girişsiz, Platform Yöneticisi) ve `ITenantOwned` kayıt ekleniyor | **Hata** (aynı) |
| Yeni `User`, rolü `PlatformAdmin` | `TenantId` boş kalır (Bölüm 2 → `Users`) |
| Yeni `User`, başka rol | `ITenantOwned` gibi: boşsa `ITenantContext.TenantId` yazılır, farklıysa ya da şirket bilinmiyorsa hata |

**Sıra ve denetim kaydı:**

- Interceptor sırası: `TenantInterceptor` → `AuditableEntityInterceptor` → `AuditLogInterceptor`. Şirket, diğerleri
  kaydı görmeden yazılmış olur.
- `AuditLogInterceptor` log satırlarını kayıttan sonra ikinci bir kayıtla yazar (`SavedChanges`). Logun `TenantId`'si
  değişen entity'nin `TenantId`'sinden alınır; interceptor bunu açıkça atar.
- `TenantId`, `AuditLogInterceptor`'daki `IgnoredProperties`'e eklenir. Eklenmezse her yeni kayıtta "TenantId: boş → 1"
  bir alan değişikliği gibi Kayıt Geçmişi'ne yazılır.

**Başka şirketin kaydına bağlanma:** bir istekten gelen id (sorumlu kullanıcı, analist, ana proje, tutanak…) kayda
yazılmadan önce **her zaman context üzerinden okunur** (bugünkü "aktif kullanıcı mı", "proje var mı" kontrolleri).
Filtre sayesinde başka şirketin kaydı bulunamaz ve mevcut mesajlar döner ("Seçilen kişi aktif bir kullanıcı değil.",
`404`). Bir id'yi okumadan doğrudan yabancı anahtara yazmak yasaktır; T7 testleri bunu her modülde dener.

## 6. Seed ve şablon

| Dosya | Faz 6'da |
| --- | --- |
| `Seed/flows/*.json`, `Seed/question-sets/*.json`, `Seed/document-types.json` | **Şablon.** Yeni şirket açılınca hepsi o şirkete kopyalanır (`TemplateCode` = dosyadaki kod) |
| `Seed/users.json` | Yalnızca Ithinka'nın geliştirme kullanıcıları. Yeni şirkete kopyalanmaz; şirketin ilk kullanıcısı Şirket Yöneticisidir |
| `Seed/sample-projects.json` | Yalnızca geliştirme ortamında ve yalnızca Ithinka'ya |
| Yeni: Platform Yöneticisi | Geliştirme ortamında seed'den (`platform@isdlc.local`, parola `Identity:TestPassword`); başka ortamda ilk kurulumda yapılandırmadan (`Platform:AdminEmail`) |

**Açılış eşitlemesi:** bugün akışlar her açılışta dosyayla eşitleniyor. Faz 6'da bu, **her aktif şirket** için
`TemplateCode`'u dolu ve `IsCustomized = false` olan akışlara uygulanır. Kural aynı kalır: metin, rol ve dönüş
değişiklikleri dosyadan; adım eklemek, çıkarmak ya da sıralamak migration ister ve migration **bütün şirketlerin** kopyasını
değiştirir (Bölüm 2 → Sonraki migration'larda akış değişikliği). Soru setleri ve doküman türleri için de aynı.

**Açılış sırası:**

1. Migration'lar uygulanır.
2. `Tenants` okunur (filtresiz tablo).
3. Her aktif şirket için `ITenantContext` o şirkete ayarlanır ve bugünkü seed adımları çalışır: şablon eşitlemesi, Ithinka
   için ayrıca kullanıcılar ve geliştirme ortamında örnek projeler (Bölüm 3).
4. `ITenantContext` boşken Platform Yöneticisi açılır (yoksa). `User` kuralı buna izin verir (Bölüm 5).

**Yeni şirket açmak** (`POST /tenants`) tek bir işlemde (transaction): şirket satırı, şablonların kopyası ve ilk Şirket
Yöneticisi. `TenantService` şirket satırını kaydettikten sonra `ITenantContext`'i yeni şirkete ayarlar ve şablon kopyasını
seed'in kullandığı kodla yapar. Biri başarısız olursa hiçbiri kaydedilmez.

**Uygulandı (T3).** Ortak kod `ITenantTemplates.ApplyAsync()` (`Isdlc.Application/Common/Interfaces/`, uygulaması
`Isdlc.Infrastructure/Tenants/TenantTemplates.cs`): akışlar ve doküman türleri (`DatabaseSeeder.SeedTemplatesAsync`), sonra
soru setleri (`QuestionSetSeeder`). Kurallar:

- `ITenantContext` boşsa hata verir; transaction açmaz, şirketi değiştirmez (ikisini çağıran yapar). T4'te `TenantService`:
  şirket satırını kaydet → `ITenantContext.Use(yeniŞirket)` → `ApplyAsync()` → ilk yönetici, hepsi tek transaction'da.
- Şablon ile kopya `TemplateCode` ile eşleşir. Kopya yoksa eklenir; `IsCustomized = false` ise dosyayla eşitlenir;
  `IsCustomized = true` ise dokunulmaz.
- Şirketin **kendi** kaydı şablonun koduyla aynı koda sahipse (`TemplateCode` boş) şablon kopyalanmaz, uyarı loglanır;
  şirketin kaydı kazanır.
- Doküman türleri şirketin `new-software-project` kopyasının aşamalarına bağlanır; o kopya yoksa doküman türleri atlanır.
- Açılışta her şirket ayrı DI kapsamında ve ayrı transaction'da çalışır; bir şirketin hatası diğerine yarım kayıt bırakmaz.
- Platform Yöneticisinin açılışta oluşturulması (4. adım) T4'tedir.

## 7. Uç noktalar

Bütün yeni uç noktalar AGENTS.md Bölüm 7'deki ortak kurallara uyar.

### Platform Yöneticisi

| İstek | Açıklama | Cevap |
| --- | --- | --- |
| `GET /tenants` | Şirketler (pasifler dahil), ada göre; sayfalı | `200` `PagedResult<TenantDto>` |
| `POST /tenants` | Yeni şirket: şablon kopyası ve ilk Şirket Yöneticisi | `201` `TenantDto` |
| `PUT /tenants/{id}` | Ad ve aktiflik | `200` `TenantDto` |

```jsonc
// POST /tenants
{
  "code": "ornek-lojistik",
  "name": "Örnek Lojistik A.Ş.",
  "admin": { "fullName": "Deniz Aydın", "email": "deniz@ornek-lojistik.com", "role": "BusinessAnalyst",
             "temporaryPassword": "…" }
}

// TenantDto
{ "id": 2, "code": "ornek-lojistik", "name": "Örnek Lojistik A.Ş.", "isActive": true,
  "userCount": 1, "createdAt": "2026-11-23T09:00:00Z" }
```

Şirketin proje, kullanıcı ya da kayıt ayrıntısı bu uç noktalarda **yer almaz** (karar 10); yalnızca kullanıcı sayısı.

### Şirket (her kullanıcı)

| İstek | Açıklama | Cevap |
| --- | --- | --- |
| `GET /tenants/current` | Kullanıcının şirketi (üst barda ad için) | `200` `{ id, code, name }` |

### Şirket Yöneticisi

| İstek | Açıklama | Cevap |
| --- | --- | --- |
| `GET /users/manage` | Şirketin bütün kullanıcıları, pasifler dahil; e-posta ve işaretlerle (`UserDto[]`). Formlardaki kişi listesi (`GET /users`, yalnızca aktifler) değişmez | `200` |
| `POST /users` | Şirkete kullanıcı ekler (ad, e-posta, rol, şirket yöneticisi mi, geçici parola) | `201` `UserDto` |
| `PUT /users/{id}` | Ad, rol, aktiflik, şirket yöneticisi işareti | `200` `UserDto` |
| `POST /users/{id}/reset-password` | Geçici parola verir, `MustChangePassword = true` | `204` |
| `GET /tenants/current/settings` · `PUT /tenants/current/settings` | `{ aiEnabled, aiDataSharingAllowed }` | `200` |

- Kullanıcının e-postası sonradan değişmez (giriş kimliği).
- Rol `PlatformAdmin` verilemez; Platform Yöneticisi yalnızca yapılandırmayla açılır.
- Pasife alınan kullanıcının önbellekteki aktiflik cevabı hemen silinir; bir sonraki isteğinde çıkış yapar.
- Platform Yöneticisi açılışta, şirketlerden sonra açılır (yoksa): geliştirme ortamında `platform@isdlc.local` (ya da
  `Platform:AdminEmail`) ve `Identity:TestPassword`; başka ortamda yalnızca `Platform:AdminEmail` ve `Platform:AdminPassword`
  verilmişse, geçici parolayla (`MustChangePassword`).

### Yapay zekâ ayarlarının etkisi

| `AiEnabled` | `AiDataSharingAllowed` | Sonuç (`GET /agents/status`) |
| --- | --- | --- |
| `false` | — | Hepsi kapalı |
| `true` | `false` | Metni dış modele gönderen bütün yardımcılar kapalı (tutanak, risk, e-posta); **ses kaydını metne çevirme açık**, çünkü kayıt servisin makinesinde işlenir |
| `true` | `true` | Hepsi bugünkü gibi |

Backend, kapalı bir yardımcı çağrılırsa servisi çağırmadan `503` döner ("Yapay zekâ yardımcıları şirketiniz için kapalı.").

**Uygulandı (T6).** Kural `Isdlc.Application/Agents/CompanyAiRules.cs` (saf sınıf), kullanımı `AgentService`:

- Şirket, giriş yapan kullanıcının şirketidir (`ICurrentUserService.TenantId`). Giriş yoksa ya da şirket bulunamazsa iki
  anahtar da kapalı sayılır.
- `GET /agents/status`: tutanak, risk ve e-posta yardımcıları servis açık **ve** iki anahtar açıksa; ses kaydı servis açık,
  ses paketi kurulu **ve** `AiEnabled` açıksa görünür. Ekranlar bugünkü gibi yalnızca bu cevaba bakar.
- Yardımcılar, giriş ve proje kontrollerinden sonra, servisi çağırmadan önce şirketin anahtarlarına bakar.
- Ithinka'da iki anahtar da açık (migration); yeni şirkette ikisi de kapalı başlar.
- Anahtarları Şirket Yöneticisi `GET` / `PUT /tenants/current/settings` ile okur ve değiştirir (`TenantsController`,
  `Isdlc.Application/Tenants/TenantSettingsService.cs`). Uygulandı (T6, T4'ün `IsTenantAdmin`'i gelince).
  - Yalnızca kendi şirketinin anahtarları; şirket isteğin kullanıcısından gelir, adreste id yoktur.
  - Şirket Yöneticisi değilse ve Platform Yöneticisi için (şirketi yok) `403` "Bu işlem için şirket yöneticisi olmalısınız.";
    giriş yoksa `403` "İşlem yapmak için giriş yapılmalıdır."
  - `PUT` iki anahtarı da ister; eksik olan `400` olur ("Yapay zekâ ayarı belirtilmelidir.", "Veri gönderme izni
    belirtilmelidir."), kapalı sayılmaz. İki anahtar bağımsız kaydedilir; yalnızca veri izni açıksa yine hepsi kapalıdır.
  - Değişiklik bir sonraki `GET /agents/status` isteğinde görünür. `Tenant` denetlenen bir kayıt olmadığından değişiklik denetim kaydına yazılmaz.
  - Ekranı (`/admin/settings`) T5'tedir.

## 8. Hata mesajları

| Durum | Kod | `detail` |
| --- | --- | --- |
| Şirket yöneticisi gereken işlem | `403` | "Bu işlem için şirket yöneticisi olmalısınız." |
| Platform yöneticisi gereken işlem | `403` | "Bu işlem yalnızca platform yöneticisine açıktır." |
| Parola değiştirilmeden işlem | `403` | "Devam etmek için parolanızı değiştirin." |
| E-posta başka bir kullanıcıda (herhangi bir şirkette) | `409` | "Bu e-posta ile bir kullanıcı zaten var." |
| Şirket kodu kullanılıyor | `409` | "Bu şirket kodu kullanılıyor." |
| Şirket kodu biçimi | `400` | "Şirket kodu küçük harf, rakam ve tireden oluşmalıdır (2–50 karakter)." |
| Son aktif şirket yöneticisini pasife alma ya da yöneticiliğini kaldırma | `400` | "Şirketin en az bir aktif yöneticisi olmalıdır." |
| Kendi hesabını pasife alma | `400` | "Kendi hesabınızı pasife alamazsınız." |
| Geçici ya da yeni parola kısa | `400` | "Parola en az 12 karakter olmalıdır." |
| Yeni parola eskisiyle aynı | `400` | "Yeni parola eskisinden farklı olmalıdır." |
| Parola değiştirirken mevcut parola yanlış | `400` | "Mevcut parola hatalı." |
| Formdan `PlatformAdmin` rolü verilmek istendi | `400` | "Platform yöneticisi rolü verilemez." |
| Başka şirketin kullanıcısı ya da olmayan şirket | `404` | "Kullanıcı bulunamadı." / "Şirket bulunamadı." |
| Yapay zekâ yardımcıları kapalı | `503` | "Yapay zekâ yardımcıları şirketiniz için kapalı." |
| Pasif şirket ya da kullanıcı | `401` | Girişteki genel mesaj |

## 9. Bildirim, denetim kaydı, yapay zekâ servisi

- `Notification` ve `AuditLog` `TenantId` taşır. `AuditLogInterceptor` kaydı değişen entity'nin şirketiyle yazar.
- Bildirim yalnızca aynı şirketin kullanıcılarına gider; alıcı filtre sayesinde zaten başka şirketten bulunamaz.
- `ai/` servisi değişmez: durumsuzdur, veritabanına erişmez, her istek tek bir şirketin metnini taşır. Şirket bilgisi
  servise **gönderilmez**.

## 10. Testler

- `DatabaseFixture` iki şirketle açılır: **Ithinka** (bugünkü seed) ve **Deneme A.Ş.** (şablon kopyası ve kendi kullanıcıları;
  `database.DenemeId`, kullanıcılar `TestTenants`'ta). Fixture, Deneme'yi ve pasif bir şirketi (`database.InactiveTenantId`)
  açtıktan sonra açılış seed'ini ikinci kez çalıştırır; böylece testler gerçek açılış döngüsünü sınar. Uygulandı (T3).
- **Şirketi kapsam belirler:** `database.CreateScope(tenantId)`. Testler DbContext'i fixture'ın açtığı kapsamdan alır;
  filtrenin şirketi de bu kapsamdaki `ITenantContext`'ten gelir. Servise verilen `TestUser` filtreyi etkilemez (bugün
  fixture'da kullanıcı tek bir `FixtureCurrentUser`'dan geliyor). Bu yüzden şirket `TestUser`'a değil, kapsama verilir;
  mevcut `database.CreateScope()` Ithinka'yı kullanır ve bugünkü testler değişmeden çalışır.
- `TestUser` şirket alır: `new TestUser(userId, tenantId)`. Bu, servisin `ICurrentUserService.TenantId`'yi okuduğu yerler
  içindir; kapsamın şirketiyle aynı verilir.
- `TestProjects.CreateAsync(database, "4", tenantId: …)` ve `TestUsers.CreateAsync(…, tenantId: …)`.
- `database.CreateScope(tenantId)` kayıtları o şirketin ilk aktif kullanıcısı olarak kaydeder (oluşturan, güncelleyen,
  denetim kaydı); API'de olduğu gibi oluşturan her zaman şirketin kendi kullanıcısıdır.
- **Her modül için ayrım testi (T7):** Erva → Baran'ın modülleri uygulandı: `Tenants/RiskIsolationTests`,
  `QuestionBankIsolationTests`, `IdentityNotificationIsolationTests` (arama ve kişi profili: #163).
  Dide → Erva'nın modülleri uygulandı: `Tenants/OpenIssueMeetingIsolationTests` (açık konular, tutanaklar),
  `ProcessIsolationTests` (akış kopyaları, süreç, "Hayır" dönüşü, süreç iyileştirme ve `SI-` sayacı),
  `AuditAgentIsolationTests` (Kayıt Geçmişi, zaman çizelgesi, yapay zekâ yardımcıları).
  Erva → Baran'ın T4'ü uygulandı: `Identity/AuthServiceTests` ve `AccountStatusServiceTests` sonundaki çapraz testler
  (başka şirketin yöneticisiyle giriş, pasif şirkette doğru ve yanlış parolanın aynı cevabı, şirket kapsamında parola
  değiştirme, parola alanı mesajları, şirketsiz eski token, şirketli Platform Yöneticisi token'ı, kullanıcıyı başka şirkete
  taşımanın reddi).
  1. B şirketinin kullanıcısı A'nın kaydını listede görmez, detayında `404` alır, güncelleyemez.
  2. B, A'nın kullanıcısını sorumlu / analist / yönetici seçemez (mevcut "aktif kullanıcı değil" mesajı).
  3. İki şirkette sayaçlar ayrı ilerler (ikisinin de 1 numaralı projesi olur).
  4. Panel, zaman çizelgesi, denetim kaydı ve bildirim yalnızca kendi şirketini sayar.
- Saf testler: `TenantInterceptor` (Bölüm 5 tablosu), şirket kodu biçimi, yapay zekâ ayar tablosu (Bölüm 7).

## 11. Ekranlar (T5)

Uygulandı. Giriş cevabındaki `mustChangePassword` ve `user.tenant` / `isTenantAdmin` oturumda tutulur; yönlendirmeyi
`layoutChildGuard` yapar (geçici parola → `/change-password`, Platform Yöneticisi → yalnızca `/platform/...`), yönetim
sayfalarını `tenantAdminGuard` korur. Menü `navGroupsFor` ile kişiye göre çıkar. Geçici parolayı form üretir (değiştirilebilir) ve
kayıttan sonra yöneticiye bir kez gösterir. Ekranlar: `features/identity` (parola, kullanıcılar), `features/tenants` (şirketler, ayarlar).

| Ekran | Kim | İçerik |
| --- | --- | --- |
| Üst bar | Herkes | Şirket adı |
| Parola değiştir (`/change-password`) | Herkes | Geçici parolayla girişte zorunlu |
| Şirketler (`/platform/tenants`) | Platform Yöneticisi | Liste, yeni şirket formu (ilk yöneticiyle), pasife al. Platform Yöneticisi menüde yalnızca bunu görür |
| Kullanıcılar (`/admin/users`) | Şirket Yöneticisi | Liste (pasifler dahil), ekle, düzenle, geçici parola ver |
| Şirket ayarları (`/admin/settings`) | Şirket Yöneticisi | Yapay zekâ açık / kapalı, veri gönderme izni ve ne anlama geldiği |

## 12. AGENTS.md'ye eklenecekler (T2 ile)

- **Bölüm 6:** `Tenant`, `TenantId`, `ITenantOwned`, `TemplateCode` / `IsCustomized`, `PlatformAdmin`, `IsTenantAdmin`, `MustChangePassword`.
- **Bölüm 7:** Bölüm 8'deki yeni hata mesajları.
- **Bölüm 8:** "Her yeni entity `ITenantOwned` uygular; `TenantId`'yi interceptor doldurur. `IgnoreQueryFilters` yalnızca
  Bölüm 3'teki yerlerde ve `// tenant:` yorumuyla. Uygulama kodunda ham SQL yok. İstekten gelen id yabancı anahtara
  okunmadan yazılmaz."
- **Bölüm 9:** iki şirketli fixture, `database.CreateScope(tenantId)` ve `TestUser(userId, tenantId)`, her yeni modül için ayrım testi.

## 13. Açık konular

1. Bölüm 1'deki **Şirket Yöneticisi işareti** planın karar 11'ini değiştiriyor (ayrı rol yerine işaret); onay gerekiyor.
2. **Platform Yöneticisinin canlı ortamda açılması:** `Platform:AdminEmail` yapılandırmasıyla ilk açılışta mı, yoksa
   kurulum komutuyla mı? Öneri: yapılandırma; parola ilk girişte değiştirilir.
3. **Şirket kodu nerede görünür?** Şimdilik yalnızca yönetim ekranında; adreslerde kullanılmaz (karar 5).
4. **Bir kullanıcı birden fazla şirkette:** desteklenmez (planın açık konu 4'ü).
5. **Başka şirketin kaydına bağlanmanın veritabanında da engellenmesi (sonraki faz, isteğe bağlı):** bugün bunu
   "id'yi yazmadan önce oku" kuralı (Bölüm 5) ve T7 testleri önlüyor. Kritik ilişkilerde `(TenantId, Id)` üzerinden
   bileşik yabancı anahtar, kuralı veritabanı düzeyinde zorunlu kılar; bütün ilişkileri değiştirdiği için Faz 6'ya alınmadı.
