# Risks Modülü — API Sözleşmesi

Projenin risk kayıtları (Risk Analiz Formu, akış 4.8): olasılık × etki skoru, önleyici aksiyon ve sorumlu.
Veri modeli: [data-model.md](data-model.md) → "Proje kayıtları" → `Risk`. Durum: **uygulandı** (#91 entity, #92 API, #93 ekran). Sorumlu: Baran.

Bütün uç noktalar AGENTS.md'deki ortak kurallara uyar: `/api/v1/` öneki, enum'lar metin, tarihler UTC / ISO 8601,
hatalar `ProblemDetails`, listeler `items` + `totalCount`, JSON alanları camelCase.
Risk, projenin alt kaynağıdır (`/api/v1/projects/{projectId}/risks`).

## Ortak kurallar

Proje kayıtlarının ortak kuralları ([data-model.md](data-model.md) → "Proje kayıtları") aynen geçerlidir:

- **Okuma:** giriş yapmış her kullanıcı.
- **Ekleme / değiştirme:** giriş yapmış her aktif kullanıcı, **yalnızca aktif projede** (değilse 409).
- **Silme yok.** Gerçekleşmeyecek ya da yanlış girilmiş risk `Closed` yapılır; gerekçe açıklamaya yazılır.
- Başka bir projeye ait bir riskin `Id`'si istenirse kayıt yokmuş gibi 404 döner.
- **Eş zamanlı düzenleme:** MVP'de son kaydeden kazanır (`RowVersion` yok). Her değişiklik denetim kaydında görünür.

## Skor ve seviye

| Kavram | Kural |
| --- | --- |
| `probability`, `impact` | 1–5 arası tam sayı |
| `score` | `probability × impact` (1–25). **Saklanmaz**, her okumada hesaplanır; böylece olasılık veya etki değişince eskimez |
| `severity` | Skordan türetilir: 1–7 `Low` (Düşük), 8–14 `Medium` (Orta), 15–25 `High` (Yüksek). Saklanmaz |

Eşikler prototipteki ısı haritasıyla aynıdır. Hesap tek yerde yazılır (`Risks/RiskRules.cs`) ve veritabanısız test edilir.

## Uç noktalar

| İstek | Açıklama | Başarılı cevap |
| --- | --- | --- |
| `GET /projects/{projectId}/risks` | Projenin riskleri (filtreli, sayfalı) | `200` `PagedResult<RiskListItemDto>` |
| `GET /projects/{projectId}/risks/matrix` | Isı haritası: kapanmamış risklerin olasılık × etki hücrelerine dağılımı | `200` `RiskMatrixDto` |
| `GET /projects/{projectId}/risks/{riskId}` | Tek kayıt | `200` `RiskDto` |
| `POST /projects/{projectId}/risks` | Yeni risk | `201` `RiskDto` |
| `PUT /projects/{projectId}/risks/{riskId}` | Riski güncelle (durum dahil) | `200` `RiskDto` |

Prototipte olduğu gibi durum formun bir alanıdır; ayrı bir durum uç noktası yoktur.

**Liste filtreleri** (hepsi isteğe bağlı): `status` (`RiskStatus`), `category` (`RiskCategory`), `severity` (`RiskSeverity`),
`probability` ve `impact` (ikisi birlikte gönderilir → ısı haritasında tıklanan hücre), sayfalama `page`, `pageSize`.
**Sıralama:** kapanmamışlar önce; sonra skor (yüksek olan önce); sonra `Code`.

## DTO'lar

```jsonc
// RiskListItemDto
{
  "id": 7,
  "code": "RSK-001",
  "title": "ERP servis dokümantasyonu eksik",
  "category": "Technical",
  "probability": 4,
  "impact": 4,
  "score": 16,
  "severity": "High",
  "mitigation": "Servis sahibiyle teknik çalıştay; POC ile doğrulama",
  "owner": { "id": 4, "fullName": "Can Öztürk" },
  "isSharedWithRequester": true,
  "status": "Open"
}

// RiskDto = RiskListItemDto alanları +
{
  "projectId": 1,
  "description": "Servislerin girdi/çıktı tanımları güncel değil.",
  "createdBy": { "id": 1, "fullName": "Elif Yılmaz" },
  "createdAt": "2026-11-09T09:12:00Z",
  "updatedAt": null
}

// CreateRiskDto (POST)
{
  "title": "ERP servis dokümantasyonu eksik",
  "description": null,
  "category": "Technical",
  "probability": 4,
  "impact": 4,
  "mitigation": "Servis sahibiyle teknik çalıştay; POC ile doğrulama",
  "ownerUserId": 4,
  "isSharedWithRequester": true
}

// UpdateRiskDto (PUT) = CreateRiskDto alanları +
{ "status": "Monitoring" }

// RiskMatrixDto — yalnızca kapanmamış riskler sayılır; boş hücreler gönderilmez
{
  "cells": [
    { "probability": 4, "impact": 4, "count": 1 },
    { "probability": 3, "impact": 3, "count": 2 }
  ],
  "countBySeverity": { "low": 0, "medium": 2, "high": 1 }
}
```

| Alan | Kural | Hata mesajı |
| --- | --- | --- |
| `title` | Zorunlu, en fazla 300 karakter | "Risk tanımı zorunludur." |
| `description` | İsteğe bağlı, en fazla 2000 karakter | |
| `category` | Zorunlu | "Kategori seçilmelidir." |
| `probability`, `impact` | Zorunlu, 1–5 | "Olasılık 1 ile 5 arasında olmalıdır.", "Etki 1 ile 5 arasında olmalıdır." |
| `mitigation` | İsteğe bağlı, en fazla 2000 karakter | |
| `ownerUserId` | Zorunlu; aktif bir kullanıcı olmalı | "Sorumlu seçilmelidir.", "Seçilen sorumlu aktif bir kullanıcı değil." |
| `isSharedWithRequester` | İsteğe bağlı, varsayılan `false` | |
| `status` | `PUT`'ta zorunlu. Yeni risk her zaman `Open` başlar | |

## Hata durumları

| Durum | Kod | Örnek `detail` |
| --- | --- | --- |
| Proje veya risk bulunamadı | `404` | "Risk bulunamadı." |
| Alanlar geçersiz | `400` | "Risk tanımı zorunludur." |
| Sorumlu kullanıcı yok veya pasif | `400` | "Seçilen sorumlu aktif bir kullanıcı değil." |
| Isı haritası filtresinde yalnızca biri gönderildi | `400` | "Olasılık ve etki birlikte gönderilmelidir." |
| Proje aktif değil | `409` | "Proje aktif değil; kayıt eklenemez veya değiştirilemez." |
| Aynı anda aynı kod verildi | `409` | "Kayıt numarası çakıştı. Lütfen tekrar deneyin." |

## Bildirim noktaları

`INotificationService` üzerinden ([process-api.md](process-api.md) → Bildirim noktaları):

| Olay | Kime | Mesaj örneği |
| --- | --- | --- |
| Riskin sorumlusu bir kullanıcı oldu (yeni kayıt veya sorumlu değişti) | O kullanıcı (işlemi yapan kendisi değilse) | "ISDLC-2026-014 · RSK-003 size atandı: ERP servis dokümantasyonu eksik" |
| Risk yüksek seviyeye çıktı (yeni kayıtta ya da güncellemede skor 15 ve üstü oldu) | Projenin proje yöneticisi (varsa ve işlemi yapan kendisi değilse) | "ISDLC-2026-014 · RSK-003 yüksek risk (skor 20)" |

## Kod yerleşimi

| Katman | Dosyalar |
| --- | --- |
| `Isdlc.Domain/Risks/` | `Risk`, `RiskCategory`, `RiskStatus`, `RiskSeverity` |
| `Isdlc.Application/Risks/` | `IRiskService`, `RiskService`, DTO'lar, `RiskRules` (skor, seviye; saf kurallar) |
| `Isdlc.Infrastructure/Risks/` | `RiskConfiguration` |
| `Isdlc.Api/Controllers/` | `RisksController` (yollar tam yazılır: `/api/v1/projects/{projectId:int}/risks`) |

Kod numarası `Common/RecordCodes.cs` ile verilir (`OpenIssues` ve `Meetings` ile aynı). Sorumlu doğrulaması
açık konulardaki aktif kullanıcı kontrolünün aynısıdır.

## Ekranlar (frontend)

| Ekran | Yer |
| --- | --- |
| Riskler (proje detayı sekmesi): ısı haritası, liste, filtreler, ekle/düzenle | `features/risks` |

Bileşen, `Projects` modülünün (Dide) proje detay sayfasına `projectId` alan bir sekme olarak eklenir; kapanmış projede salt okunurdur.
Menüdeki genel "Risk Yönetimi" sayfası (bütün projelerin riskleri) MVP'de yoktur; gerekirse `Dashboard` ile birlikte ele alınır.

## Yapay zekâ (sonra)

Risk ajanı (`ai/isdlc_ai/schemas.py` → `RiskSuggestion`) bu sözleşmeyle hizalıdır: `title` 300, `description` ve `mitigation`
2000 karakter, `probability` / `impact` 1–5, `score` hesaplanır. Ajan bağlandığında `category` alanının şemaya eklenmesi gerekir.
Bağlantı MVP kapsamında değildir ([ai-vision.md](ai-vision.md)).

## Ekip onayı bekleyen kararlar

1. **Kod biçimi `RSK-001`:** prototipte `RSK-01`; diğer proje kayıtlarıyla (`AK-001`, `TT-001`) aynı olsun diye 3 hane.
2. **Skor ve seviye saklanmaz,** her okumada hesaplanır.
3. **Sorumlu yalnızca ekip üyesidir** (`ownerUserId` zorunlu). Açık konulardaki gibi sistem dışı sorumlu (`ownerName`) yoktur;
   müşteri tarafındaki bir işi takip etmek gerekirse açık konu açılır.
4. **"Talep Sahibi ile paylaşılır"** (`isSharedWithRequester`) MVP'de yalnızca bir işarettir; listede "Paylaşılır / Dahili" görünür.
   Prototipte paylaşılan risk Açık Konular Listesi'ne aktarılıyordu (akış 6.3). Bu aktarım `OpenIssues` (Erva) ile birlikte
   kararlaştırılacak; MVP'de otomatik aktarım yoktur.
5. **Durumlar:** `Open` (Açık), `Monitoring` (İzleniyor), `Closed` (Kapandı). Kapanmış risk yeniden açılabilir.
