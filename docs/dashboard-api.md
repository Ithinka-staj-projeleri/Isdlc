# Dashboard Modülü — API Sözleşmesi

Gösterge paneli: giriş yapan kullanıcıya bekleyen işlerini ve projelerin genel durumunu gösteren ana sayfa.
Yeni tablo yoktur; mevcut kayıtlardan (`Project`, `StepRecord`, `OpenIssue`) hesaplanır.
Durum: **öneri, ekip onayı bekliyor.** Sorumlu: Dide.

Uç nokta AGENTS.md'deki ortak kurallara uyar: `/api/v1/` öneki, enum'lar metin, tarihler UTC / ISO 8601
(`DateOnly` alanları `2026-11-10` biçiminde), hatalar `ProblemDetails`, JSON alanları camelCase.

## Önerilen kararlar

| # | Soru | Öneri |
| --- | --- | --- |
| 1 | Panel herkese aynı mı? | Genel bölümler herkese aynı; **"Bana bekleyenler"** bölümleri giriş yapan kullanıcıya göre hesaplanır |
| 2 | "Bana bekleyen adım" ne demek? | Projenin mevcut adımını kullanıcı yapabiliyorsa: Process'teki yetki kuralının aynısı (`ApproverRole ?? ResponsibleRole` = kullanıcının rolü, `ProcessProgress.CanActOnStep`). Panelde görünen her iş, kullanıcının gerçekten yapabileceği bir iştir |
| 3 | İş Analisti bütün analistlerin işlerini mi görür? | Hayır. Adım İş Analisti'nin ise yalnızca **analisti olduğu** projeler, Proje Yöneticisi'nin ise yalnızca **yöneticisi olduğu** projeler listelenir. Projeye o rolde kimse atanmamışsa adım o roldeki herkese görünür. Diğer roller (Yazılım Departman Yöneticisi, Satış, Test, Geliştirici) rolündeki bütün işleri görür |
| 4 | "Yalnızca benim projelerim" filtresi | `?scope=mine`: genel bölümler yalnızca kullanıcının analist ya da proje yöneticisi olduğu projelerden hesaplanır. Varsayılan `all` |
| 5 | Kapanış tarihi nereden gelir? | `Project`'te kapanış tarihi alanı yoktur. Kapanmış kaydın **son süreç kaydının** (`StepRecord.PerformedAt`) tarihi kullanılır. Yeni kolon gerekmez |
| 6 | "Ne kadar süredir bekliyor" nasıl hesaplanır? | Son süreç kaydından bu yana geçen gün; hiç kaydı yoksa talebin açıldığı tarihten (`CreatedAt`) |
| 7 | Liste uzunlukları | Her liste en fazla 10 satır ve toplam sayı döner (`totalCount`). Tamamı için ilgili sayfaya bağlantı verilir. Bu yüzden panel sayfalanmaz |
| 8 | Grafik kütüphanesi | **Eklenmez.** Aşama dağılımı ve ilerleme çubukları CSS ile çizilir (yığına yeni paket eklememek için) |
| 9 | Risk, seviye ve denetim göstergeleri | Risks, Assessment ve Audit modülleri gelince eklenir (aşağıda "Sonra eklenecekler") |

## Uç nokta

| İstek | Açıklama | Başarılı cevap |
| --- | --- | --- |
| `GET /dashboard` | Panelin bütün verisi tek istekte | `200` `DashboardDto` |
| `GET /dashboard?scope=mine` | Genel bölümler yalnızca kullanıcının projelerinden | `200` `DashboardDto` |

- **Okuma:** giriş yapmış her kullanıcı. Panel yalnızca okur; hiçbir şey değiştirmez.
- `scope` yalnızca **genel** bölümleri etkiler; "Bana bekleyenler" bölümleri zaten kişiseldir.
- "Bugün" UTC tarihidir (açık konulardaki gecikme hesabıyla aynı).

## DTO

```jsonc
// DashboardDto
{
  "scope": "All",                       // "All" | "Mine"

  // ---- Özet kartları ----
  "summary": {
    "waitingRequests": 6,               // onay bekleyen talepler: aktif, aşama 1–3 (Projects phase=Request)
    "activeProjects": 4,                // aktif, aşama 4–13 (Projects phase=Project&status=Active)
    "myWaitingSteps": 3,                // bana bekleyen adımların toplamı (karar 2, 3)
    "overdueOpenIssues": 5,             // aktif projelerde geciken açık konular
    "overdueProjects": 1,               // aktif ve bitiş tarihi (DueDate) geçmiş projeler
    "finishedThisYear": {               // bu yıl sonuçlanan kayıtlar (karar 5)
      "closed": 2, "lost": 1, "rejected": 1, "redirected": 0
    }
  },

  // ---- Aşamalara göre aktif kayıtlar (13 satır, boş aşamalar dahil) ----
  "stages": [
    { "stageOrder": 1, "shortTitle": "Talep Kaydı", "count": 2 },
    { "stageOrder": 2, "shortTitle": "Talep Anlama & Seviye", "count": 1 }
    // ... 13'e kadar
  ],

  // ---- Bana bekleyen adımlar (en uzun bekleyen önce) ----
  "myWaitingSteps": {
    "totalCount": 3,
    "items": [
      {
        "project": { "id": 3, "code": "ISDLC-2026-016", "name": "B2B Sipariş Portalı v2" },
        "step": { "id": 31, "code": "4.5", "stageOrder": 4, "stageShortTitle": "Ön Analiz & Plan" },
        "isApproval": true,               // adımın ApproverRole'ü var
        "isDecision": true,
        "waitingSince": "2026-11-03T09:00:00Z",
        "waitingDays": 7
      }
    ]
  },

  // ---- Bana atanan açık konular (kapanmamış; geciken önce, sonra termin, sonra kod) ----
  "myOpenIssues": {
    "totalCount": 2,
    "items": [
      {
        "project": { "id": 1, "code": "ISDLC-2026-014", "name": "Saha Servis Mobil Uygulaması" },
        "id": 12, "code": "AK-003", "type": "Action", "title": "İş emri ekran taslakları",
        "dueDate": "2026-11-12", "status": "Open", "isOverdue": false
      }
    ]
  },

  // ---- Geciken açık konular (aktif projeler; en eski termin önce) ----
  "overdueOpenIssues": {
    "totalCount": 5,
    "items": [ /* myOpenIssues ile aynı satır + "owner": { "userId": null, "name": "Müşteri BT" } */ ]
  },

  // ---- Son hareketler (son 10 süreç kaydı, en yeni önce) ----
  "recentActivity": [
    {
      "project": { "id": 2, "code": "ISDLC-2026-015", "name": "İK İzin Yönetim Modülü" },
      "stepCode": "7.4",
      "action": "Completed",              // StepAction
      "performedBy": { "id": 1, "fullName": "Elif Yılmaz" },
      "performedAt": "2026-11-09T14:20:00Z"
    }
  ]
}
```

| Alan | Kural |
| --- | --- |
| `summary.waitingRequests`, `summary.activeProjects` | Projects'teki `phase` tanımının aynısı; panel ile listeler aynı sayıyı gösterir |
| `summary.overdueOpenIssues`, `overdueOpenIssues` | `Status != Closed` ve `DueDate` bugünden önce; yalnızca aktif projeler (kapanmış projenin açık konusu panelde gecikme sayılmaz) |
| `summary.overdueProjects` | `Status = Active` ve `DueDate` bugünden önce |
| `summary.finishedThisYear` | Durumu `Active` olmayan ve son süreç kaydı bu yıl (UTC) olan kayıtlar, duruma göre |
| `stages` | Yalnızca aktif kayıtlar, mevcut adımın aşamasına göre; 13 aşamanın hepsi döner (sayısı 0 olanlar dahil) |
| `myWaitingSteps` | Karar 2 ve 3. Sıralama: `waitingSince` en eski önce |
| `myOpenIssues` | `OwnerUserId` = kullanıcı, `Status != Closed`, yalnızca aktif projeler |
| `recentActivity` | `StepRecord`'lar; `scope=mine` iken yalnızca kullanıcının projelerinden |

## Ekran

Menüdeki **Gösterge Paneli** (`/dashboard`, girişten sonra açılan ilk sayfa). Yukarıdan aşağıya:

1. **Başlık:** "Merhaba, Elif" ve sağda "Tüm projeler / Yalnızca benim projelerim" seçimi (`scope`; seçim adres çubuğunda tutulur).
2. **Özet kartları:** Onay bekleyen talepler · Aktif projeler · Bana bekleyen adımlar · Geciken açık konular ·
   Takvimi geçen projeler · Bu yıl sonuçlanan (kapandı / kaybedildi / reddedildi / yönlendirildi).
   Her kart ilgili listeye gider (ör. "Onay bekleyen talepler" → `/requests`).
3. **İki sütun:**
   - Sol: **Bana bekleyen adımlar** (proje, adım, "onay" rozeti, kaç gündür bekliyor) → satır projenin Süreç sekmesini açar.
   - Sağ: **Bana atanan açık konular** → satır projenin Açık Konular sekmesini açar.
4. **Aşamalara göre aktif kayıtlar:** 13 aşamalık yatay çubuklar (aşama 1–3 talep, 4–13 proje rengiyle).
5. **Geciken açık konular** ve **Son hareketler** (iki sütun).

Liste boşsa kısa bir mesaj gösterilir ("Bekleyen adımınız yok."). Mobilde sütunlar alt alta gelir.

## Sonra eklenecekler

| Gösterge | Bekleyen modül | Sorumlu |
| --- | --- | --- |
| Yüksek skorlu riskler (olasılık × etki) | `Risks` | Baran |
| Seviyeye göre proje dağılımı, kabul puanı bekleyen talepler | `Assessment` | Baran |
| Son değişiklikler (alan bazında) | `Audit` API'si | Erva |
| Okunmamış bildirimler | `Notifications` | Baran |
| Gereksinim ve doküman ilerlemesi (proje bazında) | — (veri hazır; ilk sürümü kalabalıklaştırmamak için sonraya) | Dide |

## Uygulama sırası (her biri ayrı PR)

1. Bu doküman (ekip onayı)
2. `DashboardService` ve `GET /dashboard`; testler (bekleyen adım kuralı, kapsam filtresi, sayılar, sıralama)
3. Gösterge paneli ekranı
