# Çok Şirketli Yapı — Sözleşme

Tek kurulumun birden fazla şirkete (tenant) hizmet etmesi: veri modeli, kimlik, okuma ve yazma kuralları, seed, yönetim
uç noktaları ve testler. Plan ve iş dağılımı: [phase-6-plan.md](phase-6-plan.md).
Durum: **taslak, ekip onayı bekliyor** (23 Kasım 2026). Sorumlu: Erva (T1). Uygulama: T2–T7.

Bu belge onaylanınca kurallar T2 ile birlikte AGENTS.md'ye (Bölüm 6, 7, 8, 9) taşınır (Bölüm 12).

## 1. Kavramlar

| Türkçe | Kodda | Açıklama |
| --- | --- | --- |
| Şirket | `Tenant` | Uygulamayı kullanan kurum. Kendi kullanıcıları, akışları, soru listeleri, doküman türleri ve kayıtları vardır |
| Şirket kimliği | `TenantId` | Şirkete ait her satırda bulunur |
| Şirkete ait kayıt | `ITenantOwned` | `TenantId` taşıyan her entity bu arayüzü uygular |
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
`DocumentTypes`, `ProjectDocuments`, `Risks`, `ProjectAssessments`, `Notifications`, `QuestionSets`, `Questions`,
`QuestionOptions`, `Questionnaires`, `QuestionAnswers`, `ProcessImprovements`.

- `TenantId` int, zorunlu, `Tenants.Id`'ye `Restrict` yabancı anahtar ve indeksli.
- **Tek istisna:** `Users.TenantId` yalnızca Platform Yöneticisi için boştur. Kural veritabanında da tutulur:
  `CHECK ((Role = 'PlatformAdmin' AND TenantId IS NULL) OR (Role <> 'PlatformAdmin' AND TenantId IS NOT NULL))`.
- Alt kayıtlar (risk, açık konu, adım kaydı…) da `TenantId` taşır, projeden türetilmez. Böylece her sorgu kendi
  tablosunda filtrelenir; filtrenin bir join'e bağlı olması gerekmez.

### Benzersizlik

| Bugün | Faz 6'da |
| --- | --- |
| `Projects.Code` | `(TenantId, Code)` |
| `ProcessFlows.Code` | `(TenantId, Code)` |
| `DocumentTypes.Code` | `(TenantId, Code)` |
| `Users.Email` | **Değişmez**, bütün sistemde benzersiz (giriş şirketi e-postadan bulur, Bölüm 4) |
| Proje içi kodlar (`(ProjectId, Code)`), `(ProcessFlowId, …)`, `ProjectId` benzersizlikleri | Değişmez; üst kayıt zaten tek şirkete ait |

### Şablondan gelen kayıtlar

`ProcessFlow`, `DocumentType` ve `QuestionSet` iki alan alır:

| Alan | Tür | Kural |
| --- | --- | --- |
| `TemplateCode` | string? | Kopyalandığı şablonun kodu (`new-software-project`, `level`…); şirketin kendi eklediği kayıtta boş |
| `IsCustomized` | bool | Şirket bu kaydı (veya akışın aşama / adımlarını, setin sorularını) değiştirdiyse `true`. Faz 6'da düzenleme ekranı olmadığı için hep `false`; alan sonraki faz için şimdiden eklenir |

### Mevcut verinin taşınması (tek migration)

1. `Tenants` tablosu açılır ve **Ithinka** (`Id = 1`, `Code = "ithinka"`, `AiEnabled` ve `AiDataSharingAllowed` bugünkü
   davranış korunsun diye `true`) eklenir.
2. Bütün tablolara `TenantId` boş olarak eklenir, bütün satırlar `1` yapılır, sonra kolon zorunlu olur.
3. Benzersizlik indeksleri yukarıdaki tabloya göre yeniden kurulur.
4. `TemplateCode` bugünkü akış, doküman türü ve soru seti kodlarıyla doldurulur; `IsCustomized = false`.

Canlı veri yok; migration yine de geri alınabilir yazılır (`Down`).

## 3. Okuma kuralı: global sorgu filtresi

`IsdlcDbContext`, `ITenantOwned` olan her entity için şu filtreyi ekler:

```csharp
builder.Entity<T>().HasQueryFilter(e => e.TenantId == _tenant.TenantId);
```

- `_tenant` istek başına çözülen `ITenantContext`'tir (Bölüm 4). Giriş yapılmamışsa ya da kullanıcı Platform Yöneticisiyse
  `TenantId` boştur ve filtre **hiçbir satır döndürmez**: Platform Yöneticisi şirket verisini yapısal olarak göremez.
- Servisler değişmez; `dbContext.Projects` zaten yalnızca kullanıcının şirketini görür. Sayaçlar ("en büyük + 1",
  `RecordCodes.NextNumberAsync`) da kendiliğinden şirket içinde sayar.
- **`IgnoreQueryFilters()` yalnızca şu yerlerde** kullanılır; her kullanımın üstüne `// tenant: <neden>` yorumu yazılır ve
  PR açıklamasında belirtilir:

| Yer | Neden |
| --- | --- |
| Giriş (`AuthService`) | E-postadan kullanıcıyı ve şirketini bulmak; o anda şirket bilinmiyor |
| Seed ve açılış eşitlemesi (`DatabaseSeeder`) | Bütün şirketlerin şablon kopyalarını eşitlemek |
| Şirket yönetimi (`TenantService`, Platform Yöneticisi) | Yeni şirkete şablon kopyalamak ve ilk yöneticisini açmak; yalnızca `Tenants` ve o şirketin seed tabloları |
| E-posta benzersizliği kontrolü | E-posta bütün sistemde benzersiz |

- **Ham SQL** (`FromSql`, `ExecuteSql`) uygulama kodunda kullanılmaz; filtreyi atlar. Yalnızca migration'larda.
- `ExecuteUpdate` / `ExecuteDelete` filtreye uyar; yine de bu PR'larda şirket testi yazılır.

## 4. Kimlik: şirket nereden gelir

### Giriş

`POST /api/v1/auth/login` değişmez (`{ email, password }`). Akış:

1. Kullanıcı e-postayla bulunur (bütün şirketlerde, `IgnoreQueryFilters`).
2. Kullanıcı pasifse, şirketi pasifse ya da parola yanlışsa **aynı 401** döner ("E-posta veya parola hatalı.");
   pasif şirketin varlığı dışarıya söylenmez.
3. Token'a mevcut claim'lere ek olarak `tenant` (şirket `Id`'si, Platform Yöneticisinde yok) ve `tenantAdmin` (`true`/`false`) yazılır.
4. Cevaptaki `user` nesnesine `tenant: { id, code, name }` (Platform Yöneticisinde `null`) ve `isTenantAdmin` eklenir.

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

- **Pasife alınan şirket:** her istekte şirketin aktif olduğu kontrol edilir (1 dakika önbellekli). Pasifse `401`
  döner ve ekran girişe yönlenir. Böylece açık oturumlar token süresini beklemeden kapanır.
- **Pasife alınan kullanıcı:** bugünkü davranış aynen kalır.

### Parola

Bugün parolalar yalnızca seed'den (`Identity:TestPassword`) geliyor. Yeni şirket ve kullanıcı açılabilmesi için:

- Yeni kullanıcıyı açan (Platform ya da Şirket Yöneticisi) bir **geçici parola** verir (en az 12 karakter).
- Kullanıcıda `MustChangePassword = true` olur. Giriş başarılı olur ama cevapta `mustChangePassword: true` döner; ekran
  parola değiştirme sayfasına yönlenir. Parola değişene kadar `POST /auth/change-password` dışındaki her istek `403` alır
  ("Devam etmek için parolanızı değiştirin.").
- `POST /api/v1/auth/change-password` (`{ currentPassword, newPassword }`): yeni parola en az 12 karakter ve
  eskisinden farklı olmalıdır. Başarılıysa yeni token döner.
- Seed kullanıcıları ve testler etkilenmez (`MustChangePassword = false`).

## 5. Yazma kuralı: `TenantInterceptor`

`AuditableEntityInterceptor`'ın yanına yeni bir `SaveChangesInterceptor`:

| Durum | Davranış |
| --- | --- |
| Yeni `ITenantOwned` kayıt, `TenantId` boş (0) | `ITenantContext.TenantId` yazılır |
| Yeni kayıt, `TenantId` dolu ve mevcut şirketten farklı | **Hata** (`InvalidOperationException` → 500, loga yazılır). Yalnızca seed ve şirket yönetimi başka şirkete yazabilir; onlar `ITenantContext`'i o şirkete ayarlar |
| Var olan kaydın `TenantId`'si değiştirilmiş | **Hata** (aynı) |
| Şirket bilinmiyor (girişsiz, Platform Yöneticisi) ve `ITenantOwned` kayıt ekleniyor | **Hata** (aynı) |

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
değişiklikleri dosyadan; adım eklemek, çıkarmak ya da sıralamak migration ister ve migration **bütün şirketlerin** kopyasını değiştirir.
Soru setleri ve doküman türleri için de aynı.

**Yeni şirket açmak** (`POST /tenants`) tek bir işlemde (transaction): şirket satırı, şablonların kopyası ve ilk Şirket
Yöneticisi. Biri başarısız olursa hiçbiri kaydedilmez.

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
| `GET /users?includeInactive=true` | Bugünkü kullanıcı listesi; yönetici pasifleri de görebilir | `200` |
| `POST /users` | Şirkete kullanıcı ekler (ad, e-posta, rol, şirket yöneticisi mi, geçici parola) | `201` `UserDto` |
| `PUT /users/{id}` | Ad, rol, aktiflik, şirket yöneticisi işareti | `200` `UserDto` |
| `POST /users/{id}/reset-password` | Geçici parola verir, `MustChangePassword = true` | `204` |
| `GET /tenants/current/settings` · `PUT /tenants/current/settings` | `{ aiEnabled, aiDataSharingAllowed }` | `200` |

- Kullanıcının e-postası sonradan değişmez (giriş kimliği).
- Rol `PlatformAdmin` verilemez; Platform Yöneticisi yalnızca yapılandırmayla açılır.

### Yapay zekâ ayarlarının etkisi

| `AiEnabled` | `AiDataSharingAllowed` | Sonuç (`GET /agents/status`) |
| --- | --- | --- |
| `false` | — | Hepsi kapalı |
| `true` | `false` | Metni dış modele gönderen bütün yardımcılar kapalı (tutanak, risk, e-posta); **ses kaydını metne çevirme açık**, çünkü kayıt servisin makinesinde işlenir |
| `true` | `true` | Hepsi bugünkü gibi |

Backend, kapalı bir yardımcı çağrılırsa servisi çağırmadan `503` döner ("Yapay zekâ yardımcıları şirketiniz için kapalı.").

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
| Yapay zekâ yardımcıları kapalı | `503` | "Yapay zekâ yardımcıları şirketiniz için kapalı." |
| Pasif şirket ya da kullanıcı | `401` | Girişteki genel mesaj |

## 9. Bildirim, denetim kaydı, yapay zekâ servisi

- `Notification` ve `AuditLog` `TenantId` taşır. `AuditLogInterceptor` kaydı değişen entity'nin şirketiyle yazar.
- Bildirim yalnızca aynı şirketin kullanıcılarına gider; alıcı filtre sayesinde zaten başka şirketten bulunamaz.
- `ai/` servisi değişmez: durumsuzdur, veritabanına erişmez, her istek tek bir şirketin metnini taşır. Şirket bilgisi
  servise **gönderilmez**.

## 10. Testler

- `DatabaseFixture` iki şirketle açılır: **Ithinka** (bugünkü seed) ve **Deneme A.Ş.** (şablon kopyası ve kendi kullanıcıları).
- `TestUser` şirket alır: `new TestUser(userId, tenantId)`; mevcut `new TestUser(userId)` Ithinka'yı kullanır, bugünkü testler değişmeden çalışır.
- `TestProjects.CreateAsync(database, "4", tenantId: …)` ve `TestUsers.CreateAsync(…, tenantId: …)`.
- **Her modül için ayrım testi (T7):**
  1. B şirketinin kullanıcısı A'nın kaydını listede görmez, detayında `404` alır, güncelleyemez.
  2. B, A'nın kullanıcısını sorumlu / analist / yönetici seçemez (mevcut "aktif kullanıcı değil" mesajı).
  3. İki şirkette sayaçlar ayrı ilerler (ikisinin de 1 numaralı projesi olur).
  4. Panel, zaman çizelgesi, denetim kaydı ve bildirim yalnızca kendi şirketini sayar.
- Saf testler: `TenantInterceptor` (Bölüm 5 tablosu), şirket kodu biçimi, yapay zekâ ayar tablosu (Bölüm 7).

## 11. Ekranlar (T5)

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
- **Bölüm 9:** iki şirketli fixture, `TestUser(userId, tenantId)`, her yeni modül için ayrım testi.

## 13. Açık konular

1. Bölüm 1'deki **Şirket Yöneticisi işareti** planın karar 11'ini değiştiriyor (ayrı rol yerine işaret); onay gerekiyor.
2. **Platform Yöneticisinin canlı ortamda açılması:** `Platform:AdminEmail` yapılandırmasıyla ilk açılışta mı, yoksa
   kurulum komutuyla mı? Öneri: yapılandırma; parola ilk girişte değiştirilir.
3. **Şirket kodu nerede görünür?** Şimdilik yalnızca yönetim ekranında; adreslerde kullanılmaz (karar 5).
4. **Bir kullanıcı birden fazla şirkette:** desteklenmez (planın açık konu 4'ü).
