import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators, type AbstractControl } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';

import { AuthService } from '../../../core/services/auth.service';
import { AuthStore } from '../../../core/state/auth.store';
import { LanguagePicker } from '../../../layout/language-picker/language-picker';
import { BrandMark } from '../../../shared/ui/brand-mark/brand-mark';
import { Button } from '../../../shared/ui/button/button';
import { Spinner } from '../../../shared/ui/spinner/spinner';

/**
 * Giris ekrani. `POST /api/v1/auth/login` sozlesmesine gore calisir;
 * backend ayakta degilse ag hatasi `errors.network` olarak gosterilir.
 */
@Component({
  selector: 'hc-login',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, TranslatePipe, BrandMark, Button, Spinner, LanguagePicker],
  templateUrl: './login.html',
})
export class LoginPage {
  private readonly formBuilder = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly authStore = inject(AuthStore);
  /** Gonderim denendi mi — hatalar ancak o zaman gosterilir. */
  protected readonly submitted = signal(false);

  protected readonly form = this.formBuilder.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(8)]],
  });

  protected readonly busy = computed(() => this.authStore.isBusy());

  protected async submit(): Promise<void> {
    this.submitted.set(true);
    this.authStore.clearError();

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const { email, password } = this.form.getRawValue();
    const success = await this.authService.login({ email: email.trim(), password });
    if (!success) {
      return;
    }

    const redirectTo = this.route.snapshot.queryParamMap.get('redirectTo');
    await this.router.navigateByUrl(
      redirectTo && redirectTo !== '/login' ? redirectTo : '/dashboard',
    );
  }

  /** Bir alan icin gosterilecek i18n hata anahtari (yoksa `null`). */
  protected errorKeyFor(controlName: 'email' | 'password'): string | null {
    const control: AbstractControl | null = this.form.get(controlName);
    if (!control || control.valid || (!control.touched && !this.submitted())) {
      return null;
    }
    if (control.hasError('required')) {
      return controlName === 'email'
        ? 'auth.validation.emailRequired'
        : 'auth.validation.passwordRequired';
    }
    if (control.hasError('email')) {
      return 'auth.validation.emailInvalid';
    }
    if (control.hasError('minlength')) {
      return 'auth.validation.passwordMinLength';
    }
    return null;
  }
}
