# Faz 4 — Şirket akışları ve formlarına uyum

Bu dosya Faz 4'te yapılacak işleri, kimin neyi yapacağını ve işlerin sırasını anlatır.
Ortak kurallar için: [AGENTS.md](../AGENTS.md), ekip ve çalışma kuralları için: [PLANNING.md](../PLANNING.md).

## 1. Neden

Şirketin iş analistleri süreç akışlarının son hâlini ve kullandıkları formları paylaştı. Uygulama şu an prototipteki
13 aşamalı tek akışla çalışıyor. Faz 4'te menüler bu akışlara ve formlara göre güncellenir, eksik menüler eklenir.

**Kaynaklar** (repoda değil; şablonlar repoya konmaz, yalnızca alan adları ve seçenekler alınır):
- Akışlar: `Ithinka Proje Dokümanları/Akışlar/*.jpg` (5 akış)
- Formlar: `Ithinka Proje Dokümanları/Çalışmalar/Dokümanlar/` (onaylanmamışlar da kullanılır; `Arşiv` kullanılmaz)
- Doküman listesi: `Onaylanmamış Dokümanlar/__Dokümanlar.xlsx` — doküman kodu ve adında **tek doğru kaynak** budur

## 2. Alınan kararlar

| Konu | Karar |
| --- | --- |
| Kapsam | Akışlarda olan her şey MVP'ye girer |
| Talep kanalı | Yalnızca **E-posta** ve **Telefon** (akış görselindeki "sözlü / yazılı" yerine) |
| Proje No | Düz bir sıra numarası (1, 2, 3…). `ISDLC-2026-014` biçimindeki kod kalkar. Proje içi kayıt kodları (`RSK-001`, `GR-001`) aynen kalır |
| Akışlar | Koda gömülmez, **veri** olarak tutulur. Sonradan 6–7 akış daha gelecek (BT destek, Ar-Ge destek…); yeni akış = yeni seed dosyası |
| Ek geliştirme | Ana projeye bağlı **ayrı bir kayıt**; kendi süreci, seviyesi, riskleri ve dokümanları olur |
| Şablon dosyaları | Repoya konmaz |
| Onaylanmamış dokümanlar | Şimdilik kullanılır |

## 3. Akışlar

| Akış | Neyin üzerinden işler | Bölümler | Adım |
| --- | --- | --- | --- |
| Yeni yazılım projesi | Talep / proje | Talep ve ön analiz · Teklif ve anlaşma · Proje başlatma ve geliştirme · Kapanış ve destek | 19 |
| Devam eden proje ek modül / geliştirme | Ek geliştirme (ana projeye bağlı) | Talep ve etki analizi · Ücretlendirme ve onay · Geliştirme ve test · Kapanış | 17 |
| Süreç iyileştirme | İyileştirme önerisi | Planla · Uygula (pilot) · Kontrol et · Önlem al | 8 |
| Haftalık kaynak planlama | Hafta | Kaynak planlama toplantısı · Azure DevOps'ta task açılışı · Task takibi ve kapatma · Kontrol ve efor raporlama | 8 |
| Aylık faturalama | Ay | Jira kaydı · Ay sonu hazırlık · Kontrol ve fatura talebi · Fatura ve ay kapanışı | 6 |

**Akıştaki dallanmalar** (akış altyapısının desteklemesi gerekenler):
- *Yeni proje:* iç talepte 4. adım (müşteri uygunluğu) atlanır ("doğrudan proje" 3 → 5). Teklif hazırlanmayacaksa
  2. bölüm atlanır (9 → 12). Kapsam değişirse 10 → 9. Testte hata çıkarsa 17 → 16.
  4'te "uygun değil" ve 11'de ret: Talep Ret-Erteleme Formu doldurulur, talep kapanır.
- *Ek geliştirme:* seviye yolu belirler.
  - Seviye 1: doküman hazırlanmaz, termin e-postayla bildirilir.
  - Seviye 2: Kapsam ve Efor Dokümanı hazırlanır, teklifsiz doğrudan müşteriye gider (8 → 10).
  - Seviye 3: Kapsam ve Efor Dokümanı Satış'a gider, teklif hazırlanır.
  - Kapsam değişirse 10 → 9. Testte hata çıkarsa 15 → 14.
- *Süreç iyileştirme:*
  - 2'de öneri çalışmaya alınır, bekletilir ya da reddedilir.
  - 4'te onay → pilot; revizyon → 3; ret → kayıt kapanır.
  - 7'de hedef tutarsa standartlaştırılır, kısmen tutarsa pilot tekrarlanır, tutmazsa 3'e dönülür.
- *Kaynak planlama ve faturalama:* her hafta / her ay yeniden başlar; Azure DevOps ve Jira ile bağlantı kurulmaz,
  bilgiler uygulamaya elle girilir (bkz. Bölüm 7).

## 4. Yapılacak işler

Her satır en az bir PR'dır. Modül içi sıra aynı kalır: önce sözleşme (`docs/…-api.md`), sonra entity ve migration, sonra API, en son ekran.

### Dide — Süreç

Bu bölüm Erva'nın alanıydı. Erva'nın başka işleri olduğu için Faz 4'te Dide yapar.

| No | İş | Bağımlılık | Kaynak |
| --- | --- | --- | --- |
| E1 | **Akış altyapısı.** `Flow` kavramı: aşama ve adımlar bir akışa bağlanır, her kayıt hangi akışı izlediğini bilir. Adım atlama koşulları (iç/dış talep, teklif var/yok, seviye), geri dönüşler, adım ↔ doküman bağlantısı (Yardımcı / Talimat / Form / Çıktı), kaydın hangi adımdan sonra "proje" sayılacağı. `ProjectService` ve `DashboardService`'teki "aşama 1–3 taleptir" sabiti kalkar | — | Akış görselleri |
| E2 | **Üç akışın seed'i:** yeni yazılım projesi, ek geliştirme, süreç iyileştirme. Eski 13 aşamalı akışın verisi taşınır. Örnek projeler ve testler (`TestProjects.CreateAsync(db, "4.7")` gibi) yeni adım kodlarına çevrilir | E1 | Akış görselleri |
| E3 | **Talep Analizi sekmesi** (proje detayı): iş tipi ve talep kaynağına göre soru listesi, cevap, açık konu, durum. "Açık" kalan soru tek tıkla Açık Konular'a aktarılır | B1 | Talep Anlama Soru Listesi (003) |
| E4 | **Toplantı ve ziyaret tutanakları:** notları yazan, katılımcı rolü, toplantı özeti, tamamlanan / devam eden aşamalar, sonraki adımlar. Ziyaret türünde gidiş-dönüş tarihi, planlanan ve yapılan aktiviteler | — | Toplantı Tutanağı (004), Ziyaret Tutanağı (005) |
| E5 | **Süreç İyileştirme menüsü:** öneri kaydı (kaynak: retrospektif / hata-şikâyet / çalışan önerisi / yönetim hedefi), önceliklendirme, çalışma dokümanı alanları (mevcut durum, kök neden, çözüm, pilot planı), pilot değerlendirme (hedef ↔ gerçekleşen) | E1, E2 | Süreç iyileştirme akışı (formlar 040–042 klasörde yok) |
| E6 | **Demo senaryosu** yeni akışlarla baştan yazılır ve denenir | Hepsi | `docs/demo-scenario.md` |

### Dide — Talepler, projeler, dokümanlar, gereksinimler

| No | İş | Bağımlılık | Kaynak |
| --- | --- | --- | --- |
| D1 | **Talepler.** Proje No sayıya döner. Yeni alanlar: talep tarihi, talebi ileten kişi, iletişim (e-posta / telefon), talebi kayda alan, notlar, teklif hazırlanacak mı. Kanal: E-posta / Telefon. Kaynak: Kurum içi / Dış (müşteri) / İlişkili şirket. Mevcut kayıtlar migration ile çevrilir | — | Talep Kayıt Formu (002), Proje Kayıt Listesi (032) |
| D2 | **Ret / erteleme kaydı.** Karar (ret / erteleme), karar tarihi, kararı veren, gerekçe kategorisi (10 seçenek), açıklama, yeniden değerlendirme tarihi. Yeni durum: `Postponed` (Ertelendi) | D1 | Talep Ret-Erteleme Formu (007) |
| D3 | **Ek geliştirme kaydı.** Ana projeye bağlı kayıt (`ParentProjectId`), proje içinde sıra numarası (ör. `14 / EG-01`). Proje detayında "Ek Geliştirmeler" sekmesi; ek geliştirme kendi detay sayfasını ve sekmelerini kullanır | D1, E1 | Ek geliştirme akışı |
| D4 | **Dokümanlar.** 39 dokümanlık liste (kod, tür, kategori, sürüm, bağlı adım). Seviyeye göre Zorunlu / Koşullu / Opsiyonel / Gerekli değil matrisi; ek geliştirme için ayrı matris. Sekmede özet ("4 zorunlu, 3 opsiyonel; tamamlanan 0 / 4"). Kural: zorunlu doküman onaylanmadan bir sonraki aşamaya geçilmez (E1'deki adım ↔ doküman bağlantısını kullanır) | E1, E2 | `__Dokümanlar.xlsx`, Seviye-Doküman Matrisi (004) |
| D5 | **Doküman Kütüphanesi menüsü:** 39 dokümanın kataloğu (ne işe yarar, hangi adımda, hangi seviyede). Şablon dosyası tutulmaz | D4 | `__Dokümanlar.xlsx` |
| D6 | **Gereksinimler.** Alt kategori: fonksiyonelde API / arayüz / veritabanı / entegrasyon / iş akışı; fonksiyonel olmayanda performans / güvenlik / kullanılabilirlik / erişilebilirlik / güvenilirlik / ölçeklenebilirlik. Yeni türler: uyum, izleme ve raporlama | — | BRD (009), Kapsam Dokümanı (013) |
| D7 | **Gereksinim iç onayı.** 39 kontrol maddesi; her biri Uygun / Kısmen uygun / Uygun değil / Uygulanamaz ve açıklama. Uygunluk oranı, onaylayan, tarih | B1 | Gereksinimler İç Onay Kontrol Listesi (012) |
| D8 | **Kapsam ve efor / etki analizi.** Kapsam, kapsam dışı maddeler, efor (geliştirme + test + yayın), Proje Yöneticisi onayı. Ek geliştirmede etki analizi (fonksiyonel, veri ve entegrasyon, altyapı-güvenlik-performans) | E1 | Kapsam ve Efor Dokümanı (013), ek geliştirme akışı 5 ve 8 |
| D9 | **Gösterge paneli** yeni akışlara uyarlanır: aşama çubukları akışa göre, risk eşiği yeni ölçeğe göre, kabul kuyruğu uygunluk anketine göre; ek geliştirmeler ve ertelenen talepler sayılır | E2, B2, B3 | — |

### Baran — Soru bankası, seviye, risk, ekip, planlama, faturalama

| No | İş | Bağımlılık | Kaynak |
| --- | --- | --- | --- |
| B1 | **Soru bankası altyapısı.** Dört form aynı yapıda: seviye, talep analizi, risk analizi, iç onay. Soru seti = iş tipi + alt kategori (Kurum içi / Dış / İlişkili şirket); soru zorunlu ya da duruma bağlı; seçenekler ve puanları; cevaplar kayıt başına tutulur. Soru setleri seed dosyasından yüklenir | — | Dört formun "Sorular" ve "Parametreler" sayfaları |
| B2 | **Seviye & Uygunluk.** Seviye Belirleme v3.0: dış talepte 13 soru, 5'er seçenek; 1–20 → Seviye 1, 21–40 → Seviye 2, 41–60 → Seviye 3. Araştırma seviyesi (efor: 1–30 gün). Müşteri uygunluk anketi: 10 soru, toplam 100; Uygun (85+) / Şartlı uygun (70–84) / Riskli (50–69) / Uygun değil; yalnızca dış talepte. Eski 6 ölçütlü kabul puanlaması kalkar | B1, E1 | Seviye Belirleme Şablonu (008), Müşteri Uygunluk Anketi (006) |
| B3 | **Risk Yönetimi.** Ölçek 3 × 3 (olasılık ve şiddet 1–3, derece 1–9). Seviyeler: Hafif, Tolere edilebilir, Orta dereceli, Yüksek risk, Tolere edilemez; öncelik A / B / C. Yeni alanlar: konu / risk grubu, risk nedeni, belirleme tarihi, hedef tarih, gözden geçirme turları. Isı haritası yeni ölçeğe göre | — | Proje Riskleri (015), Risk Analiz Formu (014) |
| B4 | **Risk analizi.** 45 kontrol sorusu (30 zorunlu, 15 duruma bağlı); "Risk var" ya da "Belirsiz" denen satır risk kaydına aktarılır | B1, B3 | Risk Analiz Formu (014) |
| B5 | **Ekip ve İletişim sekmesi** (proje detayı): proje ekibi (taraf, sorumluluk, kişi, telefon, e-posta) ve müşteri iletişim kişileri | — | Proje Ekip Listesi (018) |
| B6 | **Kaynak Planlama menüsü** (haftalık): görev planı (sorumlu, proje / User Story, beklenen çıktı, planlanan süre, hedef tarih), plansız işler (ticket no, faturalanır mı, bakım mı), gerçekleşen ve kalan süre, toplantı aksiyonları, haftalık efor özeti | — | Kaynak planlama akışı, Haftalık Kaynak Planlama Takip Tablosu (021) |
| B7 | **Faturalama menüsü** (aylık): ticket listesi (proje, konu, öncelik, efor saati), faturalanır / faturalanmaz, adam-gün × birim fiyat = tutar, müşteri bazında özet, Proje Yöneticisi kontrolü, fatura kesim talebi ve takibi, ay kapanışı | B6 ile aynı yapı | Faturalama akışı |

### Çapraz testler

Kural aynı kalır (PLANNING.md → Birlikte çalışma kuralları): Erva → Baran'ın, Baran → Dide'nin işleri.
Faz 4'te Erva'nın işi olmadığı için E ve D işlerinin çapraz testlerini kimin yazacağı ekipçe kararlaştırılır.
Özellikle akış altyapısının (E1) dallanmaları ve soru bankasının (B1) hesapları saf kural sınıfı olarak her kombinasyonda test edilir.

## 5. Sıra

```
Dalga 1 (birbirini beklemez)   E1 akış altyapısı · D1 talepler · B1 soru bankası · B3 risk · B5 ekip
Dalga 2                        E2 seed · E4 tutanaklar · D2 ret/erteleme · D3 ek geliştirme · D6 gereksinim alanları · B2 seviye · B4 risk analizi
Dalga 3                        E3 talep analizi · E5 süreç iyileştirme · D4 dokümanlar · D7 iç onay · D8 kapsam/efor · B6 kaynak planlama · B7 faturalama
Dalga 4                        D5 doküman kütüphanesi · D9 gösterge paneli · E6 demo · çapraz testler
```

- **E1 kritik yoldur.** Dide sözleşmeyi (`docs/process-api.md` güncellemesi) ilk PR olarak açar; herkes onu okuyup kendi işini ona göre tasarlar.
- E ve D işlerinin ikisi de Dide'de olduğu için Dide'nin sırası: E1 → D1 → E2 → D3 → E4 → D2; kalanlar dalga sırasıyla.
- **B1 ikinci kritik iştir;** E3, D7, B2 ve B4 onu bekler. Baran sözleşmeyi erken paylaşır.
- **Migration çakışması:** Dalga 1'de üç kişi de migration üretecek. Kural aynı: PR'dan hemen önce main'i alıp üretin, PR başına tek migration.

## 6. Ortak dosyalar ve dokümanlar

| Dosya | Kim, ne zaman |
| --- | --- |
| `AGENTS.md` Bölüm 3 | Kaynak Planlama ve Faturalama "sonraki sürüm" listesinden çıkar; Süreç İyileştirme eklenir. İlk PR'da Dide |
| `AGENTS.md` Bölüm 6 | Yeni kavramlar (`Flow`, ek geliştirme, soru bankası, iyileştirme önerisi, haftalık plan, aylık fatura dönemi) kodu yazan kişi tarafından, kod PR'ından önce eklenir |
| `docs/data-model.md` | Her entity değişikliğinde sahibi günceller |
| Menü (`sidebar.component.ts`) | Yeni menü: Süreç İyileştirme (Dide), Doküman Kütüphanesi (Dide), Kaynak Planlama ve Faturalama (Baran). Küçük ve ayrı PR'lar |

## 7. Açık konular (analistlere sorulacak)

1. **Süreç iyileştirme formları** (040 Kayıt Listesi, 041 Çalışma Dokümanı, 042 Pilot Değerlendirme Raporu) klasörde yok. Gelene kadar akıştaki kutulardaki alanlarla ilerlenir.
2. **Müşteri İletişim Bilgileri Listesi (031)** klasörde yok. B5 şimdilik ekip listesindeki alanlarla yapılır.
3. **Kaynak planlama ve faturalama** Azure DevOps ve Jira'da yürüyor. Uygulama bu araçlarla bağlantı kurmayacak; bilgiler elle girilecek.
   Analistlerin bunu uygulamada tutmayı gerçekten isteyip istemediği teyit edilmeli.
4. **Ek geliştirme numarası:** akışın son hâlinde `EG-01` biçimi yazmıyor. `14 / EG-01` önerisi teyit edilmeli.
5. **Uygunluk ve kabul:** eski akıştaki "proje kabul puanlaması ve yönetici kararı" yeni akışta yok; yerini müşteri uygunluk anketi alıyor.
   Yönetici onayı yalnızca teklif (10. adım) ve kapsam-efor (Proje Yöneticisi onayı) için mi aranacak?
6. **Ek geliştirmenin soru setleri:** dört formda (seviye, talep analizi, risk analizi, iç onay) yalnızca "Yeni Yazılım Projesi" iş tipi var.
   Ek geliştirme için ayrı sorular mı hazırlanacak? Gelene kadar ek geliştirme, yeni proje sorularını kullanır ([question-bank-api.md](question-bank-api.md), karar 4).
