import type { ActivatedRouteSnapshot } from '@angular/router';

/**
 * Rota `data` anahtari. `true` ise uygulama kabugu kenar cubugunu **ve** mobil
 * menu dugmesini gizler; icerik tam genislikte gosterilir.
 *
 * Tek kullanici: hub (launcher) ekrani `/dashboard`. Orada moduller kart
 * izgarasi olarak sunuldugu icin ikinci bir gezinme sutunu gereksizdir; bir
 * module girildiginde (`/rooms`, `/housekeeping` …) bayrak olmadigi icin kabuk
 * normal duzenine doner.
 */
export const HIDE_SIDEBAR = 'hideSidebar';

/**
 * Etkin rota agacini kokten en derin cocuga kadar tarar ve bayragi arar.
 *
 * Bilincli olarak `data` kalitimina (`paramsInheritanceStrategy`) guvenmez:
 * bayrak lazy yuklenen alt rotada tanimli olsa bile bulunur.
 */
export function shouldHideSidebar(root: ActivatedRouteSnapshot | null | undefined): boolean {
  let node = root ?? null;
  while (node) {
    if (node.data[HIDE_SIDEBAR] === true) {
      return true;
    }
    node = node.firstChild;
  }
  return false;
}
