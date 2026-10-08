import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, ValidatorFn } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { catchError, distinctUntilChanged, finalize, map, of, startWith, Subject, switchMap, takeUntil } from 'rxjs';
import { KwestieService } from '../../kwesties/kwestie.service';
import { WorkspaceSummary } from '../workspace.models';
import { WorkspaceService } from '../workspace.service';

const requiredTitle: ValidatorFn = control =>
  typeof control.value === 'string' && control.value.trim() ? null : { required: true };

@Component({
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './workspace.html',
  styleUrl: './workspace.scss',
})
export class Workspace implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly workspaces = inject(WorkspaceService);
  private readonly kwesties = inject(KwestieService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly retryLoad = new Subject<void>();
  private readonly workspaceChanged = new Subject<void>();

  readonly workspace = signal<WorkspaceSummary | null>(null);
  readonly loading = signal(true);
  readonly loadError = signal<string | null>(null);
  readonly canRetry = signal(false);
  readonly creating = signal(false);
  readonly createError = signal<string | null>(null);
  readonly created = signal(false);
  readonly form = new FormGroup({
    title: new FormControl('', { nonNullable: true, validators: requiredTitle }),
    description: new FormControl('', { nonNullable: true }),
    priority: new FormControl(2, { nonNullable: true }),
  });

  ngOnInit(): void {
    this.route.paramMap.pipe(
      map(params => params.get('workspaceId')),
      distinctUntilChanged(),
      switchMap(workspaceId => {
        this.workspaceChanged.next();
        this.form.reset();
        this.created.set(false);
        this.createError.set(null);
        return this.retryLoad.pipe(
          startWith(undefined),
          switchMap(() => {
            this.workspace.set(null);
            this.loading.set(true);
            this.loadError.set(null);
            this.canRetry.set(false);
            if (!workspaceId) {
              return of({ workspace: null, error: 'Este Workspace no está disponible.', retry: false });
            }
            return this.workspaces.get(workspaceId).pipe(
              map(workspace => ({ workspace, error: null, retry: false })),
              catchError((error: unknown) => of({
                workspace: null, error: this.loadMessage(error),
                retry: !(error instanceof HttpErrorResponse && [401, 404].includes(error.status)),
              })),
            );
          }),
        );
      }),
      takeUntilDestroyed(this.destroyRef),
    ).subscribe(result => {
      this.workspace.set(result.workspace);
      this.loadError.set(result.error);
      this.canRetry.set(result.retry);
      this.loading.set(false);
    });
  }

  retry(): void {
    if (!this.loading() && this.canRetry()) this.retryLoad.next();
  }

  create(): void {
    const workspace = this.workspace();
    if (!workspace || this.loading() || this.creating()) return;
    this.form.markAllAsTouched();
    if (this.form.invalid) return;

    this.creating.set(true);
    this.created.set(false);
    this.createError.set(null);
    this.kwesties.create(workspace.workspaceId, this.form.getRawValue()).pipe(
      takeUntil(this.workspaceChanged),
      finalize(() => this.creating.set(false)),
      takeUntilDestroyed(this.destroyRef),
    ).subscribe({
      next: () => {
        this.form.reset();
        this.created.set(true);
      },
      error: (error: unknown) => this.createError.set(this.createMessage(error)),
    });
  }

  private loadMessage(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      if (error.status === 404) return 'Este Workspace no está disponible.';
      if (error.status === 401) return 'Tu sesión no permite acceder a este Workspace. Inicia sesión de nuevo.';
    }
    return 'No pudimos cargar el Workspace. Inténtalo de nuevo.';
  }

  private createMessage(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      if (error.status === 400) return 'Los datos no son válidos. Revisa el formulario.';
      if (error.status === 401) return 'Tu sesión no permite completar la operación. Inicia sesión de nuevo.';
      if (error.status === 403) return 'No tienes acceso para crear Kwesties en este Workspace.';
    }
    return 'No pudimos crear la Kwestie. Inténtalo de nuevo.';
  }
}
