# Requirements ve Documents Modülleri — API Sözleşmesi

Gereksinim / User Story kayıtları ve proje dokümanlarının durum takibi. Veri modeli: [data-model.md](data-model.md) →
"Proje kayıtları". Durum: **öneri, ekip onayı bekliyor.** Sorumlu: Dide.

Bütün uç noktalar AGENTS.md'deki ortak kurallara uyar: `/api/v1/` öneki, enum'lar metin, tarihler UTC / ISO 8601,
hatalar `ProblemDetails`, listeler `items` + `totalCount`, JSON alanları camelCase, doğrulama hataları `errors.<alanAdı>`
altında Türkçe. İki modül de projenin alt kaynağıdır (`/api/v1/projects/{projectId}/...`).

## Önerilen kararlar

| # | Soru | Öneri |
| --- | --- | --- |
| 1 | Gereksinim ile User Story ayrı kayıtlar mı? | **Tek kayıt (`Requirement`).** User Story bir yazım biçimidir; açıklamaya "… olarak, … istiyorum, böylece …" kalıbıyla yazılır. Form bu kalıbı örnek olarak gösterir |
| 2 | Gereksinim türleri | `Business` (İş gereksinimi), `Functional` (Fonksiyonel), `NonFunctional` (Fonksiyonel olmayan: performans, güvenlik, erişilebilirlik…) |
| 3 | Gereksinim önceliği | MoSCoW: `MustHave` (Olmazsa olmaz), `ShouldHave` (Olmalı), `CouldHave` (Olabilir), `WontHave` (Bu sürümde yok) |
| 4 | Gereksinim durumları | `Draft` (Taslak), `InReview` (İncelemede), `Approved` (Onaylandı), `Cancelled` (İptal edildi). Kayıt silinmez; yanlış girilen ya da vazgeçilen gereksinim iptal edilir. Durumlar arasında serbest geçiş (iptal edilen yeniden `Draft` yapılabilir) |
| 5 | Gereksinim kodu | `GR-001`, proje içinde artan (açık konulardaki `AK-001` ile aynı kural) |
| 6 | Takip edilecek dokümanlar | Süreç akışında adı geçen 18 doküman sabit bir listede (seed) tutulur, aşağıda. Liste kodla değil JSON dosyasıyla değişir |
| 7 | Dokümanlar seviyeye göre değişiyor mu? | **MVP'de hayır.** Her projede bütün dokümanlar listelenir; projede gerekmeyen doküman `NotRequired` (Gerekmiyor) olarak işaretlenir. Böylece modül `Assessment`'ı (Baran) beklemez. Seviye–doküman tablosu şirketten gelirse seed'e `MinLevel` eklenir |
| 8 | Doküman durumları (`DocumentStatus`) | `NotStarted` (Başlanmadı), `InProgress` (Hazırlanıyor), `InReview` (İncelemede), `Approved` (Onaylandı), `NotRequired` (Gerekmiyor) |
| 9 | Dosya yüklenecek mi? | **Hayır.** Yalnızca durum, kısa bir not ve isteğe bağlı bir bağlantı (SharePoint, Teams vb.) tutulur |
| 10 | Projenin doküman kayıtları ne zaman oluşur? | İlk kez durumu değiştirildiğinde. Kaydı olmayan doküman listede `NotStarted` görünür. Projects modülüne ve mevcut projelere dokunmak gerekmez |
| 11 | Menüdeki "Gereksinimler" sayfası | Açık Konular sayfasıyla aynı: üstte proje seçici (`app-project-picker`), altında seçilen projenin gereksinimleri. Proje detayında aynı liste "Gereksinimler" sekmesinde |
| 12 | Doküman Kütüphanesi (PLANNING'de bekleyen karar) | `Documents` içinde; doküman türlerinin salt okunur listesi (ad, aşama, açıklama). MVP'nin son ve isteğe bağlı işi |

## Ortak kurallar

`OpenIssues` ve `Meetings` ile aynı:

- **Okuma:** giriş yapmış her kullanıcı.
- **Ekleme / değiştirme:** giriş yapmış her aktif kullanıcı, **yalnızca aktif projede** (değilse 409).
- **Silme yok.**
- Başka bir projeye ait bir kaydın `Id`'si istenirse kayıt yokmuş gibi 404 döner.
- **Eş zamanlı düzenleme:** MVP'de son kaydeden kazanır (`RowVersion` yok). Her değişiklik denetim kaydında görünür.

## Gereksinimler

### Uç noktalar

| İstek | Açıklama | Başarılı cevap |
| --- | --- | --- |
| `GET /projects/{projectId}/requirements` | Projenin gereksinimleri (filtreli, sayfalı) | `200` `PagedResult<RequirementListItemDto>` |
| `GET /projects/{projectId}/requirements/{requirementId}` | Tek kayıt | `200` `RequirementDto` |
| `POST /projects/{projectId}/requirements` | Yeni kayıt (`Draft` başlar) | `201` `RequirementDto` |
| `PUT /projects/{projectId}/requirements/{requirementId}` | İçeriği güncelle (tür, başlık, açıklama, kabul ölçütleri, öncelik) | `200` `RequirementDto` |
| `PUT /projects/{projectId}/requirements/{requirementId}/status` | Durumu değiştir | `200` `RequirementDto` |

İçerik ile durum ayrı uç noktalarda: listedeki "Onayla" / "İptal et" butonları bütün formu göndermeden çalışır.

**Liste filtreleri** (hepsi isteğe bağlı): `status`, `type`, `priority`, `search` (kod veya başlıkta geçen metin),
sayfalama `page`, `pageSize`.
**Sıralama:** iptal edilmemişler önce, sonra `Code` (`GR-001`, `GR-002`…).

### DTO'lar

```jsonc
// RequirementListItemDto
{
  "id": 7,
  "code": "GR-003",
  "type": "Functional",
  "title": "Depo sorumlusu iş emrini mobil cihazdan kapatabilmeli",
  "priority": "MustHave",
  "status": "Approved"
}

// RequirementDto = RequirementListItemDto alanları +
{
  "projectId": 1,
  "description": "Depo sorumlusu olarak iş emrini sahadan kapatmak istiyorum, böylece kâğıt form doldurmam gerekmez.",
  "acceptanceCriteria": "- Kapatılan iş emri ERP'ye 1 dakika içinde yansır.\n- Fotoğraf eklenmeden kapatılamaz.",
  "createdBy": { "id": 1, "fullName": "Elif Yılmaz" },
  "createdAt": "2026-11-09T09:12:00Z",
  "updatedAt": null
}

// CreateRequirementDto (POST) ve UpdateRequirementDto (PUT) — aynı alanlar
{
  "type": "Functional",
  "title": "Depo sorumlusu iş emrini mobil cihazdan kapatabilmeli",
  "description": "Depo sorumlusu olarak ...",
  "acceptanceCriteria": null,
  "priority": "MustHave"
}

// UpdateRequirementStatusDto (PUT .../status)
{ "status": "Approved" }
```

| Alan | Kural |
| --- | --- |
| `type`, `priority` | Zorunlu |
| `title` | Zorunlu, en fazla 300 karakter |
| `description` | İsteğe bağlı, en fazla 4000 karakter |
| `acceptanceCriteria` | İsteğe bağlı, en fazla 4000 karakter |
| `status` | Zorunlu. Yeni kayıt her zaman `Draft` başlar |

## Dokümanlar

### Uç noktalar

| İstek | Açıklama | Başarılı cevap |
| --- | --- | --- |
| `GET /projects/{projectId}/documents` | Projenin bütün dokümanları, durumlarıyla (aşama sırasıyla) | `200` `ProjectDocumentDto[]` |
| `PUT /projects/{projectId}/documents/{documentTypeId}` | Dokümanın durumunu, notunu ve bağlantısını kaydet (kayıt yoksa oluşturur) | `200` `ProjectDocumentDto` |
| `GET /document-types` | Doküman Kütüphanesi: doküman türlerinin listesi | `200` `DocumentTypeDto[]` |

Doküman listeleri **sayfalanmaz**: sabit ve kısa bir listedir (18 satır), ekranda aşamalara göre gruplanır.

### DTO'lar

```jsonc
// DocumentTypeDto
{
  "id": 2,
  "code": "PKD",
  "name": "Proje Kapsam Dokümanı",
  "stageOrder": 4,
  "stageShortTitle": "Ön Analiz & Plan",
  "description": "Kapsam, kapsam dışı işler, varsayımlar ve Talep Sahibi'nden beklenen girdiler."
}

// ProjectDocumentDto
{
  "documentType": { "id": 2, "code": "PKD", "name": "Proje Kapsam Dokümanı", "stageOrder": 4, "stageShortTitle": "Ön Analiz & Plan" },
  "status": "InReview",
  "note": "Müşteri yorumları bekleniyor.",
  "link": "https://sirket.sharepoint.com/...",
  "updatedBy": { "id": 1, "fullName": "Elif Yılmaz" },   // hiç kaydedilmediyse null
  "updatedAt": "2026-11-09T10:30:00Z"                    // hiç kaydedilmediyse null
}

// UpdateProjectDocumentDto (PUT)
{ "status": "InReview", "note": "Müşteri yorumları bekleniyor.", "link": null }
```

| Alan | Kural |
| --- | --- |
| `status` | Zorunlu |
| `note` | İsteğe bağlı, en fazla 1000 karakter |
| `link` | İsteğe bağlı, en fazla 1000 karakter; doluysa `http://` ya da `https://` ile başlayan bir adres |

### Doküman listesi (seed: `document-types.json`)

Süreç akışında (`process-flow.json`) adı geçen dokümanlar. Başka modüllerin kendi kaydı olanlar listede yok:
Talep Kayıt Formu (`Projects`), Proje Kabul Değerlendirme Formu (`Assessment`), Açık Konular Listesi (`OpenIssues`),
toplantı tutanakları (`Meetings`).

| Kod | Doküman | Aşama | Geçtiği adımlar |
| --- | --- | --- | --- |
| `MIB` | Müşteri İletişim Bilgileri Listesi | 1 | 1.3, 6.2 |
| `TSL` | Talep Analiz Soru Listesi | 2 | 2.1 |
| `PKD` | Proje Kapsam Dokümanı | 4 | 4.1, 4.6 |
| `PGD` | Proje Gereksinim Dokümanı | 4 | 4.1, 4.6, 8.1 |
| `KTR` | Kavramsal Tasarım Raporu | 4 | 4.2, 4.6 |
| `GIK` | Gereksinim İç Onay Kontrol Listesi | 4 | 4.4, 4.5, 7.9, 7.10 |
| `YKL` | Yeni Yazılım Projesi Kontrol Listesi | 6 | 6.2 |
| `PPL` | Proje Planı | 7 | 7.6, 7.11, 7.12 |
| `KTS` | Kabul Testi Senaryoları | 7 | 7.8, 7.12 |
| `TPL` | Test Planı | 7 | 7.8, 7.11, 9.1 |
| `TDK` | Teknik Dokümantasyon | 8 | 8.4, 10.5 |
| `TSR` | Test Sonuç Raporu | 9 | 9.9 |
| `YPL` | Yayın Planı | 10 | 10.1, 11.1 |
| `KKL` | Kullanıcı ve Yönetici Kılavuzları | 10 | 10.4, 12.1 |
| `BPL` | Bakım Planı | 10 | 10.6, 13.2 |
| `EPL` | Eğitim Planı | 10 | 10.7 |
| `KET` | Kullanıcı Eğitim Tutanağı | 10 | 10.7 |
| `PTT` | Proje Teslim Tutanağı | 12 | 12.1, 12.2 |

## Ekranlar

- **Proje detayı → "Gereksinimler" sekmesi:** liste (kod, tür, başlık, öncelik, durum), filtreler, "Yeni gereksinim"
  formu, satırda "Onayla" / "İptal et". Kapanmış projede salt okunur.
- **Menü → "Gereksinimler":** üstte proje seçici, altında aynı liste (Açık Konular sayfası gibi).
- **Proje detayı → "Dokümanlar" sekmesi:** aşamalara göre gruplanmış liste; her satırda durum seçimi, not ve bağlantı.
  Üstte özet: "18 dokümandan 5'i onaylandı, 3'ü gerekmiyor". Kapanmış projede salt okunur.
- **Doküman Kütüphanesi** (isteğe bağlı, son iş): doküman türlerinin aşamaya göre listesi.

## Uygulama sırası (her biri ayrı PR)

1. Bu doküman ve veri modeli (ekip onayı)
2. `Requirement` entity'si, migration, servis ve API
3. Gereksinimler ekranı (proje detayı sekmesi ve menü sayfası)
4. `DocumentType` seed'i, `ProjectDocument` entity'si, migration, servis ve API
5. Dokümanlar sekmesi
6. Çapraz test: `OpenIssues` ve `Meetings` servis testleri (Erva'nın modülleri)
7. Doküman Kütüphanesi (isteğe bağlı)
