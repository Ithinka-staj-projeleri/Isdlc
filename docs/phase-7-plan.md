# Faz 7 — Şirkete göre ayarlanabilirlik

Bu dosya Faz 7'de yapılacak işleri, kimin neyi yapacağını ve işlerin sırasını anlatır.
Ortak kurallar için: [AGENTS.md](../AGENTS.md), ekip ve çalışma kuralları için: [PLANNING.md](../PLANNING.md).
Durum: **onaylandı** (26 Kasım 2026, #177). Sözleşme (A1): [configurability.md](configurability.md), taslak. Erva'nın geri bildirimiyle güncellendi:
- Unvan (rol) kalkıyor; yerine yönetim panelinden basit yetkilendirme geliyor.
- Platform şirketi ve yöneticisini açıyor, kullanıcıları ve yetkileri şirket yöneticisi veriyor.
- Süreçte bir adım, işi yapılmadan geçilemiyor.

## 1. Neden

Faz 6 ile aynı kurulumda birbirinden ayrı birden fazla şirket çalışıyor. Ama her şirket bugün Ithinka'nın kurallarıyla ve
kontrolsüz bir süreçle çalışıyor:

- **Unvan zorunlu ve sabit.** Her kullanıcının tek bir rolü (unvanı) var ve kimin ne yapabileceği bu altı unvana gömülü.
  Platform Yöneticisi şirketi açarken ilk yöneticiye de unvan seçiyor.
- **Süreç tıklayarak ilerliyor.** Bir adım, işi yapılmadan "Adımı tamamla" ile geçilebiliyor. Örneğin Talep Kayıt Formu
  doldurulmadan ya da toplantı yapılmadan 3. ve 6. adım bitmiş sayılıyor.
- **Atlama koşulları sabit.** Bir adımın atlanması yalnızca üç koşula bakabiliyor: dış talep mi, teklif hazırlanacak mı,
  seviye kaç.
- **Akışı geliştirici değiştiriyor.** Akışa adım eklemek, çıkarmak ya da sırasını değiştirmek migration istiyor.
- **Form alanları sabit.** Talep Kayıt Formu'nun alanları kodda; şirket kendi alanını ekleyemiyor.
- **Yapay zekâ tek tip.** Bütün şirketler aynı modeli kullanıyor ve kimin ne kadar kullandığı görünmüyor.

Faz 7'nin amacı iki şey:
- Başka bir şirket kendi kullanıcılarını, yetkilerini, akışını ve form alanlarını **kod değişmeden** kurabilsin.
- Süreç, her adımın işi yapılmadan ilerlemesin.

**Bitiş koşulu:** Ekranlarla, kod değişmeden şunlar yapılabiliyor ve demo bunu gösteriyor:
1. Platform Yöneticisi yeni bir şirketi ve şirketin yönetici hesabını açar.
2. Şirket yöneticisi kullanıcılarını ekler ve yetkilerini verir.
3. Şirket yöneticisi akışına bir adım ekler; adımı kimin yapacağını ve tamamlanması için ne gerektiğini seçer.
4. Şirket yöneticisi talep formuna kendi alanını ekler ve bir adımı bu alana göre atlatır.
5. Bir talep bu akışta yürür; işi yapılmamış bir adım geçilemez ve ekran kişiyi o işin yapılacağı yere götürür.

Ithinka'nın akışları aynı yapıda şablon olarak kalır. Bütün testler yeşil.

## 2. Bugün koda gömülü olanlar

| Konu | Bugün | Nerede |
| --- | --- | --- |
| Unvan | `UserRole` enum'ı: 6 unvan + `PlatformAdmin`. Kullanıcı başına tek unvan. `User.Role`, `ProcessStep.ResponsibleRole`, `ApproverRole` bu enum | Backend'de 7 dosya (`ProjectService`, `ProcessProgress`, `DashboardService`, `TeamWorkloadRules`, `AgentService`, `UserAdminService`, `UserRoleNames`); frontend'de 6 dosya (`user-role.ts`, `role.guard.ts`, talep formu ve listeler) |
| Unvana bağlı kurallar | Talebi yalnızca İş Analisti açar; projenin analisti ve proje yöneticisi unvana göre seçilir; talebi yalnızca Yazılım Departman Yöneticisi reddeder / erteler; adımı yapan kişi unvanla eşleşir (`ProcessProgress.CanActOnStep`) | Aynı dosyalar |
| Şirket açma | Platform Yöneticisi ilk yöneticiye unvan seçer (`TenantService`, şirket formu) | `Tenants/TenantService.cs`, `tenant-form` |
| Adımı tamamlama | Yalnızca "doğru unvan mı" kontrolü; adımın işinin yapılıp yapılmadığına bakılmaz (`ProcessService.CompleteStepAsync`, `AnswerStepAsync`) | `Process/ProcessService.cs` |
| Atlama koşulları | `ProcessStep.RequiresExternalRequest`, `RequiresOffer`, `MinLevel`; değerlendirme `StepConditions` (saf sınıf) | `Process/StepConditions.cs`, üç servis |
| Akış yapısı | Seed dosyasından gelir. Metin değişikliği dosyayla yapılır; adım ekleme, çıkarma, sıralama migration ister | `DatabaseSeeder`, data-model.md → Seed verisi |
| Akışın işi | Hangi akışın "yeni talep", "ek geliştirme", "süreç iyileştirme" olduğu kod sabiti (`ProcessFlowCodes`) | 4 servis |
| Form alanları | `Project`'in alanları sabit kolon; Talep Kayıt Formu bunları gösterir | `Project.cs`, talep formu |
| Yapay zekâ modeli | Servisin tek ayarı (`GEMINI_MODEL`); şirket seçemez, kullanım kaydı yok | `ai/isdlc_ai/config.py` |

## 3. Kararlar (öneri)

Ekip onaylayınca uygulama ayrıntıları bir sözleşme dosyasına yazılır (A1).

### Hesaplar ve yetkiler

| # | Konu | Öneri | Neden |
| --- | --- | --- | --- |
| 1 | Unvan | **Unvan kalkar.** `User.Role` ve `UserRole` enum'ı kaldırılır. Kullanıcıda yalnızca ad, e-posta ve **yetkiler** kalır | Kimin ne yapabileceğini unvan değil, verilen yetki belirler. Bir kişi birden fazla işi yapabilir |
| 2 | Yetkiler | **Yetki yalnızca akıştaki işler için istenir.** Platformda sabit ve kısa bir liste; her şirkette aynı. Bir kullanıcıya birden fazla yetki verilebilir. Liste, akışın bugünkü sorumlularından çıkar: <br>• **Analiz adımları** (talebi açmak da akışın ilk adımı olduğu için bu yetkiyle yapılır; projenin analisti olabilir)<br>• **Proje yönetimi adımları** (projenin yöneticisi olabilir)<br>• **Onay ve karar** (onay adımları, talebi reddetme / erteleme)<br>• **Teklif ve anlaşma**<br>• **Geliştirme**<br>• **Test** | Volobox'taki gibi panelden yetki verilir ama rol tasarlama, izin matrisi ya da kaynak bazlı izin yok: bir kişi, bir kutucuk listesi |
| 2a | Akış dışındaki işler | Akışa ait olmayan işler **yetki istemez**; şirketin her kullanıcısı yapar: risk, açık konu, toplantı tutanağı, gereksinim, doküman durumu, seviye ve uygunluk formları, panel, arama, yapay zekâ yardımcıları. Şirket yönetimi bir iş yetkisi değil, kullanıcıdaki **Şirket yöneticisi** işaretidir (`IsTenantAdmin`, Faz 6'daki gibi) | Her ekranda yetki sorulmaz; kontrol yalnızca sürecin ilerlediği yerde olur |
| 2b | Yetkisi olmayan kullanıcı | Kişi yetkisi olmayan bir akış işine geldiğinde düğme gizlenmez; yerine **hangi yetkinin gerektiği ve kimden isteneceği** yazar. Örnek: *"Bu adım için «Onay ve karar» yetkisi gerekir. Şirket yöneticinizden isteyin: Deniz Aydın."* Talep açarken de aynı: *"Talep açmak için «Analiz adımları» yetkisi gerekir…"* Backend aynı mesajla `403` döner. Şirket yöneticilerinin adları şirketin aktif yöneticilerinden gelir | Kişi neden yapamadığını ve kime başvuracağını görür; bugünkü "Bu adım Yazılım Departman Yöneticisi tarafından yapılır." mesajının yerini alır |
| 3 | Adımın sorumlusu | Adımda unvan yerine **yetki** seçilir: "bu adımı kim yapar" (`ResponsiblePermission`) ve onay adımında "kim onaylar" (`ApproverPermission`). Adımda o yetkiye sahip herkes işlem yapabilir. Analiz ve proje yönetimi adımları bugünkü gibi projeye atanan kişiye düşer | Bugünkü davranış korunur; şirket akışında yetkiyi seçer |
| 4 | Şirketi açma | Platform Yöneticisi şirketi açarken yalnızca **şirket yönetici hesabını** oluşturur: ad, e-posta, geçici parola. **Unvan ya da yetki seçmez.** Bu hesap yalnızca Şirket yöneticisi işaretiyle, iş yetkisi olmadan başlar | Platform şirketin iç işine karışmaz |
| 5 | Kullanıcı ve yetki yönetimi | Şirket yöneticisi **Yönetim → Kullanıcılar** ekranından kullanıcı ekler, yetkilerini kutucuklarla verir, pasife alır, geçici parola verir. Kendine de iş yetkisi verebilir. Hız için "hazır yetki setleri" (ör. *İş Analisti*: analiz adımları) tek tıkla kutucukları doldurur; set kaydedilmez, yalnızca doldurur | Yönetim tek yerde ve basit kalır. Hazır setler bugünkü unvanların karşılığıdır, ama zorunlu değildir |
| 6 | Platform Yöneticisi | Kullanıcıda ayrı bir işaret olur (`IsPlatformAdmin`, şirketi yok). Şirket yetkisi alamaz | Faz 6'daki kural aynı kalır; yalnızca enum kalkar |
| 7 | Mevcut veri | Bir migration bugünkü unvanları yetkilere çevirir; adımların sorumlu unvanını yetkiye çevirir. Şirket yöneticisi işareti (`IsTenantAdmin`) olduğu gibi kalır | Ithinka'da kimse yetkisiz kalmaz, davranış değişmez |

Unvana göre yetki eşlemesi (migration ve hazır setler):

| Bugünkü unvan | Yetkiler |
| --- | --- |
| İş Analisti | Analiz adımları |
| Yazılım Departman Yöneticisi | Onay ve karar |
| Proje Yöneticisi | Proje yönetimi adımları |
| Satış Temsilcisi | Teklif ve anlaşma |
| Yazılım Geliştirici | Geliştirme |
| Test Uzmanı | Test |

### Süreç kontrolü

| # | Konu | Öneri | Neden |
| --- | --- | --- | --- |
| 8 | Adımın ön koşulu | Her adımın isteğe bağlı bir **ön koşulu** olur: adım tamamlanmadan (ya da "Evet" denmeden) önce projede olması gereken iş. Ön koşul sağlanmadıysa backend adımı reddeder (`400`, Türkçe mesaj). Ön koşul türleri:<br>• Talep Kayıt Formu'nun zorunlu alanları dolu<br>• Bir form tamamlandı (müşteri uygunluğu, seviye, talep analizi, risk analizi)<br>• Seviye projeye uygulandı<br>• En az bir toplantı tutanağı var<br>• En az bir risk kaydı var<br>• Gereksinimler onaylandı<br>• Bir doküman "Onaylandı" durumunda | "Tık tık" ilerleme biter; adım ancak işi yapılınca geçer |
| 9 | Yönlendirme | Ön koşulu sağlanmamış adımda "Adımı tamamla" yerine ne eksik olduğu ve oraya giden düğme görünür. Örnek: *"Önce müşteri uygunluk anketini tamamlayın → Seviye & Uygunluk"*. Talep Kayıt Formu eksikse düğme talep düzenleme ekranına götürür | Kişi ne yapacağını ve nereye gideceğini görür |
| 10 | Ithinka'nın ön koşulları | Şablona eklenir: 3 → Talep Kayıt Formu; 4 → müşteri uygunluk anketi; 5 → seviye uygulandı; 6 → en az bir tutanak; 8 → en az bir risk. Diğer adımlar ekiple birlikte belirlenir | Şirketin süreç dokümanındaki "form doldurulur, tutanağa yazılır" adımları gerçekten kontrol edilir |
| 11 | Değerlendirme | Ön koşullar saf bir sınıfta değerlendirilir (`StepConditions` gibi); her tür için veritabanısız test | Kural tek yerde; ekran ve backend aynı sonucu verir |

### Ayarlanabilirlik

| # | Konu | Öneri | Neden |
| --- | --- | --- | --- |
| 12 | Atlama koşulları | Adımın atlama koşulu **kural listesi** olur: her kural bir alan, bir karşılaştırma ve bir değer (ör. `Kaynak = Dış`, `Seviye ≥ 2`, `Bütçe onaylı = Evet`). Kuralların hepsi sağlanırsa adım yapılır. Alanlar: talep kaynağı, talep türü, teklif hazırlanacak mı, seviye ve şirketin özel alanları (karar 15) | Bugünkü üç koşul bu yapının üç örneği olur |
| 13 | Akışı düzenleme | Şirket yöneticisi kendi akış kopyasını ekrandan düzenler. Aşama ve adım ekler; metin, sorumlu yetki, onaylayan yetki, karar, "Hayır" dönüşü, ön koşul ve atlama koşulu verir; sırayı değiştirir. Düzenlenen akış `IsCustomized` olur ve şablon eşitlemesi ona dokunmaz | Migration gerekmez |
| 14 | Çalışan projeler | **Kayıt silinmez:** adım "çıkarılınca" pasife alınır. Üzerinde aktif proje bekleyen adım pasife alınamaz; ekran hangi projelerin beklediğini söyler. Sıra değişince projeler bulundukları adımda kalır (`Id` değişmez). Akış sürümleme yapılmaz | Süreç geçmişi korunur |
| 15 | Şirkete özel alanlar | Yeni entity **`CustomField`**: şirkete ait, şimdilik yalnızca talep / proje için. Ad, tür (metin, sayı, tarih, seçim, evet-hayır), seçenekler, zorunlu mu, sıra, aktif mi. Değerler projede tek bir JSON kolonda (`Project.CustomValues`). Talep formunda ve Genel Bilgiler'de gösterilir; atlama koşullarında ve "Talep Kayıt Formu dolu" ön koşulunda kullanılır | Tek kolon, tek migration |
| 16 | Akışın işi | Kod sabitleri (`ProcessFlowCodes`) yerine her kayıt türü için şirkette bir **varsayılan akış** işareti. Yeni talep, şirketin varsayılan talep akışıyla başlar | Şirket kendi talep akışını varsayılan yapabilir |
| 17 | Yapay zekâ modeli | Şirket Ayarları'nda **model seçimi**: servisin desteklediği modellerden biri. Backend modeli her istekte servise gönderir; servis durumsuz kalır. Şirket başına model anahtarı bu fazda **yok** | Anahtarı şifreli saklamak ayrı bir güvenlik işi |
| 18 | Kullanım takibi | Yeni tablo **`AiUsage`**: şirket, kişi, yardımcı, model, girdi / çıktı token, süre, tarih. Şirket Ayarları'nda bu ayın özeti; isteğe bağlı aylık sınır (aşılınca 503, Türkçe mesaj) | Şirket neye ne harcandığını görür |
| 19 | Yetki | Kullanıcılar, yetkiler, akış, özel alanlar ve yapay zekâ ayarlarını yalnızca **Şirket yöneticisi** değiştirir. Herkes okuyabilir (Süreç Kütüphanesi bugünkü gibi) | Ayrı bir yönetim kavramı gerekmez |

## 4. Yapılacak işler

Her satır en az bir PR'dır. Sıra: sözleşme, sonra entity ve migration, sonra API, en son ekran.
Sorumlular **öneridir**; modül sahipliğine göre dağıtıldı (Identity Baran, Process / Agents Erva, Projects Dide), ekip
onaylarken değiştirebilir.

| No | İş | Sorumlu | Bağımlılık |
| --- | --- | --- | --- |
| A1 | **Sözleşme:** [configurability.md](configurability.md): Bölüm 3'teki kararların uygulama ayrıntıları. Yetki listesi, migration eşlemesi, ön koşul ve atlama kuralı biçimleri, uç noktalar, hata mesajları. Kurallar uygulandıkça AGENTS.md'ye taşınır | Erva | — |
| A2 | **Unvandan yetkiye (backend):** yetki listesi ve kullanıcının yetkileri; yetki kontrolünün yalnızca akış işlerinde yapılması ve eksik yetkide yetki adını ve şirket yöneticilerini söyleyen `403` (karar 2a, 2b); `User.Role` ve `UserRole`'ün kaldırılması; adımda sorumlu / onaylayan yetki; unvan kullanan bütün kuralların yetkiye çevrilmesi; `IsPlatformAdmin`; şirket açarken unvansız yönetici hesabı; migration (karar 1–7). **Kritik yol:** `Users` ve `ProcessSteps` tablolarına ve unvan kullanan bütün servislere dokunur | Baran | A1 |
| A3 | **Yetki ekranları:** Yönetim → Kullanıcılar'da yetki kutucukları ve hazır yetki setleri; yetkisi olmayan kullanıcıya Süreç sekmesinde ve "Yeni talep"te gereken yetkinin ve şirket yöneticisinin gösterilmesi (karar 2b); Platform → Yeni şirket formundan unvan alanının kaldırılması; frontend'deki sabit unvan listesinin ve unvana göre menü / buton kontrollerinin yetkiye çevrilmesi; üst barda unvan yerine şirket adı | Baran | A2 |
| A4 | *Yapıldı.* **Adım ön koşulları:** ön koşul türleri ve saf değerlendirme sınıfı; tamamlama ve "Evet" cevabında backend kontrolü; Süreç sekmesinde eksik işin ve yönlendirme düğmesinin gösterilmesi; Ithinka şablonuna ön koşulların eklenmesi (karar 8–11). Test yardımcıları (`TestProjects`) ön koşulları sağlayarak proje ilerletir | Erva | A1 |
| A5 | *Yapıldı.* **Atlama koşulu kuralları:** adımda kural listesi (karar 12); bugünkü üç koşulu kurala çeviren migration; `StepConditions` yeni yapıyla; saf testler | Erva | A1 |
| A6 | **Şirkete özel alanlar:** `CustomField` ve `Project.CustomValues` (karar 15). Alan tanımı API'si; talep formunda ve Genel Bilgiler'de gösterim; zorunlu alan doğrulaması. Alan listesi A4 ve A5'e verilir | Dide | A1 |
| A7 | *İlk parça yapıldı: kural ve ön koşul doğrulaması (`StepRuleValidation`).* **Akış düzenleme (backend):** aşama ve adım ekle, düzenle, pasife al, sırala; sorumlu yetki, ön koşul, dönüş ve atlama koşulu; varsayılan akış işareti ve `ProcessFlowCodes`'un kaldırılması; çalışan proje kuralları; `IsCustomized` (karar 13, 14, 16) | Erva | A2, A4, A5 |
| A8 | **Akış düzenleme ekranı:** Süreç Kütüphanesi'nde şirket yöneticisi için düzenleme. Aşama ve adım listesi, sıralama, adım formu (sorumlu, onaylayan, karar, dönüş, ön koşul, atlama kuralları) | Dide | A6, A7 |
| A9 | **Yapay zekâ modeli ve kullanım:** `ai/` servisinde istekle gelen model ve cevapta kullanım; `AiUsage`; Şirket Ayarları'nda model seçimi, bu ayın kullanımı ve isteğe bağlı aylık sınır (karar 17, 18) | Dide | A1 |
| A10 | *Erva → Baran (A2, A3) yapıldı: `Identity/PermissionCrossTests`, frontend `core/auth/auth.service.spec.ts`.* **Çapraz testler:** her işin testlerini başkası yazar (aşağıdaki tablo). Ayrıca ayrım testleri: A şirketinin yetkisi, alanı ve akış değişikliği B'yi etkilemez | Herkes | A2–A9 |
| A11 | **Belgeler ve demo:** data-model.md, README (şirket açma ve yetkiler), demo senaryosu. Demo: yeni şirket açılır, yönetici kullanıcı ekleyip yetki verir, akışa adım ve alan ekler; talep yürür, ön koşulu sağlanmamış adım geçilemez | Erva | Hepsi |

### Çapraz testler

Kural aynı kalır (PLANNING.md → Birlikte çalışma kuralları): Erva → Baran'ın, Baran → Dide'nin, Dide → Erva'nın işleri.

| İşi yapan | İş | Testini yazan |
| --- | --- | --- |
| Baran | A2, A3 yetkiler | Erva |
| Dide | A6 özel alanlar, A8 akış ekranı, A9 yapay zekâ | Baran |
| Erva | A4 ön koşullar, A5 atlama koşulları, A7 akış düzenleme | Dide |

## 5. Sıra

```
Dalga 1       A1 sözleşme
Dalga 2       A2 unvandan yetkiye (unvan kullanan diğer PR'lar bu sırada beklemeye alınır)
              A4 ön koşullar · A5 atlama koşulları · A6 özel alanlar · A9 yapay zekâ (A2 ile paralel)
Dalga 3       A3 yetki ekranları · A7 akış düzenleme (backend)
Dalga 4       A8 akış düzenleme ekranı
Dalga 5       A10 çapraz testler · A11 belgeler ve demo
```

- **A2 unvan kullanan her yere dokunur.** Faz 6'daki T2 gibi tek PR'da yapılır; A2 açıkken unvan kullanan başka PR açılmaz.
  A4, A5, A6 ve A9 kullanıcı ve unvan alanlarına dokunmadığı için paralel yürür.
- **A4, Ithinka'nın bugünkü davranışını bilerek değiştirir:** adımlar artık işi yapılmadan geçilemez. Süreç testleri ve demo
  senaryosu buna göre güncellenir. Bu, "Ithinka'nın davranışı değişmez" kuralının tek istisnasıdır.
- A4, A5 ve A6 aynı noktada buluşur (koşulda ve ön koşulda özel alan): A6 alan listesini veren arayüzü önce açar.

## 6. Riskler

| Risk | Önlem |
| --- | --- |
| Migration bir kullanıcıyı yetkisiz bırakır ya da bir adımı yanlış yetkiye bağlar | Eşleme tablosu (Bölüm 3) birebir uygulanır; testlerde her seed kullanıcısının ve adımın yetkisi migration sonrası kontrol edilir |
| Yetkiler bugünkü kuralları tam karşılamaz | A2'de Bölüm 2'deki her kural için mevcut test yeşil kalmalı, yoksa yenisi yazılır |
| Ön koşullar süreci gereğinden fazla kilitler (ör. dokümanı dışarıda tutan ekip adımı geçemez) | Ön koşul adım başına isteğe bağlıdır; şirket yöneticisi akış düzenleme ekranından kaldırabilir. Ithinka'nın listesi ekiple birlikte belirlenir (karar 10) |
| Ön koşul eklendiği anda yolun ortasındaki projeler takılır | Ön koşul yalnızca adımı tamamlarken bakılır; eksikse ekran nereye gidileceğini gösterir. Geriye dönük kayıt istenmez |
| Akış düzenlemesi çalışan bir projeyi bozar | Karar 14: adım silinmez, pasife alınır; üzerinde proje varsa pasife alınamaz; `Id`'ler değişmez |
| Bir koşul silinmiş ya da türü değişmiş bir özel alana bakar | Özel alan silinmez, pasife alınır; koşulda kullanılan alanın türü değiştirilemez; pasif alana bakan kural "sağlanmadı" sayılır ve ekranda uyarı görünür |
| Şablon eşitlemesi şirketin düzenlediği akışı ezer | Faz 6 kuralı: `IsCustomized` kopyaya dokunulmaz |
| Faz büyür, bitmez | Her iş ayrı PR ve ayrı dalga; her dalga sonunda uygulama çalışır. Kapsam dışı olanlar Bölüm 7'de |

## 7. Bu faza alınmayanlar

- **Şirketin kendi yetkisini tanımlaması** ya da rol / izin matrisi: yetki listesi platformda sabit ve kısa kalır (karar 2).
- **Akış sürümleme:** değişiklik çalışan projelere hemen yansır (karar 14).
- **Talep dışındaki kayıtlara özel alan** (risk, açık konu, tutanak).
- **Özel alanlara göre listeleme, süzme ve arama.**
- **Şirket başına yapay zekâ model anahtarı** (karar 17).
- **Soru listelerinin ve doküman türlerinin ekrandan düzenlenmesi.**
- **Şablon kütüphanesi:** yeni şirketin Ithinka dışında bir şablon seti seçmesi.
- **Entegrasyonlar** (GitHub, Azure DevOps, kodlama ajanı): Faz 8 ve sonrası (PLANNING.md → Yol haritası).

## 8. Açık konular

1. **Kararlar:** Bölüm 3'teki 19 karar öneridir; ekip onayı bekliyor.
2. **Sorumlular:** Bölüm 4'teki dağılım öneridir.
3. **Yetki listesi:** karar 2'deki altı yetki akışın bugünkü sorumlularından çıkarıldı. Ekip başka bir yetki isterse (ör. "kaydı
   kapatabilir") A1'de eklenir.
4. **Ithinka'nın ön koşulları:** karar 10'daki beş adım dışındaki adımlar (7, 9–19) için ne gerektiği ekiple belirlenir.
5. **Şirket yönetici hesabı iş de yapabilir mi?** Öneri: evet, kendine yetki verirse (karar 5). İstenmezse yönetim hesabı yalnızca
   yönetir ve iş yetkisi alamaz.
6. **Aylık yapay zekâ sınırı** zorunlu mu, isteğe bağlı mı? Öneri: isteğe bağlı, varsayılan sınırsız.
