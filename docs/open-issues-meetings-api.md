# OpenIssues ve Meetings Modülleri — API Sözleşmesi

Açık Konular Listesi (akış 6.3) ve toplantı tutanakları. Veri modeli: [data-model.md](data-model.md) →
"Proje kayıtları". Durum: **uygulandı, ekip onayı bekliyor.** Sorumlu: Erva.

Bütün uç noktalar AGENTS.md'deki ortak kurallara uyar: `/api/v1/` öneki, enum'lar metin, tarihler UTC / ISO 8601
(`DateOnly` alanları `2026-11-05` biçiminde), hatalar `ProblemDetails`, listeler `items` + `totalCount`, JSON alanları camelCase.
İki modül de projenin alt kaynağıdır (`/api/v1/projects/{projectId}/...`).

## Ortak kurallar

- **Okuma:** giriş yapmış her kullanıcı.
- **Ekleme / değiştirme:** giriş yapmış her aktif kullanıcı, **yalnızca aktif projede** (değilse 409).
- **Silme yok.** Açık konu kapatılır.
- Başka bir projeye ait bir kaydın `Id`'si istenirse kayıt yokmuş gibi 404 döner.
- **Sorumlu:** `ownerUserId` (ekip üyesi) **ya da** `ownerName` (sistem dışı kişi); tam olarak biri gönderilir.
  Cevaplarda ikisi tek alanda birleşir: `owner: { "userId": 3, "name": "Ayşe Demir" }` veya `{ "userId": null, "name": "Müşteri BT" }`.
- **Eş zamanlı düzenleme:** MVP'de son kaydeden kazanır (`RowVersion` yok). Her değişiklik denetim kaydında görünür.
  `Process`'teki gibi bir koruma gerekirse `RowVersion` sonradan eklenebilir.

## Açık konular

### Uç noktalar

| İstek | Açıklama | Başarılı cevap |
| --- | --- | --- |
| `GET /projects/{projectId}/open-issues` | Projenin açık konuları (filtreli, sayfalı) | `200` `PagedResult<OpenIssueListItemDto>` |
| `GET /projects/{projectId}/open-issues/{openIssueId}` | Tek kayıt | `200` `OpenIssueDto` |
| `POST /projects/{projectId}/open-issues` | Yeni kayıt | `201` `OpenIssueDto` |
| `PUT /projects/{projectId}/open-issues/{openIssueId}` | İçeriği güncelle (tür, konu, açıklama, sorumlu, termin) | `200` `OpenIssueDto` |
| `PUT /projects/{projectId}/open-issues/{openIssueId}/status` | Durumu değiştir (aç, devam ediyor, kapat) | `200` `OpenIssueDto` |

İçerik ile durum ayrı uç noktalarda: listedeki "Kapat" / "Yeniden aç" butonları bütün formu göndermeden çalışır.

**Liste filtreleri** (hepsi isteğe bağlı): `status` (`Open`, `InProgress`, `Closed`), `type` (`OpenIssueType`),
`overdueOnly` (`true` → yalnızca gecikenler), sayfalama `page`, `pageSize`.
**Sıralama:** kapanmamışlar önce; sonra termin (yakın olan önce, termini olmayanlar sonda); sonra `Code`.

### DTO'lar

```jsonc
// OpenIssueListItemDto
{
  "id": 12,
  "code": "AK-003",
  "type": "Dependency",
  "title": "ERP test ortamı erişimi müşteri tarafından sağlanacak",
  "owner": { "userId": null, "name": "Müşteri BT" },
  "dueDate": "2026-11-02",
  "status": "Open",
  "isOverdue": true,
  "sourceMeeting": { "id": 4, "code": "TT-002" }   // tutanaktan aktarılmadıysa null
}

// OpenIssueDto = OpenIssueListItemDto alanları +
{
  "projectId": 1,
  "description": "Test ortamı bilgileri müşterinin BT ekibinden bekleniyor.",
  "closedAt": null,
  "createdBy": { "id": 1, "fullName": "Elif Yılmaz" },
  "createdAt": "2026-10-28T09:12:00Z",
  "updatedAt": null
}

// CreateOpenIssueDto (POST) ve UpdateOpenIssueDto (PUT) — aynı alanlar
{
  "type": "Action",
  "title": "UAT için anahtar kullanıcı listesi",
  "description": null,
  "ownerUserId": null,
  "ownerName": "İK Uzmanı",
  "dueDate": "2026-11-12"
}

// UpdateOpenIssueStatusDto (PUT .../status)
{ "status": "Closed" }
```

| Alan | Kural |
| --- | --- |
| `type` | Zorunlu |
| `title` | Zorunlu, en fazla 300 karakter |
| `description` | İsteğe bağlı, en fazla 2000 karakter |
| `ownerUserId` / `ownerName` | Tam olarak biri; `ownerUserId` aktif bir kullanıcı olmalı; `ownerName` en fazla 200 karakter |
| `dueDate` | İsteğe bağlı |
| `status` | Zorunlu. Yeni kayıt her zaman `Open` başlar |

`isOverdue`: `status != Closed` ve `dueDate` bugünden (UTC) önce.
`closedAt`: durum `Closed` olunca dolar, başka bir duruma geçince boşalır.

## Toplantı tutanakları

### Uç noktalar

| İstek | Açıklama | Başarılı cevap |
| --- | --- | --- |
| `GET /projects/{projectId}/meetings` | Projenin tutanakları (sayfalı, en yeni toplantı önce) | `200` `PagedResult<MeetingListItemDto>` |
| `GET /projects/{projectId}/meetings/{meetingId}` | Tutanak, kararları ve aksiyonlarıyla | `200` `MeetingDto` |
| `POST /projects/{projectId}/meetings` | Yeni tutanak; kararlar ve aksiyonlar açık konulara aktarılır | `201` `MeetingDto` |
| `PUT /projects/{projectId}/meetings/{meetingId}` | Tutanak bilgilerini güncelle (tür, tarih, katılımcılar, gündem) | `200` `MeetingDto` |

### DTO'lar

```jsonc
// MeetingListItemDto
{
  "id": 4,
  "code": "TT-002",
  "type": "AnalysisSession",
  "date": "2026-11-02",
  "participants": "Elif Yılmaz, Can Öztürk, Müşteri BT",
  "decisionCount": 1,
  "actionCount": 2,
  "openActionCount": 1
}

// MeetingDto
{
  "id": 4,
  "projectId": 1,
  "code": "TT-002",
  "type": "AnalysisSession",
  "date": "2026-11-02",
  "participants": "Elif Yılmaz, Can Öztürk, Müşteri BT",
  "agenda": "ERP servisleri ve iş emri yaşam döngüsü",
  "decisions": [ /* OpenIssueListItemDto[] — Type = Decision */ ],
  "actions":   [ /* OpenIssueListItemDto[] — Type = Action, güncel durumlarıyla */ ],
  "createdBy": { "id": 1, "fullName": "Elif Yılmaz" },
  "createdAt": "2026-11-02T15:40:00Z",
  "updatedAt": null
}

// CreateMeetingDto (POST)
{
  "type": "AnalysisSession",
  "date": "2026-11-02",
  "participants": "Elif Yılmaz, Can Öztürk, Müşteri BT",
  "agenda": "ERP servisleri ve iş emri yaşam döngüsü",
  "decisions": [ "İş emri durumları 6 adım olarak kesinleşti." ],
  "actions": [
    { "title": "ERP test ortamı erişimi sağlanacak", "ownerUserId": null, "ownerName": "Müşteri BT", "dueDate": "2026-11-09" },
    { "title": "İş emri ekran taslakları", "ownerUserId": 1, "ownerName": null, "dueDate": null }
  ]
}

// UpdateMeetingDto (PUT) — type, date, participants, agenda (kararlar ve aksiyonlar Açık Konular'dan yönetilir)
```

| Alan | Kural |
| --- | --- |
| `type`, `date` | Zorunlu |
| `participants` | Zorunlu, en fazla 1000 karakter |
| `agenda` | Zorunlu, en fazla 4000 karakter |
| `decisions` | İsteğe bağlı, en fazla 50 satır; her satır en fazla 300 karakter, boş satırlar atlanır |
| `actions` | İsteğe bağlı, en fazla 50 satır; her satır açık konu kurallarına uyar (`title` zorunlu, sorumlu tam olarak biri) |

**Aktarım** (`POST`): tutanak ve açık konular tek `SaveChanges` ile kaydedilir.
- Her aksiyon → `OpenIssue(Type = Action, Status = Open)`, verilen sorumlu ve terminle.
- Her karar → `OpenIssue(Type = Decision, Status = Closed)`, sorumlu kaydeden kullanıcı, termin toplantı tarihi.
- Hepsinin `SourceMeetingId`'si yeni tutanaktır. Kodlar sırayla verilir (`AK-004`, `AK-005`…): önce aksiyonlar, sonra kararlar, girildikleri sırayla.
- Bir satır hatalıysa hiçbir şey kaydedilmez; mesaj satırı söyler: "2. aksiyon: Seçilen sorumlu aktif bir kullanıcı değil.", "3. karar en fazla 300 karakter olabilir.".
  Biçim hataları alan adıyla döner: `errors["actions[0].title"]`.
- Tutanak detayında ve listedeki sayılarda türü `Decision` olan kayıtlar karar, diğerleri aksiyon sayılır;
  `openActionCount` kapanmamış aksiyonlardır.

### Faz 4 · E4: toplantı ve ziyaret tutanakları

Durum: **taslak, ekip onayı bekliyor.** Sorumlu: Erva. Plan: [phase-4-plan.md](phase-4-plan.md) → E4.
Kaynak: Toplantı Tutanağı (004) ve Ziyaret Tutanağı (005). Taslak, bu formların önceki sürümleri
(ITH.FRM.08 Toplantı / Ziyaret Tutanağı) ve plandaki alan listesiyle yazıldı; 004 ve 005 ile karşılaştırılıp kesinleşir.

**Ziyaret tutanağı ayrı bir kayıt değildir:** türü `CustomerVisit` (Müşteri Ziyareti) olan tutanaktır. Kod (`TT-001`), liste,
kararlar ve aksiyonların açık konulara aktarılması aynen kalır. Ziyarete özgü alanlar yalnızca bu türde girilir.

**Yeni alanlar**

| Alan | Formda | Kural |
| --- | --- | --- |
| `recordedByUserId` | Tutanağı yazan / notları yazan | Zorunlu, aktif bir kullanıcı. Yeni tutanak formunda giriş yapan kişiyle dolu gelir |
| `participants` | Katılanlar ve rolleri | **Metin yerine satırlar:** `[{ "name": "Hakan Yıldız", "role": "Depo Müdürü (Demo Lojistik)" }]`. En az 1, en fazla 50 satır; `name` zorunlu (en fazla 200), `role` isteğe bağlı (en fazla 200) |
| `summary` | Toplantı özeti / notlar | İsteğe bağlı, en fazla 4000 karakter |
| `completedStages` | Tamamlanan aşamalar | İsteğe bağlı, en fazla 2000 karakter. Formda projenin süreç durumundan önerilir (tamamlanan bölümler); kişi değiştirebilir |
| `ongoingStages` | Devam eden aşamalar | İsteğe bağlı, en fazla 2000 karakter. Formda mevcut bölüm ve adımla önerilir |
| `nextSteps` | Sonraki adımlar | İsteğe bağlı, en fazla 2000 karakter |
| `returnDate` | Dönüş tarihi (ziyaret) | Yalnızca ziyarette; `date` (gidiş tarihi) ile aynı ya da sonra. Ziyarette zorunlu |
| `plannedActivities` | Planlanan aktiviteler (ziyaret) | Yalnızca ziyarette, isteğe bağlı, en fazla 4000 karakter |
| `completedActivities` | Yapılan aktiviteler (ziyaret) | Yalnızca ziyarette, isteğe bağlı, en fazla 4000 karakter |

- `date` ziyarette **gidiş tarihi** olarak gösterilir; `agenda` ziyarette "Ana gündem"dir.
- Ziyaret alanları başka türde gönderilirse `400`: "Dönüş tarihi ve aktiviteler yalnızca müşteri ziyaretinde girilir."
  Tür ziyaretten başka bir türe çevrilirken bu alanlar boş gönderilir.
- Formdaki "Firma" projenin müşterisidir (`Project.CustomerName`); ayrıca tutulmaz. Ziyaret formundaki "Açık konular"
  tutanağın aksiyonlarıdır (Açık Konular'a aktarılır), "Notlar" `summary`'dir.
- Listede `participants` yerine `participantNames` döner ("Elif Yılmaz, Can Öztürk, Hakan Yıldız"); ziyarette `returnDate` de döner.

```jsonc
// CreateMeetingDto (POST) — UpdateMeetingDto (PUT) aynı alanları decisions ve actions olmadan alır
{
  "type": "CustomerVisit",
  "date": "2026-11-18",                                   // ziyarette gidiş tarihi
  "returnDate": "2026-11-19",
  "recordedByUserId": 1,
  "participants": [
    { "name": "Elif Yılmaz", "role": "İş Analisti" },
    { "name": "Hakan Yıldız", "role": "Depo Müdürü (Demo Lojistik)" }
  ],
  "agenda": "Depoda mevcut sayım sürecinin yerinde incelenmesi",
  "summary": "Sayım kâğıtla yapılıyor; farklar ay sonunda Excel'de eşleştiriliyor.",
  "completedStages": "Talep ve ön analiz",
  "ongoingStages": "Teklif ve anlaşma — 7. adım: Kapsam ve efor",
  "nextSteps": "Ekran taslakları depo ekibiyle gözden geçirilecek.",
  "plannedActivities": "- Sayım sürecini izleme\n- Depo listesinin alınması",
  "completedActivities": "- Sayım süreci izlendi\n- Depo listesi bir hafta içinde gelecek",
  "decisions": [ "İlk fazda tablet desteği olmayacak." ],
  "actions": [ { "title": "Depo listesi", "ownerUserId": null, "ownerName": "Hakan Yıldız", "dueDate": "2026-11-26" } ]
}

// MeetingDto — yukarıdaki alanlar ve
{ "recordedBy": { "id": 1, "fullName": "Elif Yılmaz" }, "participants": [ { "name": "...", "role": "..." } ] /* … */ }
```

**Mevcut kayıtlar:** Migration, eski `Participants` metnini virgülden bölüp satırlara çevirir (rol boş), `RecordedById`
tutanağı oluşturan kişidir. Diğer yeni alanlar boş kalır.

**Yapay zekâ:** Tutanak taslağı (`minutes`) bu alanlara göre güncellenir: katılımcılar ad ve rol satırları olarak, ayrıca özet
ve sonraki adımlar önerilir. Ayrı bir PR'da yapılır; o zamana kadar ajanın virgüllü katılımcı metni formda satırlara bölünür.

**Uygulama sırası** (her biri ayrı PR): 1) bu sözleşme, 2) entity, migration, servis ve testler, 3) ekran, 4) yapay zekâ taslağının yeni alanları.

**Ekip onayı bekleyen kararlar**
1. Ziyaret ayrı bir kayıt değil, `CustomerVisit` türünde tutanaktır.
2. Katılımcılar serbest metin yerine ad + rol satırlarıdır (`MeetingParticipant`). Sistem kullanıcısına bağlanmaz; Talep Sahibi tarafı da yazılabilsin diye ad yazılır.
3. Tamamlanan / devam eden aşamalar süreçten **önerilen** serbest metindir; toplantı anındaki durum olarak saklanır, süreç ilerleyince değişmez.
4. Toplantı formundaki "Konu" ayrı alan olmaz, `agenda`'dır; "İlgili birimler" katılımcıların rolünde yazılır. 004 farklıysa bu madde değişir.

## Yapay zekâ önerileri (`Agents`)

Yeni tutanak formundaki "Yapay zekâ ile karar ve aksiyonları çıkar" butonu için. Hiçbir şey kaydedilmez; öneriler
formu doldurur, kişi düzenleyip tutanağı yukarıdaki `POST /meetings` ile kaydeder. Tasarım: [ai-vision.md](ai-vision.md).

| İstek | Açıklama | Başarılı cevap |
| --- | --- | --- |
| `GET /agents/status` | Yapay zekâ servisi ayarlı ve çalışıyor mu (butonları göstermek için) | `200` `{ "meetingAssistantEnabled": true, "riskAssistantEnabled": true }` |
| `POST /projects/{projectId}/meetings/suggestions` | Ham notlardan öneriler | `200` `MeetingSuggestionsDto` |

```jsonc
// SuggestMeetingItemsDto (POST)
{ "meetingDate": "2026-11-05", "notes": "Ekran taslaklarını Can Öztürk 12 Kasım'a kadar hazırlayacak.
..." }

// MeetingSuggestionsDto
{
  "summary": "ERP entegrasyonu ve raporlar konuşuldu.",
  "decisions": [ { "title": "Raporlar PDF olarak da alınacak", "evidence": "Raporların PDF olarak da alınmasına karar verildi." } ],
  "actions": [
    { "title": "Ekran taslakları", "ownerUserId": 4, "ownerName": null, "dueDate": "2026-11-12",
      "evidence": "Ekran taslaklarını Can Öztürk 12 Kasım'a kadar hazırlayacak.", "warnings": [] }
  ],
  "others": [ { "type": "Question", "title": "Fotoğraflar kaç gün saklanacak?", "ownerName": "Saha Operasyon Müdürü", "dueDate": null, "evidence": "..." } ],
  "skippedCount": 0,
  "modelName": "gemini-...",
  // Tutanağın üst bilgileri; ajan hazırlayamadıysa null (kararlar ve aksiyonlar yine gelir)
  "minutes": {
    "type": "AnalysisSession",
    "typeReason": "Gereksinimler ayrıntılı konuşulmuş.",
    "participants": "Elif Yılmaz, Can Öztürk, Hakan Yıldız (Demo Lojistik)",
    "agenda": "- Mobil stok sayımı\n- Depo listesi",
    "warnings": []
  }
}
```

| Alan | Kural |
| --- | --- |
| `meetingDate` | Zorunlu; terminler bu tarihe göre çözülür ("gelecek çarşamba") |
| `notes` | Zorunlu, en fazla 20000 karakter. E-posta, telefon, IBAN ve TCKN modele gitmeden gizlenir; **yalnızca sahte veri** |
| `actions[].ownerUserId` | Notlardaki ad aktif bir kullanıcının adıyla aynıysa (Türkçe büyük/küçük harf farkı gözetilmez) dolar; değilse `ownerName` |
| `decisions` | Sorumlu yoktur: karar, tutanağı kaydeden kişi adına kapalı kaydedilir |
| `others` | Soru, bağımlılık, varsayım, değişiklik talebi. Tutanak formunda yerleri yok; Açık Konular'dan elle eklenir |
| `warnings` | Ajanın uyarıları: dayanak notlarda bulunamadı, termin toplantıdan önce, sorumlu yok |
| `minutes` | Toplantı türü, katılımcılar (virgülle) ve gündem (her konu "- " ile bir satır). Tür bilinmiyorsa, alan boşsa ya da formun sınırını (1000 / 4000) aşıyorsa `null`. Form yalnızca kişinin doldurmadığı alanları doldurur; tür, kişi değiştirmediyse değişir. `warnings`: notlarda geçmeyen katılımcı, katılımcı bulunamadı |

Hatalar: proje yok `404`, proje aktif değil `409` ("Proje aktif değil; kayıt eklenemez veya değiştirilemez."),
servis ayarlı değil, çalışmıyor ya da model hata verdi `503` (Türkçe `detail`, ör. "Yapay zekâ servisine şu an ulaşılamıyor.").

Backend, `ai/` servisini `Agents:ServiceUrl` adresinden `X-Agent-Key: Agents:ServiceKey` başlığıyla çağırır
(`Isdlc.Infrastructure/Agents/HttpAgentClient.cs`). Tarayıcı Python servisini hiçbir zaman doğrudan çağırmaz.

## Hata durumları

| Durum | Kod | Örnek `detail` |
| --- | --- | --- |
| Proje, açık konu veya tutanak bulunamadı | `404` | "Açık konu bulunamadı.", "Toplantı tutanağı bulunamadı." |
| Alanlar geçersiz | `400` | "Konu zorunludur." |
| Sorumlu kuralı | `400` | "Sorumlu olarak bir kullanıcı ya da bir ad girilmelidir; ikisi birden girilemez." |
| Sorumlu kullanıcı yok veya pasif | `400` | "Seçilen sorumlu aktif bir kullanıcı değil." |
| Proje aktif değil | `409` | "Proje aktif değil; kayıt eklenemez veya değiştirilemez." |
| Aynı anda aynı kod verildi | `409` | "Kayıt numarası çakıştı. Lütfen tekrar deneyin." |

## Bildirim noktaları

`INotificationService` üzerinden ([process-api.md](process-api.md) → Bildirim noktaları):

| Olay | Kime | Mesaj örneği |
| --- | --- | --- |
| Açık konunun sorumlusu bir kullanıcı oldu (yeni kayıt, sorumlu değişti, tutanaktan aksiyon) | O kullanıcı (işlemi yapan kendisi değilse) | "ISDLC-2026-014 · AK-007 size atandı: ERP test ortamı erişimi" |

## Kod yerleşimi

| Katman | Dosyalar |
| --- | --- |
| `Isdlc.Domain/OpenIssues/`, `Isdlc.Domain/Meetings/` | `OpenIssue`, `Meeting`, enum'lar |
| `Isdlc.Application/OpenIssues/`, `Isdlc.Application/Meetings/` | `IOpenIssueService`, `IMeetingService`, DTO'lar, doğrulama |
| `Isdlc.Infrastructure/OpenIssues/`, `Isdlc.Infrastructure/Meetings/` | EF yapılandırmaları |
| `Isdlc.Api/Controllers/` | `OpenIssuesController`, `MeetingsController` (yollar tam yazılır: `/api/v1/projects/{projectId:int}/...`) |

Kod numarası verme (`AK-001`) ve sorumlu kuralı iki modülde de kullanıldığı için tek yerde yazılır;
tutanak aksiyonları açık konu servisinin doğrulamasını kullanır: `Common/RecordCodes.cs` (sonraki numara),
`OpenIssues/OpenIssueRules.cs` (saf kurallar), `OpenIssues/OpenIssueChecks.cs` (sorumlu doğrulaması, atama bildirimi, liste satırı).

## Ekranlar (frontend)

| Ekran | Yer |
| --- | --- |
| Açık Konular (proje detayı sekmesi): liste, filtreler, ekle/düzenle, kapat/yeniden aç | `features/open-issues` |
| Toplantı Tutanakları (proje detayı sekmesi): liste, tutanak detayı, yeni tutanak formu | `features/meetings` |

İkisi de `Projects` modülünün (Dide) proje detay sayfasına `projectId` alan bileşenler olarak eklenir.
