# Assessment Modülü — API Sözleşmesi (Faz 4 · B2: Seviye & Uygunluk)

Şirketin yeni akışındaki iki değerlendirme: **müşteri uygunluğu** (yeni yazılım projesi akışı, 4. adım; yalnızca dış talep) ve
**doküman seviyesi** (5. adım). İkisi de soru bankasının formlarıyla doldurulur ([question-bank-api.md](question-bank-api.md));
bu modül formların **sonucunu** hesaplar ve seviyeyi projeye uygular. Sorumlu: Baran. Plan: [phase-4-plan.md](phase-4-plan.md) → B2.

Durum: **taslak, ekip onayı bekliyor.** Faz 1'deki sürüm (6 soruluk seviye ve 6 ölçütlü kabul puanlaması; #96–#98) bu sözleşmeyle
**kalkar**; yerine şirketin kendi formları gelir.

Bütün uç noktalar AGENTS.md'deki ortak kurallara uyar: `/api/v1/` öneki, enum'lar metin, hatalar `ProblemDetails`, JSON alanları camelCase.

## Ne değişiyor

| | Faz 1 (kalkıyor) | Faz 4 (bu sözleşme) |
| --- | --- | --- |
| Seviye | 6 soru, her biri 1–3; puan 6–18 | **Seviye Belirleme Şablonu (008) v3.0**: bölüm 1 "Doküman seviyesi" (dış talepte 13, diğerlerinde 12 soru), 5'er puanlı seçenek, toplam en fazla 60 |
| Araştırma süresi | yok | Aynı formun bölüm 2'si "Araştırma seviyesi" (13 / 12 soru), toplam en fazla 60 → 1–30 gün |
| Kabul | 6 ağırlıklı ölçüt, 1–5; Kabul / Koşullu / Ret | **Müşteri Uygunluk Değerlendirme Anket Formu (006) v1.0**: 10 soru, toplam 100 → Uygun / Şartlı uygun / Riskli / Uygun değil. Yalnızca dış talepte |
| Veri | `ProjectAssessment` tablosu (sütun başına bir cevap) | Soru bankası: `Questionnaire` + `QuestionAnswer` (setler `level`, `customer-fit`) |
| Ekran | "Seviye & Kabul" sekmesi | "Seviye & Uygunluk" sekmesi; ortak `app-questionnaire` bileşeni + sonuç kartları |

## Sonuçlar

Soru bankasının ortak kuralları geçerlidir (cevaplar tek tek kaydedilir, alt kategori projenin talep kaynağıdır, yazma yalnızca aktif projede).
Bu modül `QuestionnaireDto.result` alanını doldurur. Eşikler formun seed dosyasından (`bands`) okunur, koda yazılmaz.

### Seviye (`level`)

| Bölüm | Puan | Sonuç (formdan) |
| --- | --- | --- |
| 1 · Doküman seviyesi | Seçilen seçeneklerin puan toplamı | 1–20 → Seviye 1 · 21–40 → Seviye 2 · 41–60 → Seviye 3 |
| 2 · Araştırma seviyesi | Seçilen seçeneklerin puan toplamı | 1–10 → 1 gün · 11–20 → 2 · 21–30 → 5 · 31–40 → 10 · 41–50 → 20 · 51–60 → 30 gün |

- Bir bölümün sonucu, o bölümün **bütün soruları** cevaplanınca çıkar; eksik soru varsa puan gösterilir, sonuç boştur.
- Bazı seçenekler 0 puandır; toplam ilk aralığın altında kalırsa (0) ilk aralığın sonucu verilir (Seviye 1, 1 gün).
- **"Seviyeyi uygula"** iki bölümün sonucunu projeye yazar: `Project.Level` ve `Project.ResearchDays`. Cevaplar sonradan değişirse
  proje kendiliğinden değişmez (Faz 1'deki kural); ekran "önerilen sonuç projedekinden farklı" uyarısı gösterir.
- Seviye her aşamada yeniden uygulanabilir. Seviyeyi yalnızca bu modül yazar.

### Müşteri uygunluğu (`customer-fit`)

| Puan (0–100) | Sonuç | Formdaki açıklama |
| --- | --- | --- |
| 85–100 | `Fit` — Uygun | Yüksek olgunluk düzeyi, projeye doğrudan başlanabilir |
| 70–84 | `ConditionallyFit` — Şartlı uygun | Belirli konularda ön hazırlık ve danışmanlık gerekebilir |
| 50–69 | `Risky` — Riskli | Kritik eksikler mevcut, başarısı için aksiyon planı gerekir |
| 0–49 | `NotFit` — Uygun değil | Altyapı, ilgili veya stratejik uygunluk açısından yetersizlik |

- Puan, 10 sorunun puanlarının toplamıdır; sonuç bütün sorular cevaplanınca çıkar.
- Sorular yalnızca **dış talep** alt kategorisindedir: kurum içi ve ilişkili şirket projelerinde form boş döner, sekme
  "Bu talep için müşteri uygunluk anketi doldurulmaz." yazar.
- Sonuç bir **öneridir**: 4. adımdaki "Müşteri uygun veya şartlı uygun mu?" kararını iş analisti `Process` ekranında verir.
  Bu modül adımı ilerletmez, karar da sonucu değiştirmez (karar 5).

## Uç noktalar

Formlar soru bankasının uç noktalarıyla okunur ve doldurulur; bu modül yalnızca seviyeyi uygular.

| İstek | Açıklama | Başarılı cevap |
| --- | --- | --- |
| `GET /projects/{projectId}/questionnaires/level` | Seviye formu; `result` = `LevelResultDto` | `200` `QuestionnaireDto` |
| `PUT /projects/{projectId}/questionnaires/level/answers/{questionId}` | Seviye sorusunun cevabı | `200` `QuestionnaireDto` |
| `GET /projects/{projectId}/questionnaires/customer-fit` | Uygunluk anketi; `result` = `CustomerFitResultDto` | `200` `QuestionnaireDto` |
| `PUT /projects/{projectId}/questionnaires/customer-fit/answers/{questionId}` | Anket sorusunun cevabı | `200` `QuestionnaireDto` |
| `POST /projects/{projectId}/assessment/apply-level` | Seviye formunun sonucunu projeye yaz (`Level`, `ResearchDays`) | `200` `QuestionnaireDto` (seviye formu) |

**Kalkan uç noktalar:** `GET /assessment/definition`, `GET /projects/{id}/assessment`, `PUT .../assessment/level-answers`,
`PUT .../assessment/acceptance-scores`. Ekran (B2) ve gösterge paneli (aşağıda) aynı PR'da yeni uç noktalara geçer.

## DTO'lar

```jsonc
// QuestionnaireDto.result — seviye formu (setCode "level")
{
  "documentLevel": { "score": 23, "maxScore": 60, "answered": 13, "listed": 13, "level": 2 },   // eksik soru varsa level null
  "research":      { "score": 14, "maxScore": 60, "answered": 10, "listed": 13, "days": null }, // eksik soru varsa days null
  "appliedLevel": 2,          // Project.Level; uygulanmadıysa null
  "appliedResearchDays": null // Project.ResearchDays; uygulanmadıysa null
}

// QuestionnaireDto.result — müşteri uygunluk anketi (setCode "customer-fit")
{ "score": 78, "maxScore": 100, "answered": 10, "listed": 10, "rating": "ConditionallyFit" }  // eksik soru varsa rating null

// POST .../assessment/apply-level — gövde yok; her zaman formun sonucu uygulanır
```

`maxScore`, listelenen soruların en yüksek seçenek puanlarının toplamıdır (formda 60 ve 100).

| Durum | Kural | Hata mesajı |
| --- | --- | --- |
| `apply-level` | Seviye formunun iki bölümü de tamamlanmış olmalı | "Seviyeyi uygulamak için bütün sorular cevaplanmalıdır." (400) |

## Hata durumları

| Durum | Kod | Örnek `detail` |
| --- | --- | --- |
| Proje bulunamadı | `404` | "Proje bulunamadı." |
| Sorular eksikken seviye uygulandı | `400` | "Seviyeyi uygulamak için bütün sorular cevaplanmalıdır." |
| Proje aktif değil | `409` | "Proje aktif değil; kayıt eklenemez veya değiştirilemez." |
| Proje aynı anda değiştirildi (seviye uygulanırken) | `409` | Ortak `RowVersion` mesajı |

Cevap kaydındaki hatalar soru bankasınınkilerle aynıdır ([question-bank-api.md](question-bank-api.md) → Hata durumları).

## Veri

| Değişiklik | Neden |
| --- | --- |
| Yeni soru seti `customer-fit`: `Seed/question-sets/customer-fit.json` (form 006, 10 soru, `External` alt kategorisi, eşikler `bands`'de) | Anket soru bankasında tutulur (question-bank-api.md → karar 8) |
| `Project.ResearchDays` (`int?`) | Araştırma süresi seviyeyle birlikte uygulanır (karar 3) |
| `ProjectAssessments` tablosu kaldırılır | Faz 1 formu kalktı (karar 1) |
| `Level` ve `ResearchDays` değişiklikleri denetim kaydına `Project` olarak yazılır | Mevcut kural |

## Gösterge paneli

"Kabul kararı bekleyen talepler" listesi ([dashboard-api.md](dashboard-api.md) → karar 13) bugün Faz 1 kabul puanlamasını gösteriyor.
B2'nin API PR'ında, panelin derlenmeye devam etmesi için satırdaki `weightedScore` / `recommendation` yerine uygunluk anketinin
`score` / `rating` alanları gelir (aynı adımda, 4. adımda bekleyen dış talepler). Listenin geri kalanı Dide'nin D9 işidir. *(Dide)*

## Kod yerleşimi

| Katman | Dosyalar |
| --- | --- |
| `Isdlc.Domain/Assessment/` | `CustomerFitRating` (`ProjectAssessment` ve `AcceptanceRecommendation` kalkar) |
| `Isdlc.Application/Assessment/` | `IAssessmentService` (`ApplyLevelAsync`), `LevelRules`, `CustomerFitRules` (saf; eşikleri `QuestionSet.Bands`'den okur), sonuç DTO'ları |
| `Isdlc.Application/QuestionBank/` | `QuestionnaireService`, `result`'u setin kural sınıfından alır (`level` → `LevelRules`, `customer-fit` → `CustomerFitRules`, diğerleri null) |
| `Isdlc.Infrastructure/Seed/question-sets/` | `customer-fit.json` |
| `Isdlc.Api/Controllers/` | `AssessmentController` (yalnızca `apply-level`) |
| `frontend/src/app/features/assessment/` | "Seviye & Uygunluk" sekmesi ve menü sayfası: iki `app-questionnaire` + sonuç kartları + "Seviyeyi uygula" |

## Ekranlar

| Ekran | İçerik |
| --- | --- |
| Proje detayı → **Seviye & Uygunluk** sekmesi | Üstte uygunluk anketi (yalnızca dış talepte) ve sonuç kartı: puan / 100, sonuç rozeti, formdaki açıklama. Altta seviye formu (bölüm adları "Doküman seviyesi", "Araştırma seviyesi") ve sonuç kartı: iki bölümün puanı, önerilen seviye ve süre, projedeki değerler, "Seviyeyi projeye uygula" |
| Menüdeki **Seviye & Uygunluk** sayfası | Proje seçici + aynı sekme (bugünkü "Seviye & Kabul" sayfası gibi) |

## Uygulama sırası (her biri ayrı PR)

1. Bu doküman (ekip onayı)
2. Seed (`customer-fit.json`), `Project.ResearchDays`, `ProjectAssessments` tablosunun kaldırılması (tek migration)
3. `LevelRules`, `CustomerFitRules` ve testleri; `result` alanı, `apply-level`; eski uç noktalar ve gösterge paneli satırı
4. Ekran: "Seviye & Uygunluk" sekmesi ve menü sayfası

## Ekip onayı bekleyen kararlar

1. **Faz 1 formu kalkar ve `ProjectAssessments` tablosu migration'la kaldırılır.** Uygulama henüz canlıda değil; tablodaki kayıtlar
   geliştirme ve örnek veridir. ("Kayıt silinmez" kuralı kullanıcı işlemleri içindir; kalkan bir özelliğin tablosu için değil.)
   Alternatif: tablo kalır, hiçbir yerden okunmaz.
2. **Anketin soru başına puanlaması formda yok:** form yalnızca toplamı (100) ve aralıkları veriyor. Öneri: her soru **0–10** puan
   (11 seçenek: 0, 1, … 10). Analistlere sorulacak ([phase-4-plan.md](phase-4-plan.md) → Açık konular'a eklenir); cevap gelirse yalnızca
   `customer-fit.json` değişir, kod değişmez. Not: form 006 "Onaylanmamış Dokümanlar" klasöründe, sürümü v1.0.
3. **Araştırma süresi de projeye yazılır** (`Project.ResearchDays`), seviyeyle aynı düğmeyle. Proje özetinde ve sonra kaynak planlamada (B6) görünür.
   Alternatif: yalnızca sekmede gösterilir, saklanmaz.
4. **Seviye yalnızca formun sonucu olarak uygulanır** (Faz 1'deki gibi); analist farklı bir seviye seçemez. Cevaplar değişince proje kendiliğinden değişmez.
5. **`Process`'e bağlılık yok:** 4. ve 5. adımlar anket ya da seviye tamamlanmadan da ilerletilebilir; 4. adımdaki Evet/Hayır kararını
   iş analisti verir. İstenirse "uygun değil sonucunda Evet verilemez" gibi bir kural sonradan `Process` ile eklenir.
6. **Ek geliştirme (`add-on`) de aynı formları kullanır** (question-bank-api.md → karar 4); uygunluk anketi yine yalnızca dış talepte.
