# Genel Arama — API Sözleşmesi

Faz 5 · F7 ([phase-5-plan.md](phase-5-plan.md)). Üst bardaki arama kutusu (Ctrl+K) projeleri, süreç iyileştirme önerilerini ve
proje içi kayıtları (açık konular, tutanaklar, riskler, gereksinimler) tek yerden bulur; sonuca tıklayınca kayıt açılır.
Durum: sözleşme **taslak** (24 Kasım 2026); servis, API ve testleri uygulandı (2. adım). Sorumlu: Baran.

Yeni tablo ya da kolon yoktur; arama mevcut tablolarda yapılır.

Uç nokta AGENTS.md'deki ortak kurallara uyar: `/api/v1/` öneki, enum'lar metin, `DateOnly` alanları `2026-11-24`
biçiminde, hatalar `ProblemDetails`, JSON alanları camelCase.

## Kararlar

| # | Konu | Karar |
| --- | --- | --- |
| S1 | Ne aranır? | **Projeler** (talepler ve ek geliştirmeler dahil; `ProcessFlowKind.Request`): Proje No, ad, talep sahibi (`CustomerName`). **Süreç iyileştirme önerileri** (`ProcessFlowKind.Improvement`): kod (`SI-001`), başlık (`Name`). **Açık konular, riskler, gereksinimler:** kod ve başlık (`Title`). **Tutanaklar:** kod ve gündem (`Agenda`; tutanağın başlığı yoktur). Açıklama, not gibi uzun metinler aranmaz |
| S2 | Eşleşme | Arama metni kırpılır; alanın **içinde geçmesi** yeterlidir (`Contains`). Büyük / küçük harf fark etmez (veritabanı collation'ı). **Proje No** bir istisnadır: proje listesindeki aramayla aynı kural uygulanır; tam eşleşme gerekir ("14", 114'ü bulmaz) ve projenin ek geliştirmeleri de bulunur ("14" → "14 / EG-01") |
| S3 | En az / en çok | `q` en az **2**, en fazla **100** karakterdir (kırpıldıktan sonra). Kısa ya da uzun metin `400` döner. Ekran 2 karakterden önce istek göndermez |
| S4 | Sonuç sayısı | Her türden en fazla **5** sonuç döner; her grubun yanında toplam eşleşme sayısı (`totalCount`) gelir. Ekran "5 / 12" gibi gösterir; "tümünü gör" sayfası MVP'de yoktur |
| S5 | Sıralama | Her grupta önce açık olanlar (proje / öneri `Active` ya da `Postponed`; açık konu ve risk `Closed` değil; gereksinim `Cancelled` değil), sonra en yeni kayıt (`Id` büyük olan) önce. Tutanak kapanmaz: en yeni toplantı tarihi önce |
| S6 | Kapalı kayıtlar | Aranır ve gösterilir (eski bir projeyi bulmak da aramanın işi). Satırda durumu yazar |
| S7 | Kim ne görür? | Giriş yapan herkes her şeyi bulur; proje ve kayıt listelerindeki görünürlükle aynıdır (şirket içi uygulama, rol bazlı kısıt yok) |
| S8 | Proje içi kayıtların kodu | `RSK-001` gibi kodlar projeye göre tekrar eder; bu yüzden her satırda projenin No'su ve adı da gelir |
| S9 | Sonucun bağlantısı | Ekran kurar, API göndermez. Proje → `/projects/{id}`, öneri → `/process-improvements/{id}`, kayıt → `/projects/{projectId}?tab=<sekme>`. Kaydın kendisini açmak (`&id=12`) F8'in işidir; F8 gelince yalnızca bağlantıya `id` eklenir |
| S10 | Yeni modül | Arama birden fazla modülün tablolarını okur; kod `Isdlc.Application/Search/` (modül adı `Search`), controller `SearchController` → `/api/v1/search`. AGENTS.md Bölüm 3 ve 6'ya eklenir |

## Uç nokta

| İstek | Açıklama | Başarılı cevap | Hata |
| --- | --- | --- | --- |
| `GET /search?q=portal` | Bütün türlerde arama | `200` `SearchResultDto` | `400` `q` boş, 2 karakterden kısa ya da 100'den uzun |

Doğrulama hatası `errors.q` altında döner: "En az 2 karakter girin." / "En fazla 100 karakter olabilir."
Kenarlarındaki boşluklar atılınca kısa kalan metin (`"  a  "`) servisin `BusinessRuleException`'ıyla aynı mesajı `detail` içinde döner.

### `SearchResultDto`

Boş grup da gönderilir (`items: []`, `totalCount: 0`); ekran boş grupları göstermez.

```json
{
  "projects": {
    "totalCount": 2,
    "items": [
      { "id": 14, "code": "14", "name": "Bayi Portalı", "customerName": "Demo Lojistik A.Ş.", "status": "Active" },
      { "id": 21, "code": "14 / EG-01", "name": "Bayi Portalı — raporlama modülü", "customerName": "Demo Lojistik A.Ş.", "status": "Active" }
    ]
  },
  "improvements": {
    "totalCount": 1,
    "items": [ { "id": 30, "code": "SI-001", "name": "Toplantı notlarının tek yerde tutulması", "status": "Active" } ]
  },
  "openIssues": {
    "totalCount": 1,
    "items": [ { "id": 7, "code": "AK-003", "title": "Portal giriş ekranı onayı", "status": "Open",
                 "projectId": 14, "projectCode": "14", "projectName": "Bayi Portalı" } ]
  },
  "meetings": {
    "totalCount": 1,
    "items": [ { "id": 4, "code": "TT-002", "title": "Portal kapsam görüşmesi", "date": "2026-11-12",
                 "projectId": 14, "projectCode": "14", "projectName": "Bayi Portalı" } ]
  },
  "risks": { "totalCount": 0, "items": [] },
  "requirements": { "totalCount": 0, "items": [] }
}
```

| Tür | Satır DTO'su | Alanlar |
| --- | --- | --- |
| Proje | `SearchProjectDto` | `id`, `code`, `name`, `customerName`, `status` (`ProjectStatus`) |
| Öneri | `SearchImprovementDto` | `id` (projenin id'si; detay sayfası bununla açılır), `code`, `name`, `status` (`ProjectStatus`) |
| Açık konu | `SearchRecordDto` | `id`, `code`, `title`, `status` (`OpenIssueStatus` metni), `projectId`, `projectCode`, `projectName` |
| Risk | `SearchRecordDto` | aynı; `status` `RiskStatus` metni |
| Gereksinim | `SearchRecordDto` | aynı; `status` `RequirementStatus` metni |
| Tutanak | `SearchMeetingDto` | `id`, `code`, `title` (gündem, ilk 200 karakter), `date`, `projectId`, `projectCode`, `projectName` |

Gruplar `SearchGroupDto<T>` (`items`, `totalCount`) biçimindedir.

## Ekran

- **Üst bar:** "Ara… (Ctrl+K)" kutusu. Ctrl+K (Mac'te Cmd+K) kutuya odaklanır; Esc kapatır.
- Yazarken 300 ms beklenir, sonra istek gider; 2 karakterden önce istek gitmez. Önceki istek bitmeden yenisi gelirse eskisi iptal edilir.
- Sonuçlar kutunun altında açılan bir panelde, gruplar hâlinde gösterilir: Projeler, Süreç iyileştirme, Açık konular, Tutanaklar,
  Riskler, Gereksinimler. Grup başlığında "5 / 12" gibi sayı yazar.
- Satır: kod, başlık; proje içi kayıtta altında "14 · Bayi Portalı". Kapalı kayıt soluk gösterilir ve durumu yazar.
- Ok tuşlarıyla satırlar arasında gezilir, Enter seçili kaydı açar, fareyle tıklamak da açar. Kayıt açılınca panel kapanır.
- Sonuç yoksa: "Sonuç bulunamadı."

Kod: `frontend/src/app/features/search/` (servis ve modeller), arama kutusu `core/layout/topbar` içinde
`app-global-search` bileşeni olarak.

## Testler

- `SearchServiceTests` (veritabanıyla): her tür kendi alanında bulunur; Proje No tam eşleşir ve ek geliştirmeleri bulur;
  büyük / küçük harf fark etmez; en fazla 5 satır ve doğru `totalCount`; açıklar önce; iyileştirme önerisi projeler grubuna karışmaz.
- Testler kendi kayıtlarını benzersiz bir kelimeyle oluşturur; seed verisine bağlı sayı beklemez (AGENTS.md Bölüm 9).
- Frontend: arama modelinin bağlantı kurma ve gruplama fonksiyonları için birim testleri.

## Uygulama sırası (her biri ayrı PR)

1. Bu doküman (ekip onayı)
2. `SearchService`, `GET /search` ve testleri; AGENTS.md Bölüm 3 ve 6
3. Ekran: üst bardaki arama kutusu ve sonuç paneli

## Ekip onayı bekleyen konular

1. **Açıklamalar aranmalı mı?** Öneri: hayır (S1). Uzun metinde `Contains` yavaşlar ve gürültülü sonuç verir; gerekirse sonra eklenir.
2. **Tutanakta gündem başlık sayılır mı?** Öneri: evet (S1); tutanağın ayrı bir başlık alanı yoktur.
3. **Ek geliştirme projeler grubunda mı?** Öneri: evet; kodu ("14 / EG-01") zaten projesiyle birlikte görünür.
