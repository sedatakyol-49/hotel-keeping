import { ChangeDetectionStrategy, Component, computed, effect, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink, convertToParamMap } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';

import type { GuestListQuery, GuestResponse } from '../../core/models/guest.model';
import { PERMISSIONS } from '../../core/models/permission.model';
import { HasPermissionDirective } from '../../shared/directives/has-permission.directive';
import { parseInteger } from '../../shared/forms/numeric-validators';
import { Button } from '../../shared/ui/button/button';
import { EmptyState } from '../../shared/ui/empty-state/empty-state';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { Spinner } from '../../shared/ui/spinner/spinner';
import { TableShell } from '../../shared/ui/table-shell/table-shell';
import {
  GUEST_PAGE_SIZE_OPTIONS,
  guestListQueryToParams,
  parseGuestListQuery,
  withGuestFilterChange,
} from './guest-list-query';
import { GuestsStore } from './guests.store';

/**
 * Misafir listesi (`GET /guests`) — sayfali + arama.
 *
 * Sozlesme notu: `stayCount` **listede `null`** doner (satir basina korele alt
 * sorgudan kacinmak icin), bu yuzden listede boyle bir sutun **hic cizilmez**;
 * deger yalnizca detay/duzenleme ekraninda gosterilir. Bos sutun gostermek
 * kullaniciyi "0 konaklama" sanisina dusururdu.
 *
 * Silme: aktif veya gelecek tarihli rezervasyonu olan misafir silinemez
 * (**409**); mesaj sunucunun `detail` metniyle birlikte satirin yaninda
 * gosterilir.
 */
@Component({
  selector: 'hc-guest-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    TranslatePipe,
    PageHeader,
    TableShell,
    EmptyState,
    Spinner,
    Button,
    HasPermissionDirective,
  ],
  templateUrl: './guest-list.html',
})
export class GuestListPage {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly store = inject(GuestsStore);

  protected readonly writePermission = PERMISSIONS.ReservationsCreate;
  protected readonly pageSizeOptions = GUEST_PAGE_SIZE_OPTIONS;

  /** Silme onayi acik olan satir. */
  protected readonly confirmingId = signal<string | null>(null);

  private readonly queryParams = toSignal(this.route.queryParamMap, {
    initialValue: convertToParamMap(this.route.snapshot.queryParams),
  });

  protected readonly query = computed(() => parseGuestListQuery(this.queryParams()));

  protected readonly deleteErrorKey = computed(() => {
    const error = this.store.deleteError();
    if (!error) {
      return null;
    }
    return error.status === 409 ? 'guests.delete.conflict' : error.messageKey;
  });

  constructor() {
    effect(() => {
      this.confirmingId.set(null);
      void this.store.load(this.query());
    });
  }

  protected applySearch(search: string): void {
    this.applyFilters({ search: search.trim() || null });
  }

  protected onPageSizeChange(event: Event): void {
    const pageSize = parseInteger((event.target as HTMLSelectElement).value);
    if (pageSize !== null) {
      this.applyFilters({ pageSize });
    }
  }

  protected resetFilters(): void {
    void this.navigate({ ...this.query(), page: 1, search: null });
  }

  protected goToPage(page: number): void {
    if (page < 1 || page > this.store.totalPages()) {
      return;
    }
    void this.navigate({ ...this.query(), page });
  }

  protected retry(): void {
    void this.store.reload();
  }

  protected askDelete(guest: GuestResponse): void {
    this.store.clearDeleteError();
    this.confirmingId.set(guest.id);
  }

  protected cancelDelete(): void {
    this.confirmingId.set(null);
  }

  protected async remove(guest: GuestResponse): Promise<void> {
    const error = await this.store.remove(guest.id);
    if (error === null) {
      this.confirmingId.set(null);
    }
  }

  private applyFilters(changes: Partial<Omit<GuestListQuery, 'page'>>): void {
    void this.navigate(withGuestFilterChange(this.query(), changes));
  }

  private navigate(query: GuestListQuery): Promise<boolean> {
    return this.router.navigate([], {
      relativeTo: this.route,
      queryParams: guestListQueryToParams(query),
    });
  }
}
