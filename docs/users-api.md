# Kişiler (Identity) — Kişi profili ve sorumlu önerisi

Faz 5 · F9 ([phase-5-plan.md](phase-5-plan.md)). Kullanıcıya uzmanlık alanları ve "benimle nasıl çalışılır" notu eklenir;
her kişinin bir profil sayfası olur; açık konu formunda sorumlu seçilirken kişilerin iş yükü ve uzmanlığı görünür.
Durum: **taslak** (23 Kasım 2026). Sorumlu: Baran.

İş yükü sayıları yeniden hesaplanmaz; panelin "Ekip iş yükü" hesabından gelir (`ITeamWorkloadService`,
[dashboard-api.md](dashboard-api.md) → "Faz 5: bu hafta ve ekip iş yükü", F2-1…F2-6).

Uç noktalar AGENTS.md'deki ortak kurallara uyar: `/api/v1/` öneki, enum'lar metin, `DateOnly` alanları `2026-11-23`
biçiminde, hatalar `ProblemDetails`, JSON alanları camelCase.

## Kararlar

| # | Konu | Karar |
| --- | --- | --- |
| P1 | Yeni alanlar | `User.Expertise` (en fazla 300 karakter, isteğe bağlı): uzmanlık alanları, virgülle ayrılmış düz metin ("Ödeme sistemleri, SQL, Raporlama"); ekranda her alan ayrı etiket olarak gösterilir. `User.WorkingNote` (en fazla 1000 karakter, isteğe bağlı): "Benimle nasıl çalışılır" notu. Boş metin `null` kaydedilir |
| P2 | Kim düzenler? | Herkes **yalnızca kendi** profilini düzenler (`PUT /users/me/profile`). Rol, ad ve e-posta bu formdan değişmez |
| P3 | Kim görür? | Giriş yapan herkes herkesin profilini görür (şirket içi uygulama). Pasif kullanıcının profili de açılır, "Pasif" olarak gösterilir |
| P4 | Profildeki sayılar | Aktif kullanıcıda `workload` iş yükü servisindeki satırın aynısıdır; panelde görünen sayıyla birebir aynıdır. Pasif kullanıcı iş yüküne girmez, `workload` `null` döner |
| P5 | Profildeki listeler | **Açık konular:** kişiye (sistem kullanıcısı olarak) atanmış, kapanmamış, aktif projedeki açık konular; sıralama paneldeki gibi (gecikenler önce, sonra termine göre, termini olmayanlar sonda). **Bekleyen adımlar:** aktif kayıtlarda kişinin yapabileceği mevcut adımlar (F2-3 kuralı, `TeamWorkloadRules.IsWaitingFor`); en uzun bekleyen önce. Her liste en fazla 10 satır ve toplam sayı döner; toplamlar `workload` sayılarıyla aynıdır |
| P6 | Pasif kullanıcının listeleri | Açık konuları yine listelenir (başkasına aktarılması gerekir). Pasif kullanıcıya adım beklemez: bekleyen adımlar boş döner |
| P7 | Sorumlu önerisi | Açık konu formu `GET /users/workload` listesini kullanır: her aktif kullanıcının rolü, uzmanlığı ve iş yükü. Seçenekte "Ad · 3 açık konu (1 gecikmiş)" yazar; seçilen kişinin uzmanlığı ve bekleyen adım sayısı seçimin altında görünür, profili yeni sekmede açılır. Liste ada göre (Türkçe sıra) sıralanır; sistem kimseyi kendiliğinden seçmez |
| P8 | Bağlantılar | Üst bardaki ad kişinin kendi profiline, panelin "Ekip iş yükü" satırları ilgili profile gider |

## Uç noktalar

| İstek | Açıklama | Başarılı cevap | Hata |
| --- | --- | --- | --- |
| `GET /users/{id}/profile` | Kişinin profili | `200` `UserProfileDto` | `404` kullanıcı yok |
| `PUT /users/me/profile` | Giriş yapanın uzmanlık ve notunu kaydeder | `200` `UserProfileDto` | `400` doğrulama |
| `GET /users/workload` | Aktif kullanıcılar, uzmanlık ve iş yüküyle, ada göre | `200` `UserWorkloadDto[]` | |

### `UserProfileDto`

```json
{
  "id": 3,
  "fullName": "Elif Yılmaz",
  "email": "elif.yilmaz@isdlc.local",
  "role": "BusinessAnalyst",
  "isActive": true,
  "expertise": "Ödeme sistemleri, SQL",
  "workingNote": "Sabahları toplantıya girmiyorum; acil işler için telefon.",
  "workload": { "openIssues": 3, "overdueOpenIssues": 1, "waitingSteps": 2 },
  "openIssues": { "totalCount": 3, "items": [ /* DashboardOpenIssueDto */ ] },
  "waitingSteps": { "totalCount": 2, "items": [ /* WaitingStepDto */ ] }
}
```

Liste satırları panelin satırlarıyla aynı biçimdedir (`DashboardOpenIssueDto`, `WaitingStepDto`; bkz. dashboard-api.md).

### `UpdateMyProfileDto`

| Alan | Kural | Hata mesajı |
| --- | --- | --- |
| `expertise` | isteğe bağlı, en fazla 300 karakter | "En fazla 300 karakter olabilir." |
| `workingNote` | isteğe bağlı, en fazla 1000 karakter | "En fazla 1000 karakter olabilir." |

### `UserWorkloadDto`

```json
{ "id": 3, "fullName": "Elif Yılmaz", "role": "BusinessAnalyst", "expertise": "Ödeme sistemleri, SQL",
  "openIssues": 3, "overdueOpenIssues": 1, "waitingSteps": 2 }
```

## Ekranlar

- **Kişi profili** (`/users/:userId`): ad, rol, e-posta, uzmanlık etiketleri, "Benimle nasıl çalışılır" notu, iş yükü sayıları,
  açık konular ve bekleyen adımlar (satır ilgili projenin sekmesine gider). Kendi profilinde "Profili düzenle" düğmesi.
- **Açık konu formu:** "Ekip üyesi" seçiminde her seçenekte açık konu sayısı; seçilen kişinin uzmanlığı ve bekleyen adım sayısı
  seçimin altında, "Profili gör" bağlantısıyla.
