# Faz 6 — Çok şirketli yapı (multi-tenant)

Bu dosya Faz 6'da yapılacak işleri, kimin neyi yapacağını ve işlerin sırasını anlatır.
Ortak kurallar için: [AGENTS.md](../AGENTS.md), ekip ve çalışma kuralları için: [PLANNING.md](../PLANNING.md).
Durum: **taslak, ekip onayı bekliyor** (23 Kasım 2026).

## 1. Neden

Uygulama bugün tek bir şirket için kuruluyor. Bir kurulumun birden fazla şirkete hizmet etmesi isteniyor: her şirketin kendi
kullanıcıları, kendi akışları, soru listeleri ve doküman türleri olur; hiçbir şirket başka bir şirketin kaydını göremez.

Faz 6'nın amacı **ayrımı** kurmaktır. Şirketlerin rollerini, atlama koşullarını ve akışlarını ekrandan düzenleyebilmesi
ayrı bir iştir ve sonraki faza bırakılır (Bölüm 7).

## 2. Kararlar

Önerilen kararlar; ekip onaylayınca bu tablo kesinleşir.

| # | Konu | Öneri | Neden |
| --- | --- | --- | --- |
| 1 | Verinin ayrımı | **Tek veritabanı, her tabloda `TenantId`** | Tek kurulum, tek migration, tek yedek. Şirket başına veritabanı her şirkette ayrı migration ve bağlantı yönetimi ister |
| 2 | `TenantId` hangi tablolarda | **Bütün tablolarda**, alt kayıtlar dahil (risk, açık konu, adım kaydı…) | Her sorgu kendi tablosunda filtrelenir; filtre için başka tabloya join gerekmez, unutulan bir join veri sızdırmaz |
| 3 | `TenantId` nasıl dolar | Kaydederken interceptor doldurur (`AuditableEntityInterceptor` gibi); kod elle atamaz. Başka şirketin kaydına bağlanmaya çalışan kayıt reddedilir | Kodda unutulamaz |
| 4 | Okurken ayrım | EF **global sorgu filtresi** (`HasQueryFilter(e => e.TenantId == currentTenant)`); `IgnoreQueryFilters()` yalnızca platform yönetiminde ve açılış seed'inde, gerekçesiyle | Bütün servisler değişmeden ayrılır |
| 5 | Şirket nereden anlaşılır | **Kullanıcıdan.** E-posta bütün sistemde benzersiz kalır; kullanıcı tek bir şirkete aittir. Giriş, kullanıcının şirketini JWT'ye `tenant` claim'i olarak yazar. Alt alan adı (`ithinka.isdlc...`) gerekmez | Giriş ekranı ve adresler değişmez |
| 6 | Mevcut veri | Bir migration, bugünkü bütün kayıtları ilk şirkete (**Ithinka**) taşır | Canlı veri yok ama geliştirme ve demo verisi korunur |
| 7 | Seed ve ayar tabloları | Akışlar, aşamalar, adımlar, dönüşler, doküman türleri ve soru setleri **şirkete ait** olur. `Seed/` dosyaları **şablon** olur: yeni şirket açılınca şablondan kopyalanır; sonra şirket kendi kopyasını değiştirebilir | Her şirketin akışı farklı olabilmeli |
| 8 | Açılışta seed eşitleme | Şablon değişikliği yalnızca şirketin **değiştirmediği** kopyalara uygulanır (kayıt bazında `IsCustomized`). Kendi akışını değiştirmiş şirket etkilenmez | Şirketin değişikliği açılışta ezilmez |
| 9 | Kodlar ve sayaçlar | Proje No, `SI-001` gibi kodlar **şirket içinde** sırayla verilir; benzersizlik `(TenantId, Code)` olur. Proje içi kodlar (`AK-001`, `RSK-001`) zaten proje bazında, değişmez | İki şirketin de 1 numaralı projesi olabilir |
| 10 | Platform yönetimi | Yeni rol **Platform Yöneticisi**: hiçbir şirkete ait değildir, yalnızca şirket açar / pasife alır ve şirketin ilk yöneticisini oluşturur. Şirketlerin proje verisini **göremez** | Şirket verisine erişen tek kişi şirketin kendisi olur |
| 11 | Şirket içi yönetim | **Şirket Yöneticisi** bir rol değil, kullanıcıdaki işarettir (`IsTenantAdmin`): kendi şirketinin kullanıcılarını ekler, pasife alır, rol verir. Yeni kullanıcı geçici parolayla açılır, ilk girişte değiştirir | Bugün kullanıcılar seed'den geliyor; şirket kendi kullanıcısını yönetebilmeli. Kullanıcı başına tek rol kuralı yüzünden ayrı rol olsaydı bir İş Analisti yönetici olamazdı |
| 12 | Yapay zekâ | Servis ve servis anahtarı ortak kalır; her şirketin **açık / kapalı** ayarı ve **veri gönderme izni** olur. Model anahtarı şirket başına ayrılmaz (sonraki faz) | Veri gönderme kararı şirkete göre değişir |
| 13 | Bildirim, denetim kaydı | `Notification` ve `AuditLog` de `TenantId` taşır; denetim kaydı şirket dışına çıkmaz | Karar 2 ile aynı |

## 3. Etkilenen yerler

| Alan | Bugün | Faz 6'da |
| --- | --- | --- |
| Tablolar (23) | Şirket bilgisi yok | Hepsinde `TenantId` (karar 2) ve yeni `Tenants` tablosu |
| Benzersizlik | `Project.Code`, `ProcessFlow.Code`, `DocumentType.Code`, `QuestionSet.Code`, `Question.Key`, `QuestionOption.Key` bütün sistemde benzersiz | `(TenantId, Code)` / `(TenantId, Key)` |
| Kod numaraları | `RecordCodes.NextNumberAsync` ve servislerdeki "en büyük + 1" sorguları tüm sistemde sayar | Global filtre sayesinde şirket içinde sayar; testle doğrulanır |
| Akış kodları | Servislerde `ProcessFlowCodes.NewRequest` gibi sabitler | Kod şirket içinde aynı kalır (`new-software-project`), sorgu filtre sayesinde şirketin akışını bulur |
| Seed | Tablo boşsa dosyadan yüklenir; akış her açılışta dosyayla eşitlenir | Şablon → şirket kopyası (karar 7, 8) |
| Giriş | JWT'de `sub` | JWT'de `sub` + `tenant`; `ICurrentUserService.TenantId` |
| Roller | `UserRole` 6 sabit rol | + `PlatformAdmin` rolü ve `IsTenantAdmin` işareti (karar 10, 11). Şirkete özel roller sonraki faz |
| Testler | Tek şirket | Her modül için "A şirketi B'nin kaydını göremez ve değiştiremez" testi |

## 4. Yapılacak işler

Her satır en az bir PR'dır. Sıra aynı: önce sözleşme, sonra entity ve migration, sonra API, en son ekran.
Sorumlular **öneridir**; modül sahipliğine göre dağıtıldı, ekip onaylarken değiştirebilir.

| No | İş | Sorumlu | Bağımlılık |
| --- | --- | --- | --- |
| T1 | **Sözleşme:** [multi-tenancy.md](multi-tenancy.md) — Bölüm 2'deki kararlar, `Tenant` entity'si, JWT claim'i, interceptor ve filtre kuralları, `IgnoreQueryFilters` kuralı, parola, yönetim uç noktaları, yeni hata mesajları. Kurallar T2 ile AGENTS.md'ye taşınır | Erva | — |
| T2 | **Ayrım altyapısı:** `Tenant` entity'si; bütün tablolara `TenantId`; mevcut veriyi Ithinka'ya taşıyan migration; `TenantId`'yi dolduran interceptor; global sorgu filtresi; `ICurrentUserService.TenantId` ve JWT'ye `tenant` claim'i; benzersizlik ve sayaçların şirkete göre ayrılması (karar 9). **Kritik yol:** bütün tablolara dokunduğu için tek PR'da ve diğer işler durmuşken yapılır | Dide | T1 |
| T3 | **Şirket başına seed:** `Seed/` dosyaları şablon olur; yeni şirkete şablondan kopya; açılış eşitlemesi yalnızca şirketin değiştirmediği kopyalara (karar 7, 8); test ve örnek veri yardımcıları (`TestProjects`, `TestUsers`) şirket alır | Erva | T2 |
| T4 | **Platform ve şirket yönetimi (backend):** `PlatformAdmin` rolü ve `IsTenantAdmin` işareti; geçici parola ve parola değiştirme; şirket aç / pasife al; şirketin ilk yöneticisi; şirket içinde kullanıcı ekle, pasife al, rol ver. Pasif şirketin kullanıcıları giriş yapamaz | Baran | T2, T3 |
| T5 | **Yönetim ekranları:** Platform Yöneticisi için şirket listesi ve yeni şirket formu; Şirket Yöneticisi için kullanıcı listesi ve formu; üst barda şirket adı | Baran | T4 |
| T6 | **Yapay zekâ ayarları:** şirket bazında açık / kapalı ve veri gönderme izni (karar 12); `GET /agents/status` şirkete göre döner | Erva | T2 |
| T7 | **Ayrım testleri:** her modül için iki şirketli testler (listeleme, detay, güncelleme, sayaç, bildirim, denetim kaydı, panel, arama). Çapraz test kuralıyla dağıtılır | Herkes | T2–T6 |
| T8 | **Belgeler ve demo:** README'ye yeni şirket açma; `docs/data-model.md`; demo senaryosuna ikinci bir şirket | Erva | Hepsi |

### Çapraz testler

Kural aynı kalır (PLANNING.md → Birlikte çalışma kuralları): Erva → Baran'ın, Baran → Dide'nin, Dide → Erva'nın işleri.
T7'de her kişi **başkasının modüllerinin** ayrım testlerini yazar: bir sızıntıyı yazanın değil, başkasının bulması daha olasıdır.

## 5. Sıra

```
Önce          Faz 5'in açık işleri birleşir (F7 genel arama, F8 bağlantı, F9 kişi profili)
Dalga 1       T1 sözleşme
Dalga 2       T2 ayrım altyapısı (diğer kod PR'ları bu sırada beklemeye alınır)
Dalga 3       T3 şirket başına seed · T6 yapay zekâ ayarları
Dalga 4       T4 yönetim (backend) → T5 yönetim ekranları
Dalga 5       T7 ayrım testleri · T8 belgeler ve demo
```

- **T2 bütün tablolara ve migration'lara dokunur.** Başka PR'larla çakışmaması için Faz 5'in açık işleri önce birleşir;
  T2 açıkken yeni migration üreten PR açılmaz. T2 birleşince herkes main'i alır.
- Faz 5'in demo işi (F10) T8 ile birleştirilir: demo bir kez, iki şirketle yazılır.

## 6. Riskler

| Risk | Önlem |
| --- | --- |
| Bir sorgu filtreyi atlar, başka şirketin verisi görünür | Global filtre (karar 4); `IgnoreQueryFilters` kullanan her yer PR'da gerekçesiyle yazılır; T7 testleri |
| Ham SQL ya da migration içi sorgular filtrelenmez | Ham SQL yalnızca migration'da; uygulama kodunda yasak (AGENTS.md'ye kural) |
| Seed eşitlemesi bir şirketin değiştirdiği akışı ezer | Karar 8: yalnızca şablona bağlı şirketler eşitlenir; testle doğrulanır |
| Bir kayıt başka şirketin kaydına bağlanır (ör. başka şirketin kullanıcısı sorumlu seçilir) | Interceptor, bağlanan kaydın şirketini de kontrol eder (karar 3); servislerdeki "aktif kullanıcı mı" kontrolleri filtre sayesinde şirket dışını bulamaz |
| Yapay zekâ servisine giden metin şirket karıştırır | Servis zaten durumsuz; her istek tek bir şirketin verisini taşır. Şirketin veri izni kapalıysa backend servisi çağırmaz (T6) |

## 7. Sonraki faza bırakılanlar

Faz 6 ayrımı kurar; şirketlerin uygulamayı kendine göre ayarlaması bundan sonraki iştir:

- **Roller veriye:** `UserRole` sabit 6 rol yerine şirketin tanımladığı roller.
- **Atlama koşulları veriye:** bugünkü üç koşul (dış talep, teklif, seviye) yerine şirketin tanımlayabileceği koşullar.
- **Akış düzenleme ekranı:** adım ekleme, çıkarma, sıralama (bugün migration ister).
- **Şirkete özel formlar:** Talep Kayıt Formu gibi alanların şirkete göre değişmesi.
- **Şirket başına yapay zekâ modeli ve anahtarı, kullanım ve maliyet takibi.**

## 8. Açık konular

1. **Kararlar:** Bölüm 2'deki 13 karar ekip onayı bekliyor; en önemlileri 1 (tek veritabanı), 5 (şirket kullanıcıdan anlaşılır) ve 10 (Platform Yöneticisi şirket verisini görmez).
2. **Sorumlular:** Bölüm 4'teki dağılım öneridir.
3. **Faz 5'in kalanları:** F7–F9'un Faz 6'dan önce birleşmesi gerekiyor (Bölüm 5).
4. **Kullanıcı birden fazla şirkette olabilir mi?** Öneri: hayır (karar 5). Danışman gibi birden fazla şirkette çalışan biri her şirkette ayrı e-postayla açılır; gerekirse sonraki faz.
