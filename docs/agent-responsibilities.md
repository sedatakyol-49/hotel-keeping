# HotelCore — Ajan Sorumlulukları

> Bu proje **agentic (multi-agent)** bir yapıda organize edilmiştir. Her katman, bir
> "ajan" (Claude Code alt-görev / skill tanımı) tarafından yönetilir. Main Agent
> (orkestratör) tüm alt ajanları koordine eder. Ajan tanımları `.claude/agents/`,
> tekrar kullanılabilir yetenekler `.claude/skills/*/SKILL.md` altındadır.

## Ajan Haritası

| Ajan | Dosya | Sorumluluk alanı |
|---|---|---|
| **Main Agent** | `.claude/agents/main-agent.md` | Orkestrasyon, görev dağıtımı, API sözleşmesi tutarlılığı, karar günlüğü |
| **Frontend Agent** | `.claude/agents/frontend-agent.md` | Angular + Tailwind, Signals, i18n, responsive/PWA, "Otel Defteri" tasarım sistemi |
| **Backend Agent** | `.claude/agents/backend-agent.md` | .NET Web API, Clean Architecture, RBAC, GoBD, Swagger |
| **Database Agent** | `.claude/agents/database-agent.md` | EF Core entity/migration, PostgreSQL, seed, global query filter |
| **DevOps/QA Agent** | `.claude/agents/devops-agent.md` | Testler, lint/format, GitHub Actions CI/CD |

## Skill Haritası

| Skill | Ne zaman tetiklenir |
|---|---|
| `frontend-angular-tailwind` | Component/route/service/tema/i18n işi |
| `backend-dotnet-ef` | Controller/handler/DTO/validation/auth işi |
| `database-migrations` | Entity değişikliği, migration üretme/uygulama, seed |
| `api-integration` | Frontend↔backend sözleşmesi, OpenAPI client üretimi |
| `testing` | Unit/integration/e2e test, CI davranışı |

## Çalışma Protokolü (ajanlar arası)
1. **Sözleşme önce:** Bir endpoint değişecekse Backend Agent OpenAPI şemasını günceller;
   Frontend Agent client'ı yeniden üretir. Uyuşmazlıkları Main Agent çözer.
2. **İsimlendirme otoritesi:** DTO/endpoint/enum isimleri Main Agent'ta merkezîleşir
   (`docs/api-contracts.md`). Çakışmada Main Agent karar verir.
3. **Domain değişikliği** → önce Database Agent (entity + migration), sonra Backend Agent
   (handler/DTO), sonra Frontend Agent (UI). Bu sıra bozulmaz.
4. **Her önemli mimari karar** `docs/architecture.md` §10 Karar Günlüğü'ne eklenir.
5. **Yeni modül/agent eklemek:** yeni bir `.claude/agents/<x>-agent.md` + gerekirse
   `.claude/skills/<x>/SKILL.md` yaz; Main Agent haritalara ekler. Mimari buna açıktır
   (örn. ileride `notification-agent`, `pdf-export-agent`, `reporting-agent`).

## Sorumluluk Sınırları (kim neye dokunur)
- **Frontend Agent** yalnızca `src/frontend/` altına yazar; backend'e dokunmaz.
  Public kanalla birlikte **üç hedef** vardır: `src/` (proje adı `hotelcore-web`, admin, CSR),
  `projects/guest-web` (misafir, SSR/prerender) ve `projects/shared` (kütüphane,
  import yolu `@hotelcore/shared`).
  **Sert kural:** paylaşılan kütüphaneye **JWT'ye dokunan hiçbir şey** (auth interceptor, token
  deposu, permission guard, hotel switcher) ve admin API client'ları **konulamaz** — kütüphane
  misafir paketine giriyor. Bkz. `architecture-public-booking.md` §2.1.
- **Backend Agent** `src/backend/HotelCore.{Application,Api}` — iş mantığı ve API.
- **Database Agent** `src/backend/HotelCore.{Domain,Infrastructure}` — entity ve persistence.
  (Domain, Backend ve Database ajanlarının ortak alanıdır; entity şekli Database, davranış Backend.)
- **DevOps Agent** `.github/`, test projeleri, lint config'leri, `docker*`.
- **Main Agent** `docs/`, kök `README.md`, ve ajanlar arası koordinasyon.
