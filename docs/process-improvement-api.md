# Süreç İyileştirme (Faz 4 · E5) — API Sözleşmesi

Süreç iyileştirme önerilerinin kaydı, önceliklendirmesi, çalışma dokümanı ve pilot değerlendirmesi.
Durum: **taslak, ekip onayı bekliyor.** Sorumlu: Erva. Plan: [phase-4-plan.md](phase-4-plan.md) → E5.
Akış: `process-improvement` ([process-api.md](process-api.md) → Akışlar). Çapraz testler: Dide (#123).

Bütün uç noktalar AGENTS.md'deki ortak kurallara uyar: `/api/v1/` öneki, enum'lar metin, tarihler UTC / ISO 8601,
hatalar `ProblemDetails`, listeler `items` + `totalCount`, JSON alanları camelCase, doğrulama hataları `errors.<alanAdı>` altında Türkçe.

## Kaynaklar ve formlar

Akışın kutularında üç form geçer; **hiçbiri henüz klasörde yok** (phase-4-plan.md → Açık konular, 1. madde):

| Form | Akış adımı | Bu taslakta |
| --- | --- | --- |
| Süreç İyileştirme Kayıt Listesi (040) | 1 (kayıt), 2 (önceliklendirme), 4 ve 7 (ret / sonuç) | Akıştaki kutularda yazan alanlar |
| Süreç İyileştirme Çalışma Dokümanı (041) | 3, 4 | Mevcut durum, kök neden, çözüm, pilot planı, fayda / maliyet |
| Pilot Değerlendirme Raporu (042) | 7 | Hedef, gerçekleşen, geri bildirim, sonuç |

**Formlar gelince:** alanlar formlara göre bölümlenmiştir (aşağıdaki dört bölüm). Formda olup burada olmayan bir alan,
`ProcessImprovement` tablosuna ilgili bölümün altına yeni bir kolon olarak eklenir (tek migration, mevcut kayıtlarda boş);
uç noktalar ve ekran bölüm bölüm olduğu için yalnızca o bölüm değişir. Formda tablo hâlinde tekrar eden satırlar varsa
(ör. birden çok ölçüt) o bölüm için ayrı bir satır tablosu eklenir. Seçenek listeleri (kaynak, sonuç) enum'dur; formdaki
seçenekler farklıysa enum değerleri değişir.

## Kayıt modeli

**Öneri bir `Project` kaydıdır** (ekip kararı, process-api.md): `process-improvement` akışını izler, Talepler / Projeler'de
değil Süreç İyileştirme menüsünde listelenir. Böylece süreç adımları (Süreç sekmesi), bildirimler, Kayıt Geçmişi,
açık konular ve toplantı tutanakları değişmeden çalışır.

| `Project` alanı | Öneride |
| --- | --- |
| `Code` | `SI-001`, `SI-002`… (formdaki "No"); bütün öneriler içinde artan, değişmez |
| `Name` | Önerinin başlığı |
| `Description` | Sorunun kısa tanımı |
| `BusinessAnalystId` | Öneriyi yürüten kişi (akışta 1, 3, 5–8. adımlar İş Analistinde) |
| `Status`, `CurrentStepId`, `StartDate` | Akış; ret 2. ve 4. adımda kaydı `Rejected` yapar |
| `CustomerName`, `RequesterName`, `Source`… | Kullanılmaz (boş metin / varsayılan) |

Öneriye özgü alanlar 1'e 1 bağlı **`ProcessImprovement`** tablosundadır (`ProjectId` hem birincil hem yabancı anahtar).

## Alanlar

### 1. Öneri (Kayıt Listesi · adım 1)

| Alan | Kural |
| --- | --- |
| `title` | Zorunlu, en fazla 200 karakter (`Project.Name`) |
| `problem` | Zorunlu, en fazla 2000 (`Project.Description`): sorunun kısa tanımı |
| `source` | Zorunlu: `Retrospective` (Proje kapanışı / retrospektif), `DefectOrComplaint` (Hata, sapma, müşteri şikâyeti), `EmployeeSuggestion` (Çalışan önerisi), `ManagementGoal` (Yönetim hedefi) |
| `affectedProcess` | Zorunlu, en fazla 200: etkilediği süreç ("Talep kaydı", "Test ve UAT") |
| `proposedSolution` | İsteğe bağlı, en fazla 2000: varsa çözüm fikri |
| `suggestedBy` | Zorunlu, en fazla 200: öneriyi getiren kişi (her çalışan öneri getirebilir; sistem kullanıcısı olması gerekmez) |
| `ownerUserId` | Zorunlu, aktif kullanıcı: öneriyi yürüten (`Project.BusinessAnalystId`) |

### 2. Önceliklendirme (adım 2)

Yazılım Departman Yöneticisi öneriyi üç ölçütle puanlar (her biri 1–5):

| Alan | Anlamı |
| --- | --- |
| `impact` | Etki: iyileşince ne kadar fayda sağlar |
| `frequency` | Uygulama sıklığı: süreç ne sıklıkla işler |
| `ease` | Uygulama kolaylığı: ne kadar kolay hayata geçer |

- **Öncelik puanı** saklanmaz, hesaplanır: `impact + frequency + ease` (3–15). **Öncelik**: 12–15 `High` (Yüksek),
  8–11 `Medium` (Orta), 3–7 `Low` (Düşük). Kayıt Listesi'nde formül farklıysa yalnızca kural sınıfı değişir.
- Puanlar boş bırakılabilir (öneri henüz değerlendirilmedi); üçü birlikte girilir.
- Çalışmaya alma / reddetme kararı puanla değil, Süreç sekmesinde 2. adımın Evet / Hayır cevabıyla verilir.

### 3. Çalışma dokümanı (Çalışma Dokümanı · adım 3–4)

| Alan | Kural |
| --- | --- |
| `currentState` | Mevcut durum; en fazla 4000 |
| `rootCause` | Kök neden analizi; en fazla 4000 |
| `solution` | Önerilen çözüm; en fazla 4000 |
| `pilotPlan` | Pilot planı: nerede, hangi ekipte, ne kadar süre; en fazla 4000 |
| `pilotTarget` | Pilotun hedefi (ölçülebilir: "Talep kayıt süresi 2 günden 1 güne inecek"); en fazla 1000 |
| `costBenefit` | Fayda / maliyet ve etki analizi (adım 4); en fazla 4000 |

Hepsi isteğe bağlıdır; boş alan "henüz yazılmadı" demektir. Süreç adımlarını tamamlamak için zorunlu tutulmazlar
(akış adımlarında doküman zorunluluğu D4'le gelecek).

### 4. Pilot değerlendirme (Pilot Değerlendirme Raporu · adım 7)

| Alan | Kural |
| --- | --- |
| `pilotActual` | Gerçekleşen ölçüm; en fazla 1000 (hedef `pilotTarget`'ta) |
| `pilotFeedback` | Pilot ekibinin geri bildirimi; en fazla 4000 |
| `pilotResult` | `Met` (Karşılandı), `PartiallyMet` (Kısmen karşılandı), `NotMet` (Karşılanmadı); isteğe bağlı |
| `pilotDecision` | Karar ve gerekçe; en fazla 2000 |

`pilotResult` bilgi içindir; akıştaki dallanmayı (standartlaştır / pilotu tekrarla / 3. adıma dön) Süreç sekmesindeki
7. adımın cevabı belirler. Pilot tekrarlanırsa alanlar son pilotun bilgisiyle güncellenir; önceki değerler Kayıt Geçmişi'nde kalır.

## Uç noktalar

| İstek | Açıklama | Başarılı cevap |
| --- | --- | --- |
| `GET /process-improvements?status=&stageOrder=&page=&pageSize=` | Öneriler (sayfalı, en yeni önce). `status`: `Active`, `Closed`, `Rejected` | `200` `PagedResult<ImprovementListItemDto>` |
| `GET /process-improvements/{id}` | Öneri ve dört bölümü | `200` `ImprovementDto` |
| `POST /process-improvements` | Yeni öneri; akışın 1. adımında başlar | `201` `ImprovementDto` |
| `PUT /process-improvements/{id}` | 1. bölüm (öneri bilgileri) | `200` `ImprovementDto` |
| `PUT /process-improvements/{id}/prioritization` | 2. bölüm | `200` `ImprovementDto` |
| `PUT /process-improvements/{id}/study` | 3. bölüm | `200` `ImprovementDto` |
| `PUT /process-improvements/{id}/pilot-evaluation` | 4. bölüm | `200` `ImprovementDto` |

- `{id}` kaydın `Project.Id`'sidir. Akış adımları mevcut süreç uç noktalarıyla ilerler: `/projects/{id}/process/...`.
  Açık konular, tutanaklar ve Kayıt Geçmişi de mevcut `/projects/{id}/...` uç noktalarını kullanır.
- Başka akıştaki bir kaydın `Id`'si istenirse kayıt yokmuş gibi 404 döner ("Süreç iyileştirme önerisi bulunamadı.").
- **Okuma:** giriş yapmış her kullanıcı. **Yazma:** giriş yapmış her aktif kullanıcı, **yalnızca aktif kayıtta** (değilse 409).
  Önceliklendirmeyi akış Yazılım Departman Yöneticisine verir; MVP'de bölüm herkes tarafından yazılabilir, karar adımı rolle sınırlıdır.
- Her bölüm kendi PUT'uyla kaydedilir: bir bölüm kaydedilirken diğerlerinin değerleri değişmez.

## DTO'lar

```jsonc
// ImprovementListItemDto
{
  "id": 41, "code": "SI-003", "title": "Talep kaydında mükerrer alanlar",
  "source": "EmployeeSuggestion", "affectedProcess": "Talep kaydı",
  "priority": "High", "priorityScore": 13,          // puan girilmediyse null
  "status": "Active",
  "currentStep": { "code": "3", "stageShortTitle": "Planla" },   // kapalıysa null
  "owner": { "id": 1, "fullName": "Elif Yılmaz" },
  "createdAt": "2026-11-12T09:00:00Z"
}

// ImprovementDto
{
  "id": 41, "code": "SI-003", "status": "Active",
  "currentStep": { "id": 103, "code": "3", "stageShortTitle": "Planla" },
  "title": "...", "problem": "...", "source": "EmployeeSuggestion", "affectedProcess": "Talep kaydı",
  "proposedSolution": null, "suggestedBy": "Can Öztürk", "owner": { "id": 1, "fullName": "Elif Yılmaz" },
  "prioritization": { "impact": 5, "frequency": 4, "ease": 4, "score": 13, "priority": "High" },  // puan yoksa üçü ve sonuç null
  "study": { "currentState": null, "rootCause": null, "solution": null, "pilotPlan": null, "pilotTarget": null, "costBenefit": null },
  "pilotEvaluation": { "pilotActual": null, "pilotFeedback": null, "pilotResult": null, "pilotDecision": null },
  "createdBy": { "id": 1, "fullName": "Elif Yılmaz" }, "createdAt": "...", "updatedAt": null
}

// CreateImprovementDto (POST) ve UpdateImprovementDto (PUT /{id})
{ "title": "...", "problem": "...", "source": "EmployeeSuggestion", "affectedProcess": "Talep kaydı",
  "proposedSolution": null, "suggestedBy": "Can Öztürk", "ownerUserId": 1 }

// SavePrioritizationDto — üçü birlikte ya da hiçbiri (boşaltmak için üçü null)
{ "impact": 5, "frequency": 4, "ease": 4 }

// SaveStudyDto, SavePilotEvaluationDto — yukarıdaki bölüm alanları
```

| Durum | Kod | Örnek `detail` / `errors` |
| --- | --- | --- |
| Öneri bulunamadı (ya da başka akışın kaydı) | `404` | "Süreç iyileştirme önerisi bulunamadı." |
| Alan kuralları | `400` | `errors.title`: "Başlık zorunludur." |
| Puanların bir kısmı boş | `400` | "Etki, uygulama sıklığı ve kolaylık birlikte girilmelidir." |
| Puan aralık dışı | `400` | `errors.impact`: "Puan 1 ile 5 arasında olmalıdır." |
| Yürüten kişi aktif değil | `400` | "Seçilen kişi aktif bir kullanıcı değil." |
| Kayıt aktif değil | `409` | "Öneri aktif değil; değiştirilemez." |
| Aynı anda aynı kod | `409` | sunucu yeni numarayla 3 kez dener (Proje No gibi), olmazsa "Kayıt numarası çakıştı. Lütfen tekrar deneyin." |

## Ekranlar (frontend)

| Ekran | Yer |
| --- | --- |
| Menü → Süreç İyileştirme: liste (kod, başlık, kaynak, öncelik, adım, durum), filtreler, "+ Yeni öneri" | `features/process-improvement` |
| Öneri detayı: Öneri · Önceliklendirme · Çalışma · Pilot · Süreç · Açık Konular · Tutanaklar · Kayıt Geçmişi sekmeleri | Süreç, Açık Konular, Tutanaklar ve Kayıt Geçmişi mevcut bileşenlerdir (`projectId` alır) |

## Kod yerleşimi

| Katman | Dosyalar |
| --- | --- |
| `Isdlc.Domain/ProcessImprovement/` | `ProcessImprovement` (1'e 1 `Project`), `ImprovementSource`, `PilotResult`, `ImprovementPriority` |
| `Isdlc.Application/ProcessImprovement/` | `IProcessImprovementService`, DTO'lar, `ImprovementRules` (öncelik hesabı; saf) |
| `Isdlc.Infrastructure/ProcessImprovement/` | `ProcessImprovementConfiguration` |
| `Isdlc.Api/Controllers/` | `ProcessImprovementsController` |

## Uygulama sırası (her biri ayrı PR)

1. Bu sözleşme (ekip onayı) · 2. entity, migration, servis ve testler · 3. menü ve ekranlar · 4. formlar (040–042) gelince eksik alanlar

## Ekip onayı bekleyen kararlar

1. Öneri `Project` kaydıdır, öneriye özgü alanlar 1'e 1 bağlı `ProcessImprovement` tablosundadır.
2. Kod `SI-001` biçiminde, bütün öneriler içinde artar.
3. Öncelik = etki + sıklık + kolaylık (her biri 1–5), 12–15 Yüksek, 8–11 Orta, 3–7 Düşük. Kayıt Listesi (040) gelince teyit edilir.
4. Bölümler serbest metindir ve süreç adımlarını engellemez.
5. Pilot tekrarlanırsa son pilotun değerleri tutulur, öncekiler Kayıt Geçmişi'nde kalır (ayrı pilot satırları tutulmaz).
