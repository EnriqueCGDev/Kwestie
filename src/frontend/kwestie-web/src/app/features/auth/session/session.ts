import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';

@Component({
  templateUrl: './session.html',
})
export class Session {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  readonly pending = signal(false);
  readonly errorMessage = signal<string | null>(null);

  logout(): void {
    if (this.pending()) return;

    this.pending.set(true);
    this.errorMessage.set(null);
    this.auth.logout()
      .pipe(finalize(() => this.pending.set(false)), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => { void this.router.navigateByUrl('/login'); },
        error: () => this.errorMessage.set('No pudimos cerrar sesión. Inténtalo de nuevo.'),
      });
  }
}
