import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';

@Component({
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './login.html',
})
export class Login {
  private readonly auth = inject(AuthService);
  private readonly destroyRef = inject(DestroyRef);

  readonly form = new FormGroup({
    email: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] }),
    password: new FormControl('', { nonNullable: true, validators: Validators.required }),
  });
  readonly submitted = signal(false);
  readonly pending = signal(false);
  readonly succeeded = signal(false);
  readonly errorMessage = signal<string | null>(null);

  submit(): void {
    if (this.pending()) return;
    this.submitted.set(true);
    this.form.markAllAsTouched();
    if (this.form.invalid) return;

    this.pending.set(true);
    this.succeeded.set(false);
    this.errorMessage.set(null);
    this.auth.login(this.form.getRawValue())
      .pipe(finalize(() => this.pending.set(false)), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => this.succeeded.set(true),
        error: (error: unknown) => this.errorMessage.set(
          error instanceof HttpErrorResponse && error.status === 401
            ? 'No pudimos iniciar sesión. Revisa tu correo y contraseña.'
            : 'No pudimos conectar en este momento. Inténtalo de nuevo.'),
      });
  }
}
