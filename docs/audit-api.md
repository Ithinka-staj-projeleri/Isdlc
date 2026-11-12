# Audit Modülü — API Sözleşmesi

Projenin değişiklik geçmişi: proje detayındaki **Kayıt Geçmişi** sekmesi. Veri modeli: [data-model.md](data-model.md) →
`AuditLog`. Durum: **uygulandı.** Sorumlu: Erva.

Kayıtlar `AuditLogInterceptor` tarafından, kayıt kaydedilirken otomatik yazılır; bu modül yalnızca okur.
Değiştirilmez, silinmez, elle eklenmez.

## Uç nokta

| İstek | Açıklama | Başarılı cevap |
| --- | --- | --- |
| `GET /projects/{projectId}/audit-logs` | Projenin ve kayıtlarının değişiklikleri, en yeni önce (sayfalı) | `200` `PagedResult<AuditLogDto>` |

- **Okuma:** giriş yapmış her kullanıcı. Proje yoksa `404`.
- **Filtre** (isteğe bağlı): `entityType` = `Project`, `OpenIssue`, `Meeting`, `Requirement`, `ProjectDocument`, `Risk`, `ProjectAssessment`.
- **Sayfalama:** `page`, `pageSize` (en fazla 100). Sıralama: `performedAt` azalan, eşitlikte `id` azalan.

## DTO

```jsonc
// AuditLogDto
{
  "id": 412,
  "entityType": "OpenIssue",          // C# entity adı; ekran Türkçesini gösterir
  "entityId": 12,
  "recordCode": "AK-003",              // proje kodu, AK-003, TT-002, gereksinim kodu, RSK-01 ya da doküman adı;
                                       // kayıt bulunamazsa ve Seviye & Kabul için (projede tek, kodu yok) null
  "action": "Updated",                 // Created | Updated | Deleted
  "changes": [
    { "field": "Status", "oldValue": "Open", "newValue": "Closed" },
    { "field": "OwnerUserId", "oldValue": "Ayşe Demir", "newValue": "Can Öztürk" },
    { "field": "ClosedAt", "oldValue": null, "newValue": "2026-11-05T12:00:00Z" }
  ],
  "performedBy": { "id": 1, "fullName": "Elif Yılmaz" },
  "performedAt": "2026-11-05T12:00:00Z"
}
```

| Alan | Kural |
| --- | --- |
| `changes[].field` | C# alan adı (`Status`, `DueDate`). Türkçe karşılığı ekranda: `features/audit/audit-labels.ts` |
| `changes[].oldValue` / `newValue` | Metin. Enum değeri adıyla (`"Closed"`), tarih `"2026-11-12"`, zaman ISO 8601, `"true"`/`"false"`. `null`: boş ya da (yeni kayıtta) eski değer yok |
| Okunur değerler | Başka kayda işaret eden alanlar backend'de çevrilir: `OwnerUserId`, `BusinessAnalystId`, `ProjectManagerId`, `RecordedById` → kişi adı; tutanağın `Participants` listesi "Ad (Rol), Ad" olarak gösterilir; `CurrentStepId` → adım kodu (`1.2`); `SourceMeetingId` → tutanak kodu; `DocumentTypeId` → doküman adı. Kayıt bulunamazsa sayı olarak kalır |
| Yer almayan | `ProjectId` (her satırda aynı), ve AuditLog'a hiç yazılmayan alanlar (`Id`, `RowVersion`, `PasswordHash`, `CreatedAt/By`, `UpdatedAt/By`) |

Süreç adımları (`StepRecord`) loglanmaz; ama adım değişince proje güncellendiği için geçmişte
"Mevcut adım: 1.2 → 1.3" olarak görünür. Notlu süreç geçmişi Süreç sekmesindedir.

## Kod yerleşimi

| Katman | Dosyalar |
| --- | --- |
| `Isdlc.Application/Audit/` | `IAuditService`, `AuditService` (sorgu, kod ve ad çözümleme), `AuditChangeReader` (JSON'u alanlara çeviren saf sınıf), DTO'lar |
| `Isdlc.Api/Controllers/` | `AuditLogsController` (yol tam yazılır: `/api/v1/projects/{projectId:int}/audit-logs`) |
| `frontend/.../features/audit/` | `app-audit-log` (`projectId` alır), `audit-labels.ts` (alan ve değer adları), `audit.service.ts` |

Yeni bir proje kaydı `IAudited` + `IProjectRecord` taşıdığında geçmişe kendiliğinden düşer (`Risk` ve
`ProjectAssessment` böyle eklendi).
Ekranda Türkçe görünmesi için `audit-labels.ts`'e alan adları ve enum etiketleri, kodunun görünmesi için
`AuditService.LoadRecordCodesAsync`'e bir satır eklenir.
