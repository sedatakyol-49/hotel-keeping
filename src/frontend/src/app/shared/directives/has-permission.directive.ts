import { Directive, TemplateRef, ViewContainerRef, effect, inject, input } from '@angular/core';

import type { PermissionKey, PermissionMatchMode } from '../../core/models/permission.model';
import { AuthStore } from '../../core/state/auth.store';

/**
 * Yapisal direktif — izin yoksa icerik DOM'a hic basilmaz.
 *
 * ```html
 * <p *hcHasPermission="'Invoices.View'">…</p>
 * <p *hcHasPermission="['Invoices.Approve', 'Invoices.Cancel']; mode: 'all'">…</p>
 * ```
 *
 * Not: bu yalnizca **gorunurluk** kontroludur; yetki her zaman backend policy'si
 * tarafindan da dogrulanir (mimari §7).
 */
@Directive({ selector: '[hcHasPermission]' })
export class HasPermissionDirective {
  private readonly templateRef = inject(TemplateRef<unknown>);
  private readonly viewContainer = inject(ViewContainerRef);
  private readonly authStore = inject(AuthStore);

  readonly hcHasPermission = input.required<PermissionKey | readonly PermissionKey[]>();
  readonly hcHasPermissionMode = input<PermissionMatchMode>('any');

  private rendered = false;

  constructor() {
    effect(() => {
      const value = this.hcHasPermission();
      const required: readonly PermissionKey[] = Array.isArray(value)
        ? value
        : [value as PermissionKey];
      const allowed = this.authStore.matchesPermissions(required, this.hcHasPermissionMode());

      if (allowed && !this.rendered) {
        this.viewContainer.createEmbeddedView(this.templateRef);
        this.rendered = true;
      } else if (!allowed && this.rendered) {
        this.viewContainer.clear();
        this.rendered = false;
      }
    });
  }
}
