/**
 * Vitest kurulum dosyasi.
 *
 * Angular 22 `@angular/build:unit-test` builder'i Vitest'i kullanir ve
 * TestBed baslatmasini kendisi yapar; burada yalnizca jsdom'da eksik olan
 * tarayici API'lari doldurulur.
 */

// jsdom `matchMedia` saglamaz; responsive yardimcilar bunu bekleyebilir.
if (typeof globalThis.matchMedia !== 'function') {
  Object.defineProperty(globalThis, 'matchMedia', {
    writable: true,
    value: (query: string): MediaQueryList =>
      ({
        matches: false,
        media: query,
        onchange: null,
        addEventListener: () => undefined,
        removeEventListener: () => undefined,
        addListener: () => undefined,
        removeListener: () => undefined,
        dispatchEvent: () => false,
      }) as unknown as MediaQueryList,
  });
}
