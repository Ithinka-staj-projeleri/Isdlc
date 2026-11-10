# Notifications Modülü — API Sözleşmesi

Uygulama içi bildirimler: üst bardaki zil, okunmamış sayısı ve bildirim listesi. Veri modeli: [data-model.md](data-model.md) →
`Notification`. Durum: **onaylandı** (#99); entity #101, servis ve API bu PR. Sorumlu: Baran.

Bütün uç noktalar AGENTS.md'deki ortak kurallara uyar: `/api/v1/` öneki, tarihler UTC / ISO 8601, hatalar `ProblemDetails`,
listeler `items` + `totalCount`, JSON alanları camelCase. Herkes **yalnızca kendi** bildirimlerini görür.

## Modüller nasıl bildirim gönderir (değişmiyor)

Modüller bugün olduğu gibi yalnızca `INotificationService`'i çağırır ([process-api.md](process-api.md) → Bildirim noktaları).
**Arayüz değişmez;** bu modül `NullNotificationService`'in yerine gerçek bir `NotificationService` kaydeder
(`DependencyInjection.cs`'te tek satır). Hiçbir modülün kodu değişmez.

```csharp
public interface INotificationService
{
    Task NotifyRoleAsync(UserRole role, int projectId, string message, CancellationToken cancellationToken);
    Task NotifyUsersAsync(IEnumerable<int> userIds, int projectId, string message, CancellationToken cancellationToken);
}
```

**Bugünkü gönderim noktaları** (bu modülle birlikte gerçekten kullanıcıya ulaşacaklar):

| Modül | Olay | Kime |
| --- | --- | --- |
| `Process` | Onay adımına gelindi (3.2, 4.5, 5.1) | Onaylayan roldeki kullanıcılar |
| `Process` | "Hayır" ile önceki bir adıma dönüldü | Projenin iş analisti |
| `Process` | Proje kapandı | İş analisti ve proje yöneticisi |
| `Projects` | İş analisti veya proje yöneticisi atandı | Atanan kişi |
| `OpenIssues` / `Meetings` | Açık konunun ya da tutanak aksiyonunun sorumlusu oldu | Sorumlu |
| `Risks` | Riskin sorumlusu oldu | Sorumlu |
| `Risks` | Risk yeni yüksek seviyeye çıktı | Projenin proje yöneticisi |

## Gönderim kuralları

- **Rol bildirimi** o anda o roldeki **her aktif kullanıcıya** ayrı bir bildirim olarak yazılır. Rolü sonradan alan kişi
  eski bildirimleri görmez; her kullanıcının kendi okundu bilgisi olur.
- **Pasif kullanıcıya** bildirim yazılmaz. Aynı çağrıda aynı kişi iki kez geçerse bir kez yazılır.
- **Kimse kendi yaptığı işlem için bildirim almaz.** Modüller bunu bugün de gözetiyor; servis ayrıca giriş yapan kullanıcıyı
  alıcılardan çıkarır. Böylece ör. bir yönetici kendi onay adımına ilerlettiği projede kendine "onayınızı bekliyor" almaz.
- Mesaj olduğu gibi saklanır (modülün yazdığı Türkçe metin, en fazla 500 karakter; uzunsa kısaltılır).
- **Bildirim asıl işlemi bozmaz.** Modüller bildirimi kayıt başarıyla kaydedildikten sonra gönderir; bildirim kaydedilemezse
  hata loga yazılır, kullanıcıya hata dönmez ve asıl işlem geri alınmaz. Bildirim kaybolabilir ama talep, adım ya da risk kaybolmaz.

## Uç noktalar

| İstek | Açıklama | Başarılı cevap |
| --- | --- | --- |
| `GET /notifications` | Giriş yapan kullanıcının bildirimleri, en yeni önce (sayfalı). `?unreadOnly=true` yalnızca okunmamışlar | `200` `PagedResult<NotificationDto>` |
| `GET /notifications/unread-count` | Zil üzerindeki sayı | `200` `{ "count": 3 }` |
| `POST /notifications/{notificationId}/read` | Bildirimi okundu say | `200` `NotificationDto` |
| `POST /notifications/read-all` | Bütün okunmamışları okundu say | `200` `{ "count": 3 }` (okundu sayılan) |

Başka bir kullanıcının bildiriminin `Id`'si istenirse kayıt yokmuş gibi 404 döner. Okunmuş bildirimi tekrar okundu saymak
hata değildir (ilk okunma zamanı değişmez).

## DTO'lar

```jsonc
// NotificationDto
{
  "id": 41,
  "project": { "id": 3, "code": "ISDLC-2026-016", "name": "B2B Sipariş Portalı" },
  "message": "ISDLC-2026-016 · 4.5 onayınızı bekliyor.",
  "createdAt": "2026-11-10T09:12:00Z",
  "readAt": null            // okunmadıysa null
}

// UnreadCountDto — GET /notifications/unread-count, POST /notifications/read-all
{ "count": 3 }
```

## Ekranlar (frontend)

| Ekran | Yer |
| --- | --- |
| Üst barda zil ve okunmamış sayısı; tıklayınca son 10 bildirim, "Tümünü okundu say", "Tümünü gör" | `core/layout/topbar` + `features/notifications` |
| Bildirimler sayfası (`/notifications`): bütün bildirimler, "Yalnızca okunmamışlar" filtresi, sayfalama | `features/notifications` |

- Bildirime tıklamak onu okundu sayar ve **proje detayını** açar.
- Sayı **60 saniyede bir** ve sayfa değişince yenilenir (anlık bağlantı yok; yeni paket gerekmez).
- Okunmamış bildirim kalın ve işaretli görünür; zamanı "10.11.2026 09:12" biçimindedir.

## Hata durumları

| Durum | Kod | Örnek `detail` |
| --- | --- | --- |
| Bildirim bulunamadı ya da başkasının | `404` | "Bildirim bulunamadı." |
| Giriş yapılmadı | `401` | |

## Kod yerleşimi

| Katman | Dosyalar |
| --- | --- |
| `Isdlc.Domain/Notifications/` | `Notification` |
| `Isdlc.Application/Notifications/` | `INotificationService` (mevcut), `NotificationService` (gönderim), `INotificationInboxService` ve `NotificationInboxService` (okuma, okundu sayma), DTO'lar |
| `Isdlc.Infrastructure/Notifications/` | `NotificationConfiguration` |
| `Isdlc.Api/Controllers/` | `NotificationsController` (`/api/v1/notifications`) |

`NullNotificationService` silinir; testlerdeki `RecordingNotificationService` olduğu gibi kalır.

## Kapsam dışı

- **Zamanlanmış bildirimler** (ör. "açık konunun termini geçti"): bir zamanlayıcı gerektirir; prototipteki "Otomasyon"
  kurallarıyla birlikte sonraki sürüm.
- **E-posta** ve anlık bağlantı (SignalR): sonraki sürüm.
- **Bildirimden belirli sekmeye gitmek** (ör. doğrudan Riskler sekmesi): arayüze bir bağlantı alanı eklemeyi, yani bütün
  gönderim noktalarını değiştirmeyi gerektirir; MVP'de proje detayı açılır.
- **Eski bildirimleri temizlemek:** MVP'de bildirim silinmez (kayıt silinmez kuralı).

## Öneri (Erva)

Demo senaryosu 4.3 "talep kabul edildi" bildirimini bekliyor. Bu bir `Process` gönderim noktasıdır (3.2'de "Evet" → iş analistine
"ISDLC-2026-016 proje olarak kabul edildi."); bu modül gelince `Process`'e tek bir `NotifyUsersAsync` çağrısıyla eklenebilir.

## Ekip onayı bekleyen kararlar

1. **Arayüz değişmez,** bütün modüller bugünkü çağrılarıyla çalışmaya devam eder.
2. **Rol bildirimi kişi başına yazılır** (o anki aktif kullanıcılara); her kişinin okundu bilgisi ayrıdır.
3. **Servis giriş yapan kullanıcıyı alıcılardan çıkarır;** kimse kendi işlemi için bildirim almaz.
4. **Bildirim kaydedilemezse asıl işlem geri alınmaz,** hata yalnızca loga yazılır.
5. **Sayı 60 saniyede bir yenilenir;** anlık bağlantı yok.
6. **Bildirime tıklamak proje detayını açar;** belirli sekmeye gitmek sonraya.
