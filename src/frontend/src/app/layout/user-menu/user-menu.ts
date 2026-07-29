import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

import { AuthService } from '../../core/services/auth.service';
import { AuthStore } from '../../core/state/auth.store';

/**
 * Kullanici menusu — avatar ikonu yerine tipografik bas harfler.
 * Escape ile kapanir, `aria-expanded`/`aria-controls` ile duyurulur.
 */
@Component({
  selector: 'hc-user-menu',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe],
  host: {
    '(document:keydown.escape)': 'close()',
  },
  template: `
    <div class="relative">
      <button
        type="button"
        class="flex min-h-touch items-center gap-2 border border-rule px-3 label-mono text-ink hover:bg-paper-sunken"
        [attr.aria-expanded]="open()"
        aria-haspopup="menu"
        aria-controls="hc-user-menu-panel"
        [attr.aria-label]="'nav.userMenu' | translate"
        (click)="toggle()"
      >
        <span aria-hidden="true">{{ authStore.initials() }}</span>
        <span class="hidden max-w-40 truncate normal-case tracking-normal md:inline">
          {{ authStore.displayName() }}
        </span>
      </button>

      @if (open()) {
        <div
          id="hc-user-menu-panel"
          role="menu"
          class="absolute right-0 z-30 mt-1 w-64 border border-rule bg-paper-raised"
        >
          <div class="border-b border-rule px-4 py-3">
            <p class="eyebrow">{{ 'auth.signedInAs' | translate }}</p>
            <p class="mt-1 truncate text-sm text-ink">{{ authStore.displayName() }}</p>
            <p class="truncate font-mono text-xs text-ink-muted">{{ authStore.user()?.email }}</p>
            @if (authStore.roles().length > 0) {
              <p class="mt-2 eyebrow">{{ 'auth.roles' | translate }}</p>
              <p class="font-mono text-xs text-ink-muted">{{ authStore.roles().join(' · ') }}</p>
            }
          </div>
          <button
            type="button"
            role="menuitem"
            class="w-full min-h-touch px-4 text-left label-mono text-ink hover:bg-paper-sunken"
            (click)="logout()"
          >
            {{ 'auth.logout' | translate }}
          </button>
        </div>
      }
    </div>
  `,
})
export class UserMenu {
  protected readonly authStore = inject(AuthStore);
  private readonly authService = inject(AuthService);

  protected readonly open = signal(false);

  protected toggle(): void {
    this.open.update((value) => !value);
  }

  protected close(): void {
    this.open.set(false);
  }

  protected async logout(): Promise<void> {
    this.close();
    await this.authService.logout();
  }
}
