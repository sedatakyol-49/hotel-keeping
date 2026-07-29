import { Injectable, computed, signal } from '@angular/core';

export type NotificationTone = 'info' | 'success' | 'warning' | 'danger';

export interface AppNotification {
  readonly id: number;
  /** i18n anahtari — metin sablonlara sabit yazilmaz. */
  readonly messageKey: string;
  readonly tone: NotificationTone;
  /** Teknik detay (ProblemDetails.detail) — gelistirici/destek icin. */
  readonly detail?: string;
}

/**
 * Global bildirim seridi (signal store). Interceptor'lar hata mesajlarini
 * buraya yazar, shell bileseni gosterir.
 */
@Injectable({ providedIn: 'root' })
export class NotificationStore {
  private nextId = 1;
  private readonly _items = signal<readonly AppNotification[]>([]);

  readonly items = this._items.asReadonly();
  readonly hasItems = computed(() => this._items().length > 0);

  push(messageKey: string, tone: NotificationTone = 'info', detail?: string): number {
    const id = this.nextId++;
    this._items.update((items) => [...items, { id, messageKey, tone, detail }]);
    return id;
  }

  dismiss(id: number): void {
    this._items.update((items) => items.filter((item) => item.id !== id));
  }

  clear(): void {
    this._items.set([]);
  }
}
