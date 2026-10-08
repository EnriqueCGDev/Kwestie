import { DatePipe } from '@angular/common';
import { Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { WorkspaceSummary } from './workspace.models';
import { WorkspaceService } from './workspace.service';

@Component({
  imports: [DatePipe, ReactiveFormsModule],
  templateUrl: './workspaces.html',
  styleUrl: './workspaces.scss',
})
export class Workspaces implements OnInit {
  private readonly service = inject(WorkspaceService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  readonly workspaces = signal<readonly WorkspaceSummary[]>([]);
  readonly loading = signal(false);
  readonly creating = signal(false);
  readonly loggingOut = signal(false);
  readonly loadError = signal<string | null>(null);
  readonly createError = signal<string | null>(null);
  readonly logoutError = signal<string | null>(null);
  readonly created = signal(false);
  readonly form = new FormGroup({
    name: new FormControl('', { nonNullable: true, validators: Validators.required }),
  });

  ngOnInit(): void {
    this.loadWorkspaces();
  }

  create(): void {
    if (this.creating() || this.loading()) return;
    this.form.markAllAsTouched();
    if (this.form.invalid) return;

    this.creating.set(true);
    this.created.set(false);
    this.createError.set(null);
    this.service.create(this.form.getRawValue())
      .pipe(finalize(() => this.creating.set(false)), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.form.reset();
          this.created.set(true);
          this.loadWorkspaces();
        },
        error: () => this.createError.set('No pudimos crear el Workspace. Revisa el nombre e inténtalo de nuevo.'),
      });
  }

  logout(): void {
    if (this.loggingOut()) return;
    this.loggingOut.set(true);
    this.logoutError.set(null);
    this.auth.logout()
      .pipe(finalize(() => this.loggingOut.set(false)), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => { void this.router.navigateByUrl('/login'); },
        error: () => this.logoutError.set('No pudimos cerrar sesión. Inténtalo de nuevo.'),
      });
  }

  private loadWorkspaces(): void {
    this.loading.set(true);
    this.loadError.set(null);
    this.service.list()
      .pipe(finalize(() => this.loading.set(false)), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: workspaces => this.workspaces.set(workspaces),
        error: () => this.loadError.set('No pudimos cargar tus Workspaces. Inténtalo de nuevo más tarde.'),
      });
  }
}
