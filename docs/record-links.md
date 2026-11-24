# Kayıt Bağlantıları — Bağlantıyı kopyala

Faz 5 · F8 ([phase-5-plan.md](phase-5-plan.md)). Bir proje, süreç iyileştirme önerisi ya da proje içi kayıt açıkken
"Bağlantıyı kopyala" düğmesi o kaydın adresini panoya kopyalar; bağlantıyı açan kişi aynı sekmeyi ve aynı kaydı görür.
Durum: **taslak** (24 Kasım 2026). Sorumlu: Baran.

Yalnızca frontend değişir; yeni uç nokta, tablo ya da kolon yoktur.

## Kararlar

| # | Konu | Karar |
| --- | --- | --- |
| L1 | Bağlantının biçimi | Proje → `/projects/{id}`; öneri → `/process-improvements/{id}`; proje içi kayıt → `/projects/{projectId}?tab=<sekme>&id=<kayıt id>`. Sekme adları proje detayındakilerdir: `open-issues`, `risks`, `requirements`, `meetings` |
| L2 | Nerede düğme var? | Proje detayının başlığı, öneri detayının başlığı; açık konu, risk ve gereksinim formu (yalnızca kayıtlı bir kayıt açıkken, "Yeni …" formunda yok); tutanak detayı |
| L3 | Bağlantı açılınca | Proje detayı `tab` sekmesini açar; sekme `id` kaydının formunu (tutanakta detayını) açar. Kayıt yoksa ya da başka projeye aitse form kendi "bulunamadı" hatasını gösterir |
| L4 | Kayıt kapanınca | Form ya da detay kapanınca adresteki `id` silinir; sayfa yenilenince kayıt yeniden açılmaz. Sekme değişince de `id` silinir |
| L5 | Kopyalama | Tam adres (`http://…/projects/14?tab=risks&id=12`) panoya yazılır; düğme 2 saniye "Kopyalandı" yazar. Pano kullanılamıyorsa "Kopyalanamadı" yazar ve adres düğmenin `title`'ında görünür |
| L6 | Dokümanlar | Doküman sekmesinde ayrı bir kayıt detayı olmadığı için düğme yoktur |
| L7 | Genel arama | F7'deki arama sonuçları da aynı bağlantıyı kullanır; proje içi kayıt sonucu artık kaydın kendisini açar |
| L8 | Giriş | Bağlantıyı giriş yapmamış biri açarsa önce giriş ekranı gelir, sonra aynı adrese döner (mevcut `returnUrl` davranışı) |

## Kod

- `shared/components/copy-link-button`: `app-copy-link-button`, girdi olarak router komutlarını ve sorgu parametrelerini alır.
- `shared/models/record-link.ts`: bağlantıları kuran saf fonksiyonlar (`projectLink`, `improvementLink`, `projectRecordLink`);
  düğmeler ve genel arama aynı fonksiyonları kullanır.
- Liste bileşenleri (`app-risk-list`, `app-open-issue-list`, `app-requirement-list`, `app-meeting-list`) `openId` girdisi alır
  ve kapanınca `closedRecord` çıktısı verir; proje detayı `id` parametresini bunlara bağlar.
