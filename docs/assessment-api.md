# Assessment Modülü — API Sözleşmesi

Seviye belirleme (akış 2.4, Proje Seviye Belirleme Formu) ve proje kabul puanlaması (akış 3.1, Proje Kabul Değerlendirme
Formu Bölüm 2/3). Veri modeli: [data-model.md](data-model.md) → `ProjectAssessment`. Durum: **taslak, ekip onayı bekliyor.**
Sorumlu: Baran.

Bütün uç noktalar AGENTS.md'deki ortak kurallara uyar: `/api/v1/` öneki, enum'lar metin, hatalar `ProblemDetails`,
JSON alanları camelCase. Değerlendirme projenin alt kaynağıdır (`/api/v1/projects/{projectId}/assessment`).

## Ne yapar

| Bölüm | Akış | Girdi | Çıktı |
| --- | --- | --- | --- |
| Seviye belirleme | 2.4 | 6 soru, her biri 1–3 | Puan (6–18) ve **önerilen seviye** (1–3). "Seviyeyi uygula" ile `Project.Level` olur |
| Kabul puanlaması | 3.1 | 6 ağırlıklı ölçüt, her biri 1–5 | **Ağırlıklı ortalama** (1,00–5,00) ve **öneri** (kabul / koşullu kabul / ret-erteleme) |

Kabul önerisi bir karar değildir: nihai kararı Yazılım Departman Yöneticisi 3.2'de `Process` üzerinden verir.
Cevaplar yarım bırakılabilir; seviye ve öneri yalnızca bütün sorular cevaplanınca hesaplanır.

## Sorular ve ölçütler

Prototipteki forma göre. Metinler, seçenekler, ağırlıklar ve eşikler **tek yerde** tutulur (`Assessment/AssessmentRules.cs`)
ve ekran onları `GET /assessment/definition` ile alır; kurumun Seviye Şablon 2 ölçütleri gelince yalnızca orası değişir.

**Seviye soruları** (her biri 1–3):

| Alan | Soru | 1 | 2 | 3 |
| --- | --- | --- | --- | --- |
| `effort` | Tahmini efor | ≤ 20 adam-gün | 21–80 adam-gün | > 80 adam-gün |
| `screensAndModules` | Modül / ekran sayısı | 1–5 ekran, tek modül | 6–20 ekran veya 2–3 modül | > 20 ekran veya 4+ modül |
| `integrations` | Dış sistem entegrasyonu | Yok | 1–2 servis | 3+ servis / çift yönlü |
| `userRoles` | Kullanıcı rolü ve kitlesi | Tek rol, iç kullanıcı | 2–4 rol | 5+ rol veya son kullanıcı / vatandaş |
| `dataSensitivity` | Veri hassasiyeti (KVKK) | Kişisel veri yok | Kişisel veri var | Özel nitelikli kişisel veri / finansal veri |
| `hardwareDependency` | Donanım / cihaz bağımlılığı | Yok | Standart cihaz (yazıcı, barkod) | Özel cihaz / IoT |

**Önerilen seviye:** efor 3 (> 80 adam-gün) **veya** puan ≥ 14 → Seviye 3; puan 9–13 → Seviye 2; daha azı → Seviye 1.

**Kabul ölçütleri** (her biri 1–5):

| Alan | Ölçüt | Ağırlık |
| --- | --- | --- |
| `strategicFit` | Stratejik uyum (şirket hedefleri, ürün yol haritası) | %20 |
| `commercialValue` | Ticari değer / gelir potansiyeli | %20 |
| `technicalFeasibility` | Teknik uygulanabilirlik ve mevcut yetkinlik | %20 |
| `resourceAndSchedule` | Kaynak ve takvim uygunluğu | %15 |
| `customerFit` | Müşteri uygunluğu (ödeme, iletişim, geçmiş) | %15 |
| `riskLevel` | Risk düzeyi (yüksek puan = düşük risk) | %10 |

**Ağırlıklı ortalama** = Σ (puan × ağırlık) / 100, iki ondalıkla. **Öneri:** ≥ 3,50 `Accept` (Kabul önerilir);
2,50–3,49 `Conditional` (Koşullu kabul); < 2,50 `RejectOrPostpone` (Ret / erteleme önerilir).

## Ortak kurallar

- **Okuma:** giriş yapmış her kullanıcı.
- **Yazma:** giriş yapmış her aktif kullanıcı, **yalnızca aktif projede** (değilse 409). Proje kayıtlarıyla aynı kural.
- Bir projenin tek değerlendirmesi vardır; ilk cevap kaydedilince oluşur. Silinmez; cevaplar değiştirilebilir.
- Cevaplar değişince seviye **kendiliğinden değişmez**: `Project.Level` yalnızca "Seviyeyi uygula" ile değişir.
  Böylece projenin seviyesi, biri bilerek uygulamadan oynamaz.
- Seviye projenin her aşamasında yeniden uygulanabilir (ör. 5.4'te kapsam değişip 4.1'e dönüldüğünde).
- **Eş zamanlı düzenleme:** son kaydeden kazanır. "Seviyeyi uygula" `Project.RowVersion`'a takılır (aynı anda değişen proje → 409).
- Her değişiklik denetim kaydında görünür: cevaplar `ProjectAssessment` olarak, seviye `Project.Level` olarak.

## Uç noktalar

| İstek | Açıklama | Başarılı cevap |
| --- | --- | --- |
| `GET /assessment/definition` | Sorular, seçenekler, ölçütler, ağırlıklar ve eşikler (ekran için) | `200` `AssessmentDefinitionDto` |
| `GET /projects/{projectId}/assessment` | Projenin değerlendirmesi; hiç cevap yoksa boş alanlarla döner | `200` `ProjectAssessmentDto` |
| `PUT /projects/{projectId}/assessment/level-answers` | Seviye sorularının cevaplarını kaydet (yarım olabilir) | `200` `ProjectAssessmentDto` |
| `PUT /projects/{projectId}/assessment/acceptance-scores` | Kabul ölçütlerinin puanlarını kaydet (yarım olabilir) | `200` `ProjectAssessmentDto` |
| `POST /projects/{projectId}/assessment/apply-level` | Önerilen seviyeyi projeye uygula (`Project.Level`) | `200` `ProjectAssessmentDto` |

## DTO'lar

```jsonc
// ProjectAssessmentDto
{
  "projectId": 1,
  "level": {
    "answers": { "effort": 2, "screensAndModules": 2, "integrations": 3, "userRoles": 2, "dataSensitivity": 2, "hardwareDependency": 1 },
    "answeredCount": 6,          // 0–6
    "score": 12,                 // bütün sorular cevaplanmadıysa null
    "suggestedLevel": 2,         // bütün sorular cevaplanmadıysa null
    "appliedLevel": 2            // Project.Level; uygulanmadıysa null
  },
  "acceptance": {
    "scores": { "strategicFit": 4, "commercialValue": 4, "technicalFeasibility": 3, "resourceAndSchedule": 3, "customerFit": 5, "riskLevel": 3 },
    "answeredCount": 6,
    "weightedScore": 3.70,       // bütün ölçütler puanlanmadıysa null
    "recommendation": "Accept"   // bütün ölçütler puanlanmadıysa null
  },
  "updatedBy": { "id": 1, "fullName": "Elif Yılmaz" },   // hiç kaydedilmediyse null
  "updatedAt": "2026-11-10T09:12:00Z"                    // hiç kaydedilmediyse null
}

// SaveLevelAnswersDto (PUT .../level-answers) — her alan isteğe bağlı (null = cevaplanmadı), doluysa 1–3
{ "effort": 2, "screensAndModules": 2, "integrations": 3, "userRoles": 2, "dataSensitivity": 2, "hardwareDependency": null }

// SaveAcceptanceScoresDto (PUT .../acceptance-scores) — her alan isteğe bağlı, doluysa 1–5
{ "strategicFit": 4, "commercialValue": 4, "technicalFeasibility": 3, "resourceAndSchedule": 3, "customerFit": 5, "riskLevel": 3 }

// POST .../apply-level — gövde yok; seviye her zaman önerilen seviyedir

// AssessmentDefinitionDto — ekranın soruları ve ölçütleri çizmesi için
{
  "levelQuestions": [ { "key": "effort", "title": "Tahmini efor",
                        "options": [ { "value": 1, "label": "≤ 20 adam-gün" }, { "value": 2, "label": "21–80 adam-gün" }, { "value": 3, "label": "> 80 adam-gün" } ] } ],
  "levelRule": "Efor 80 adam-günü aşıyorsa veya puan 14 ve üstüyse Seviye 3; puan 9–13 Seviye 2; daha azı Seviye 1.",
  "acceptanceCriteria": [ { "key": "strategicFit", "title": "Stratejik uyum (şirket hedefleri, ürün yol haritası)", "weight": 20 } ],
  "acceptanceRule": "Ağırlıklı ortalama 3,50 ve üstü kabul; 2,50–3,49 koşullu kabul; 2,50'nin altı ret / erteleme önerisi."
}
```

| Alan | Kural | Hata mesajı |
| --- | --- | --- |
| Seviye cevapları | İsteğe bağlı; doluysa 1–3 | "Cevap 1 ile 3 arasında olmalıdır." |
| Kabul puanları | İsteğe bağlı; doluysa 1–5 | "Puan 1 ile 5 arasında olmalıdır." |
| `apply-level` | Bütün seviye soruları cevaplanmış olmalı | "Seviyeyi uygulamak için bütün sorular cevaplanmalıdır." |

## Hata durumları

| Durum | Kod | Örnek `detail` |
| --- | --- | --- |
| Proje bulunamadı | `404` | "Proje bulunamadı." |
| Cevap veya puan aralık dışında | `400` | `errors.effort`: "Cevap 1 ile 3 arasında olmalıdır." |
| Sorular eksikken seviye uygulandı | `400` | "Seviyeyi uygulamak için bütün sorular cevaplanmalıdır." |
| Proje aktif değil | `409` | "Proje aktif değil; kayıt eklenemez veya değiştirilemez." |
| Proje aynı anda değiştirildi (seviye uygulanırken) | `409` | Ortak `RowVersion` mesajı |

## Bildirim noktaları

MVP'de yok. Kabul puanlaması bitince yöneticiye ayrıca bildirim gönderilmez: proje 3.2'ye gelince `Process` zaten
"onayınızı bekliyor" bildirimini gönderiyor ([process-api.md](process-api.md) → Bildirim noktaları).

## Kod yerleşimi

| Katman | Dosyalar |
| --- | --- |
| `Isdlc.Domain/Assessment/` | `ProjectAssessment`, `AcceptanceRecommendation` |
| `Isdlc.Application/Assessment/` | `IAssessmentService`, `AssessmentService`, DTO'lar, `AssessmentRules` (sorular, ağırlıklar, seviye ve öneri hesabı; saf kurallar) |
| `Isdlc.Infrastructure/Assessment/` | `ProjectAssessmentConfiguration` |
| `Isdlc.Api/Controllers/` | `AssessmentController` (yollar tam yazılır) |

## Ekranlar (frontend)

| Ekran | Yer |
| --- | --- |
| Seviye & Kabul (proje detayı sekmesi): seviye sihirbazı, "Seviyeyi uygula", kabul puanlama tablosu ve öneri | `features/assessment` |
| Menüdeki "Seviye & Kabul" sayfası: proje seçici + aynı bileşen (Açık Konular ve Risk Yönetimi sayfaları gibi) | `features/assessment` |

Bileşen, `Projects` modülünün (Dide) proje detay sayfasına `projectId` alan bir sekme olarak eklenir; kapanmış projede salt okunurdur.

## Kapsam dışı

- **Öncelik (`Project.Priority`):** sözleşmeye göre 3.2'de `Process` belirler ([projects-api.md](projects-api.md) → Erva ile alınan kararlar).
  Şu an Process 3.2'de öncelik almıyor; bu `Process`'in (Erva) işi, `Assessment` önceliğe dokunmaz.
- **Seviye–doküman matrisi:** takip edilen dokümanlar seviyeye bağlı değildir ([data-model.md](data-model.md) → karar 14).
- **Müşteri uygunluk formu ve ön fizibilite raporu** (3.1'in diğer dokümanları): `Documents`'ta durum olarak takip edilir.
- **Gösterge paneli:** "seviyeye göre proje dağılımı, kabul puanı bekleyen talepler" ([dashboard-api.md](dashboard-api.md) → Sonra eklenecekler)
  bu modül bittikten sonra ayrı bir PR'da.

## Ekip onayı bekleyen kararlar

1. **Bir proje, bir değerlendirme:** cevaplar ayrı satırlar yerine `ProjectAssessment` tablosunda sütun olarak tutulur.
   Sorular sabit olduğu için böylesi basit ve denetim kaydında "Tahmini efor 2 → 3" gibi okunur. Sorular kurumdan değişirse migration gerekir.
2. **Seviye yalnızca önerilen seviye olarak uygulanır** (prototipteki gibi); analist farklı bir seviye seçemez.
   Gerekirse sonradan `apply-level`'e "gerekçeli farklı seviye" eklenebilir.
3. **Cevaplar değişince seviye kendiliğinden değişmez;** yeniden "Seviyeyi uygula" gerekir.
4. **Yazma yetkisi** proje kayıtlarıyla aynı: her aktif kullanıcı. 2.4 ve 3.1'in sorumlusu İş Analisti olsa da
   ekranı başkası da doldurabilir.
5. **Process'e bağlılık yok:** 2.4 ve 3.1 adımları seviye uygulanmadan ya da puanlama bitmeden de tamamlanabilir.
   İstenirse "2.4 tamamlanmadan önce seviye uygulanmış olmalı" kuralı `Process` ile birlikte sonradan eklenir (Erva).
