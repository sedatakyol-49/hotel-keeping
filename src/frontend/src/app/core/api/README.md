# `core/api` — HTTP istemci katmani

Bu klasor backend ile konusan **tek** katmandir. Bilesenler ve store'lar
`HttpClient`'i dogrudan kullanmaz; buradaki servisleri cagirir.

## Su anki durum (iskelet fazi)

Backend heniz ayakta olmadigi icin servisler **elle yazilmis, tip-guvenli**
sarmalayicilardir (`auth.api.ts`). Tipler `core/models/` altindaki arayuzlerden
gelir ve `docs/api-contracts.md` ile birebir eslesir.

## OpenAPI'dan uretime gecis

Backend `dotnet run` ile ayaga kalktiginda (`http://localhost:5080/swagger`)
istemci sematan uretilir:

```bash
cd src/frontend
npx ng-openapi-gen --input http://localhost:5080/swagger/v1/swagger.json --output src/app/core/api/generated
```

Kurallar:

- Uretilen kod `src/app/core/api/generated/` altina yazilir ve **elle duzenlenmez**
  (`.prettierignore` ve lint kapsami disinda tutulmustur).
- Elle yazilmis servisler, uretilen istemci stabilize olduktan sonra tek tek
  kaldirilir; cagri noktalari degismesin diye ayni metot isimleri korunur.
- Taban adres `environment.apiBaseUrl` (`/api/v1`) uzerinden gelir; uretilen
  istemcinin `rootUrl` degeri de bu degere baglanir.

## Ortak davranislar (interceptor'lar halleder)

| Baslik                         | Kaynak                                           |
| ------------------------------ | ------------------------------------------------ |
| `Authorization: Bearer <jwt>`  | `core/interceptors/auth.interceptor.ts`          |
| `X-Hotel-Id: <guid>`           | `core/interceptors/hotel-context.interceptor.ts` |
| `Accept-Language: de\|en\|tr`  | `core/interceptors/language.interceptor.ts`      |
| `ProblemDetails` -> `ApiError` | `core/interceptors/error.interceptor.ts`         |

Bu nedenle tekil servisler header yonetimi yapmaz.
