# Yapay Zekâ Ajanları — Vizyon

ISDLC sürecindeki işlerin bir kısmının yapay zekâ ajanlarıyla hazırlanması için ilk tasarım notu.
Durum: **toplantı ve risk yardımcıları uygulamada; geri kalanı vizyon.** Sorumlu: Erva.
Komut satırındaki denemenin ardından ekip, toplantı ajanını Python servisi olarak uygulamaya almayı onayladı:
bkz. "Uygulamada: toplantı yardımcısı". Risks modülü gelince risk ajanı aynı yoldan bağlandı: bkz. "Uygulamada: risk
yardımcısı". Belgenin geri kalanı sonraki adımların tasarımıdır.

## Temel ilke: ajan hazırlar, insan onaylar

- Ajan bir işi **taslak** olarak hazırlar; kaydı kesinleştiren her zaman bir insandır.
- Sorumluluk veya imza gerektiren adımlar (onaylar, Talep Sahibi teyitleri, teklif, sözleşme) insanda kalır.
- Akışı ajan değil, `Process` modülü yönetir. Ajan, proje belirli bir adıma geldiğinde o adımın işi için çalışır.
- Ajan, insan kullanıcılarla aynı servisleri, aynı doğrulamayı ve aynı yetki kurallarını kullanır.
  Yaptığı her şey denetim kaydına düşer.

## Ajan tipleri

Her adıma ayrı ajan yazılmaz; aynı türden işleri aynı ajan yapar. Değişen yalnızca şablon ve girdidir.

| Ajan | Görev |
| --- | --- |
| Talep ajanı | E-posta veya görüşme notundan talep kaydı ve talep analizi taslağı çıkarır; belirsiz noktaları soru olarak açar |
| Doküman ajanı | Şablona göre doküman taslağı yazar |
| Gözden geçirme ajanı | Dokümanları kontrol listesi maddelerine göre değerlendirir |
| Toplantı ajanı | Tutanağı özetler, aksiyonları açık konulara aktarır |
| Risk ajanı | Risk önerir; olasılık, etki ve önleyici aksiyon yazar |
| Tahmin ajanı | Seviye, kabul puanı, efor ve maliyet önerir; benzer geçmiş projeleri gösterir |
| Kapsam ajanı | Netleşen kapsamı teklif kapsamıyla karşılaştırır, farkları listeler |
| Test ajanı | Gereksinimlerden Test Planı ve test senaryosu taslağı çıkarır |

### Ajan ve görev

- **Görev** tek bir işi yapar: bir prompt dosyası, modelden istenen cevap şeması ve göreve özel kontroller.
  Her görev ayrı bir model çağrısıdır ve kendi eval vakaları vardır.
- **Ajan** aynı türden kaydı öneren görevlerin ailesidir. Maskeleme, model çağrısı, bozuk cevapta tekrar deneme
  ve dayanak kontrolü bütün görevler için ortaktır (`ai/isdlc_ai/agents/base.py`).
- Görevler tek başına da, sırayla da çalışabilir; sıralı çalışmada bir görevin sonucu sonrakinin girdisidir.

**Bir görev hangi ajana konur?** Ana ölçüt, görevin **ne ürettiğidir**, yani hangi kaydı önerdiği. Girdi
yalnızca bağlamdır: toplantı notundan da dokümandan da risk çıkarmak Risk ajanının işidir. Yardımcı ölçütler şunlar:

| Ölçüt | Aynı ajanda durur, eğer… |
| --- | --- |
| Uzmanlık | Görevler aynı bilgiyi ve kuralları istiyorsa |
| Yetki | Görevler aynı veriyi görmeye yetkiliyse (ör. maliyet verisi ayrı ajanda) |
| Birlikte değişme | Birinin kuralı değişince öbürünün de değişmesi gerekiyorsa |
| Hazırlayan / kontrol eden | Hiçbir zaman. Bir işi hazırlayan görev ile onu kontrol eden görev ayrı ajanlarda olur |

Bölmenin yanlış gittiğini gösteren işaretler:
- Bir prompt "görev X ise…, Y ise…" diye dallanıyorsa ajan bölünmeli.
- Bir görevi iyileştirmek aynı ajandaki başka bir görevin eval sonucunu düşürüyorsa görevler ayrılmalı.
- İki ajan aynı kuralları tekrar ediyorsa birleştirilmeli.

## Adımlara göre ajan rolü

Adım kodları `process-flow.json` ile aynıdır.

### Ajan hazırlar, insan onaylar (`Draft`)

| Adım | Ajan | Çıktı |
| --- | --- | --- |
| 1.1 | Talep ajanı | Talep Başlangıç Şablonu taslağı |
| 2.1 | Talep ajanı | Talep Analizi taslağı (görüşme notundan) |
| 4.1 | Doküman ajanı | Proje Kapsam Dokümanı taslağı |
| 4.2 | Doküman ajanı | Kavramsal Tasarım Raporu taslağı |
| 4.3 | Doküman ajanı | Teknik kısıtlar ve teknik gereksinimler taslağı |
| 4.8 | Risk ajanı | Risk kayıtları (taslak) |
| 6.4, 7.1, 7.11, 13.3 | Toplantı ajanı | Tutanak özeti ve açık konu kayıtları (taslak) |
| 7.2 | Doküman ajanı | Seviyenin gerektirdiği analiz dokümanları |
| 7.8 | Test ajanı | Test Planı ve kabul testi senaryoları taslağı |
| 9.1 | Test ajanı | User Story'ye ait test senaryoları taslağı |
| 10.1, 10.4, 10.5, 10.6 | Doküman ajanı | Yayın Planı, kılavuzlar, teknik dokümantasyon, Bakım Planı taslakları |
| 12.1 | Doküman ajanı | Proje Teslim Tutanağı taslağı |

### Ajan yalnızca öneri verir (`Suggest`)

| Adım | Ajan | Öneri |
| --- | --- | --- |
| 1.2 | Talep ajanı | Talebin yeni proje olup olmadığı |
| 2.2 | Talep ajanı | Belirsiz kalan noktalar; her biri açık konu (tür: Soru) olarak önerilir |
| 2.4 | Tahmin ajanı | Seviye ve efor tahmini |
| 3.1 | Tahmin ajanı | Proje kabul puanlaması |
| 4.4, 7.9 | Gözden geçirme ajanı | Gereksinim İç Onay Kontrol Listesi'ne göre madde madde değerlendirme |
| 4.9 | Tahmin ajanı | Efor, süre, kaynak ve maliyet tahmini |
| 7.4 | Kapsam ajanı | Teklif kapsamı ile netleşen kapsam arasındaki farklar |
| 8.2 | Gözden geçirme ajanı | Gereksinim ve kabul ölçütünün geliştirmeye yeterliliği |

### Yalnızca insan (`Manual`)

| Adımlar | Neden |
| --- | --- |
| 3.2, 4.5, 7.10 | Yönetici onayı |
| 4.6, 4.7, 5.2, 5.3, 5.4, 7.12, 9.7, 10.8, 12.2 | Talep Sahibi ile iletişim ve teyit |
| 5.1, 6.1, 13.1 | Teklif, sözleşme, faturalama |
| 10.2, 10.3, 11.x | Canlı ortam işlemleri |

Aşama 8'de (Geliştirme) kod yazımı bir kodlama ajanına bırakılabilir; bu ISDLC'nin kapsamı dışındadır.
ISDLC bu aşamada yalnızca takip eder.

Tabloda olmayan adımlar başlangıçta `Manual` kabul edilir.

## Mimari

```
Process (adıma gelindi)                     .NET backend
   │  AgentRun oluşturur, Python servisini çağırır
   ▼
Ajan servisi (Python) ──► LLM
   │  ISDLC API'si üzerinden okur: proje, tutanak, gereksinim, geçmiş projeler (RAG)
   │  önerileri gönderir: POST /agent-runs/{id}/suggestions
   ▼
Öneriler (AgentSuggestion) ──► İş analisti inceler ──► Kabul / düzenleyerek kabul / ret
                                                          │
                                                          ▼
                                      Normal servis gerçek kaydı oluşturur (ör. OpenIssueService)
```

### Teknoloji: ayrı Python servisi (öneri, ekip onayı bekliyor)

- Ajanlar Python ile, backend'den ayrı bir servis olarak yazılır. Gerekçe: RAG, doküman işleme, kişisel veri
  gizleme ve değerlendirme (eval) araçlarının Python'da çok daha olgun olması.
- **Python servisi veritabanına hiçbir zaman doğrudan erişmez**; yalnızca ISDLC API'sini kullanır.
  İş kuralları yalnızca .NET servislerinde kalır, ajan da bu kurallardan geçer.
- İstisna: geçmiş projelerden üretilen arama indeksi (RAG) Python servisinin kendi deposunda tutulabilir.
  Bu indeks API'den her zaman yeniden üretilebilir; asıl veri değildir.
- Yerelde `docker-compose.yml`'ye bir servis olarak eklenir. API sözleşmesi Swagger (OpenAPI) tanımından izlenir.
- Python yığına eklenmeden önce AGENTS.md "Teknoloji yığını" ve "Klasör yapısı" bölümleri güncellenir.

- **Tetikleme:** Proje bir adıma geldiğinde, o adımın ayarı `Manual` değilse ilgili ajan çalışır.
  Kullanıcı ajanı elle de başlatabilir ("Taslak hazırla" butonu).
- **Araçlar:** Ajan veritabanına doğrudan erişmez; yalnızca `Isdlc.Application` servislerini çağırır.
  Bu yüzden iş kuralları yalnızca servislerde olmalıdır (process-api.md'deki kural).
- **Yapılandırılmış çıktı:** Ajan çıktısı doğrudan ilgili DTO'ya (ör. `CreateRiskDto`) uyar ve insan girdisiyle aynı doğrulamadan geçer.
  Doğrulamadan geçemeyen öneri kaydedilmez. Her öneri, dayandığı cümleyi (`evidence`) de taşır; inceleyen kişi kaynağı görür.
- **Adım ayarı veri olarak tutulur:** Hangi adımda hangi ajanın hangi modda ve hangi şablonla çalışacağı kodda değil veritabanındadır.
  Bir adımı ajana açmak veya kapatmak bir ayar değişikliğidir.
- **Geçmişten öğrenme (RAG):** Kapanmış projelerin dokümanları, riskleri ve tahminleri aranabilir hale getirilir.
  Tahmin ve risk ajanları önerilerinde benzer projeleri kaynak olarak gösterir.
- **Ajan kimliği:** Ajan, adına çalıştığı rolün yetkisiyle çalışır. Onay adımlarını hiçbir koşulda kapatamaz.

### Veri modeli (öneri)

**Ajan kayıt oluşturmaz, öneri oluşturur.** Öneriler ayrı bir tabloda bekler; kabul edilince normal servis gerçek kaydı
oluşturur. Mevcut tablolarda (`OpenIssue`, `Meeting`, …) değişiklik gerekmez. Taslakları mevcut tablolarda bir `Draft`
durumuyla tutmak yerine bu yolun seçilme nedenleri:
- taslaklar `AK-001` gibi kod numaralarını boşuna tüketmez;
- listelere, gecikme hesabına ve göstergelere karışmaz;
- reddedilen taslaklar denetim kaydını doldurmaz;
- Python servisinin gerçek kayıt oluşturmasına gerek kalmaz.

**`StepAutomation`** — hangi adımda hangi ajan çalışır (ana veri, seed ile yüklenir)

| Alan | Tür | Not |
| --- | --- | --- |
| `Id` | int | |
| `ProcessStepId` | int (FK → ProcessStep) | |
| `AgentType` | `AgentType` | `Request`, `Document`, `Review`, `Meeting`, `Risk`, `Estimate`, `Scope`, `Test` |
| `Mode` | `AutomationMode` | `Suggest` veya `Draft`; tabloda olmayan adım `Manual` sayılır |
| `TemplateCode` | string? | Doküman ajanı için şablon |
| `IsEnabled` | bool | Adım kod değişikliği olmadan açılıp kapatılır |

**`AgentRun`** — her ajan çalıştırması (`StepRecord` gibi değiştirilmez, silinmez)

| Alan | Tür | Not |
| --- | --- | --- |
| `Id` | int | |
| `ProjectId` | int (FK → Project) | |
| `ProcessStepId` | int? (FK → ProcessStep) | Elle başlatıldıysa boş olabilir |
| `AgentType` | `AgentType` | |
| `Trigger` | `AgentTrigger` | `StepEntered` (adıma gelindi), `Manual` (butonla) |
| `TriggeredById` | int? (FK → User) | |
| `Status` | `AgentRunStatus` | `Queued`, `Running`, `Succeeded`, `Failed` |
| `ModelName`, `PromptVersion` | string | Değerlendirme ve karşılaştırma için |
| `InputTokens`, `OutputTokens` | int? | Maliyet takibi için |
| `Error` | string? | |
| `StartedAt`, `FinishedAt` | DateTime (UTC), DateTime? (UTC) | |

**`AgentSuggestion`** — ajanın önerdiği her kayıt

| Alan | Tür | Not |
| --- | --- | --- |
| `Id` | int | |
| `AgentRunId` | int (FK → AgentRun) | |
| `ProjectId` | int (FK → Project) | |
| `TargetType` | `SuggestionTarget` | `OpenIssue`, `Meeting`, `Risk`, `Requirement`, `Document`, `StepAnswer` |
| `Payload` | string (JSON) | İlgili Create DTO'sunun aynısı (ör. `CreateOpenIssueDto`) |
| `Evidence` | string? | Önerinin dayandığı cümle |
| `Rationale` | string? | Ajanın gerekçesi |
| `Sources` | string (JSON)? | Dayandığı kayıtlar: `TT-003`, `ISDLC-2026-009` |
| `Status` | `SuggestionStatus` | `Pending`, `Accepted`, `AcceptedWithEdits`, `Rejected` |
| `ReviewedById`, `ReviewedAt` | int?, DateTime? (UTC) | |
| `ReviewNote` | string? | Ret gerekçesi |
| `CreatedEntityId` | int? | Kabulde oluşan gerçek kaydın `Id`'si |

- `AcceptedWithEdits`, kalite ölçümü için ayrıca tutulur: hangi ajanın ne sıklıkla düzeltildiği buradan görülür.
- Kabul edilen önerinin gerçek kaydı, **inceleyen kişi adına** normal servisle oluşturulur; denetim kaydında oluşturan o kişidir.

Kesin model, ajan geliştirmesine başlanırken `data-model.md`'ye eklenir. Yeni kavramlar önce AGENTS.md sözlüğüne yazılır.

## Güvenlik ve KVKK

- Müşteri verisinin hangi yapay zekâ servisine, hangi koşullarda gönderileceği şirket kararıdır; karar alınmadan geliştirmeye başlanmaz.
- Kişisel veriler (ad, telefon, e-posta) modele gönderilmeden önce maskelenir.
- Her ajan çalıştırmasında hangi verinin gönderildiği denetim kaydına yazılır.
- `RequiresNda` işaretli projelerde ajan varsayılan olarak kapalıdır.

## Kalite ölçümü

- Her ajan tipi için örnek girdi ve beklenen çıktıdan oluşan bir test seti (eval) hazırlanır.
- Gerçek kullanımda şunlar ölçülür: taslakların ne kadarı onaylandı, ne kadar düzeltildi, ne kadarı reddedildi.
- Prompt veya model değiştiğinde test seti yeniden çalıştırılır; `AgentRun.PromptVersion` bunun için tutulur.

## Eksik malzeme

| Eksik | Neden gerekli |
| --- | --- |
| Doküman şablonlarının içeriği (bölümler, her bölümde ne yazılacağı) | Doküman ajanı için; prototipte yalnızca doküman adları var |
| Gereksinim İç Onay Kontrol Listesi maddeleri | Gözden geçirme ajanı için |
| Proje Kabul Değerlendirme Formu ölçütleri | Tahmin ajanının kabul puanlaması için |
| Doküman içeriğinin saklanması | MVP'de `Documents` yalnızca durum takip ediyor; ajanın taslağı yazacağı bir yer gerekli |
| Geçmiş proje verisi | Tahmin ve risk önerileri için; başlangıçta seed'deki örnek projeler kullanılabilir |
| Model ve KVKK kararı | Şirket onayı |

## MVP'de şimdiden dikkat edilecekler

MVP'ye ajan eklenmez; ama şu kurallar ileride ajan eklemeyi kolaylaştırır:

1. İş kuralları yalnızca `Application` servislerinde olur; controller veya frontend'de iş kuralı yazılmaz.
2. Her değişiklik `Audit` ile kaydedilir.
3. Her modülün `Create...Dto`'su tek başına anlamlı ve doğrulanabilir olur; ajan önerileri bu DTO'ların aynısıdır.
4. Toplantı tutanakları ve notlar serbest metin olarak saklanır; ajanın girdisi bunlardır.

## Uygulamada: toplantı yardımcısı

Denemenin ilk ürünü. Yeni tutanak formunda ham toplantı notları yazılır, "Yapay zekâ ile karar ve aksiyonları çıkar"
butonu önerileri forma doldurur; kişi düzenleyip tutanağı normal şekilde kaydeder. Aynı istekte ajanın ikinci görevi
(tutanak taslağı) toplantı türünü, katılımcıları ve gündemi önerir; form bunları yalnızca kişi doldurmadıysa yazar.
Taslak görevi başarısız olursa kararlar ve aksiyonlar yine gelir.

```
Tutanak formu ──► POST /api/v1/projects/{id}/meetings/suggestions (.NET: giriş, proje aktif mi, mevcut adım)
                     │  X-Agent-Key ile
                     ▼
                  ai/ servisi: POST /v1/meeting/suggestions (maskeleme, model çağrısı, doğrulama)
                     │
                     ▼
                  .NET: kararlar / aksiyonlar / diğerleri; sorumlu adı aktif bir kullanıcıysa ownerUserId
                     │
                     ▼
                  Form doldurulur ──► kişi kontrol eder ──► POST /meetings (normal kayıt, normal doğrulama)
```

- Bu adımda `AgentRun` / `AgentSuggestion` tabloları yoktur: inceleme formun kendisidir, kaydedilmeyen öneri iz bırakmaz.
  Kabul / ret oranlarını ölçmek gerektiğinde (yol haritası adım 4) tablolar eklenir.
- Servis çalışmıyorsa `GET /api/v1/agents/status` kapalı döner ve buton görünmez.
- Sözleşme: [open-issues-meetings-api.md](open-issues-meetings-api.md) → "Yapay zekâ önerileri".

## Uygulamada: risk yardımcısı

Riskler sekmesinde (akış 4.8, Risk Analiz Formu) iki kullanım:

| Buton | Ne yapar | Ajan görevleri |
| --- | --- | --- |
| "Yapay zekâ ile risk öner" | Yapıştırılan metinden (toplantı notu, e-posta) riskleri bulur; her biri için kategori, olasılık, etki, gerekçe, önleyici aksiyon ve dayanak cümle önerir. "Forma aktar" risk formunu doldurur; kişi kontrol edip kaydeder. Liste açık kalır, birkaç risk art arda eklenebilir | bul → puanla → önlem öner (3 model çağrısı) |
| "Yapay zekâ ile puan ve aksiyon öner" (risk formunda) | Kişinin yazdığı risk için olasılık, etki ve önleyici aksiyon önerir. Puanı forma işler; önleyici aksiyon alanı doluysa üzerine yazmaz, öneriyi yanında gösterir | puanla → önlem öner (2 model çağrısı) |

- Akış toplantı yardımcısıyla aynıdır: tarayıcı → .NET (giriş, proje aktif mi, mevcut adım) → `ai/` servisi
  (`/v1/risk/suggestions`, `/v1/risk/assessment`) → .NET doğrular (puan 1–5, kategori, başlık uzunluğu) → form.
- Skor ve seviye modelden istenmez; `RiskRules` ile hesaplanır.
- Sözleşme: [risks-api.md](risks-api.md) → "Yapay zekâ önerileri".

## Deneme: toplantı ve risk ajanları (komut satırı)

Ajan fikrini gerçek kodla sınamak için yapılan deneme. Kod: [`ai/`](../ai/README.md). Komut satırı hâlâ çalışır;
eval'ler ve risk ajanı buradan kullanılır.

| Konu | Karar |
| --- | --- |
| Neden bu ajan | OpenIssues API'si hazır; çıktı (`CreateOpenIssueDto`) gerçek doğrulamadan geçirilerek hemen sınanabilir |
| Girdi | Toplantı notu (metin dosyası), toplantı tarihi, proje (`GET /projects/{id}/process` ile okunur) |
| Çıktı | Açık konu önerileri: tür, konu, açıklama, sorumlu, termin ve dayanak cümle |
| Onay | Komut satırında öneriler tek tek onaylanır; onaylananlar `POST /open-issues` ile kaydedilir. Kararlar `Closed` kaydedilir (tutanak kuralı) |
| Model | Gemini, `LlmClient` arayüzünün arkasında; başka sağlayıcıya geçiş yeni bir sınıf eklemekle yapılır |
| Güvenlik | Yalnızca sahte veri. E-posta, telefon, IBAN ve TCKN modele gitmeden gizlenir, cevapta geri açılır |
| Ölçüm | `ai/evals/meeting/` altındaki sahte vakalarla: bulunan / kaçırılan / fazla öneri, yanlış termin |
| Backend değişikliği | Yok. `AgentRun` ve `AgentSuggestion` tabloları denemenin sonucuna göre, ekip onayıyla eklenir |

Denemede iki ajan ve beş görev var:

| Ajan | Görev | Prompt | Çıktı | Kayıt |
| --- | --- | --- | --- | --- |
| Toplantı | Açık konu önerileri | `meeting/open_issues.v1` | `CreateOpenIssueDto` | Komut satırında onaylanır, API'ye yazılır |
| Toplantı | Tutanak taslağı (tür, katılımcılar, gündem) | `meeting/minutes.v1` | `CreateMeetingDto` önizlemesi; kararlar ve aksiyonlar ilk görevden gelir | Uygulamada: tutanak formunun boş alanlarını doldurur |
| Risk | Riskleri bulma | `risk/identify.v2` | Risk başlığı, açıklama, kategori, dayanak | Uygulamada: risk formuna aktarılır |
| Risk | Olasılık ve etki puanlama (1-5) | `risk/assess.v1` | Puan ve gerekçe; skor kodda hesaplanır | Uygulamada: risk formuna aktarılır |
| Risk | Önleyici aksiyon | `risk/mitigate.v2` | Aksiyon metni | Uygulamada: risk formuna aktarılır |

Risk ajanının üç görevi sırayla çalışır: bul, puanla, önlem öner. Puanlama ve önlem görevleri, kişinin yazdığı
riskte tek başına da çalışır (risk formundaki "puan ve aksiyon öner" butonu). Risk alanları Risks sözleşmesiyle
hizalıdır; `identify.v2` kategori de önerir (`identify.v1` iz için duruyor).

Denemenin sonunda ekibe şu sorularla dönülür: Ajan beklenen kayıtların ne kadarını buluyor? Ne sıklıkla uyduruyor
(dayanağı notlarda olmayan öneri)? İnceleme süresi elle girmekten kısa mı?

## Yol haritası

| Adım | İçerik | Ön koşul |
| --- | --- | --- |
| A | **Deneme:** toplantı ajanı, komut satırından onay ✅ | OpenIssues API'si (hazır) |
| 0 | MVP tamamlanır | — |
| 1 | Ekip kararı: Python servisi ✅ (onaylandı); `AgentRun` / `AgentSuggestion` tabloları ve model sağlayıcısı bekliyor | Deneme sonuçları |
| 2 | Toplantı ajanı uygulamaya girer: tutanak formunda öneri butonu ✅; risk ajanı Riskler sekmesinde ✅; öneri paneli sonra | Meetings ve Risks modülleri (hazır) |
| 3 | Pilot: Aşama 4'te doküman ajanı + gözden geçirme ajanı | Şablonlar, kontrol listesi, doküman içeriği saklama |
| 4 | Ölçüm: önerilerin kabul, düzeltme ve ret oranları | Gerçek kullanım |
| 5 | Diğer ajan tipleri ve `StepAutomation` ayar ekranı | Pilotun başarılı olması |
| 6 | RAG ile geçmiş projelerden öğrenme | Yeterli sayıda kapanmış proje |

## Açık kararlar

1. Hangi yapay zekâ servisi kullanılacak, verinin gönderilmesine şirket izin veriyor mu?
   Şirket kullanımı için doğrudan API anahtarı yerine kurumsal bulut servisi (Azure, Vertex AI, Bedrock) tercih edilir.
2. ~~Ajanlar ayrı bir Python servisi olarak mı yazılacak?~~ **Evet** (ekip onayı). AGENTS.md yığın ve klasör bölümleri güncellendi, CI'a Python işi eklendi.
3. Ajan modülünün kod adı: **`Agents`** (AGENTS.md'ye eklendi). Sözlük karşılıkları (`AgentType`, `StepAutomation`, `AgentRun`, `AgentSuggestion`) tablolar eklenirken yazılır.
4. Ajan çalıştırmaları senkron mu (kullanıcı bekler), arka planda mı (bildirimle haber verilir) olacak?
5. Talep Sahibi'ne giden içerikler (ör. 4.6'da sunulan dokümanlar) ajan taslağından üretildiyse bu belirtilecek mi?
