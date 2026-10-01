import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';

@Component({
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './register.html',
})
export class Register {
  private readonly auth = inject(AuthService);
  private readonly destroyRef = inject(DestroyRef);

  readonly form = new FormGroup({
    email: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] }),
    password: new FormControl('', { nonNullable: true, validators: Validators.required }),
  });
  readonly submitted = signal(false);
  readonly pending = signal(false);
  readonly succeeded = signal(false);
  readonly errors = signal<readonly string[]>([]);

  submit(): void {
    if (this.pending() || this.succeeded()) return;
    this.submitted.set(true);
    this.form.markAllAsTouched();
    if (this.form.invalid) return;

    this.pending.set(true);
    this.errors.set([]);
    this.auth.register(this.form.getRawValue())
      .pipe(finalize(() => this.pending.set(false)), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => this.succeeded.set(true),
        error: (error: unknown) => {
          const details: unknown = error instanceof HttpErrorResponse ? error.error?.errors : null;
          this.errors.set(Array.isArray(details) && details.length > 0 &&
            details.every(item => typeof item === 'string' && item.trim().length > 0)
            ? details
            : ['No pudimos crear la cuenta. Inténtalo de nuevo.']);
        },
      });
  }
}
