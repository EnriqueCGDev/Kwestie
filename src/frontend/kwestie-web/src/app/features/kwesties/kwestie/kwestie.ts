import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { distinctUntilChanged, map, Subscription } from 'rxjs';
import { KwestieDetails } from '../kwestie.models';
import { KwestieService } from '../kwestie.service';

@Component({
  imports: [DatePipe, RouterLink],
  templateUrl: './kwestie.html',
  styleUrl: './kwestie.scss',
})
export class Kwestie implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly service = inject(KwestieService);
  private readonly destroyRef = inject(DestroyRef);
  private kwestieId: string | null = null;
  private request?: Subscription;

  readonly workspaceId = signal<string | null>(null);
  readonly details = signal<KwestieDetails | null>(null);
  readonly loading = signal(true);
  readonly error = signal<{ message: string; retry: boolean } | null>(null);
  readonly statusLabels: Readonly<Record<number, string>> = {
    1: 'Abierta', 2: 'En progreso', 3: 'Resuelta', 4: 'Cerrada',
  };
  readonly priorityLabels: Readonly<Record<number, string>> = {
    1: 'Baja', 2: 'Normal', 3: 'Alta', 4: 'Crítica',
  };

  ngOnInit(): void {
    this.route.paramMap.pipe(
      map(params => ({ workspaceId: params.get('workspaceId'), kwestieId: params.get('kwestieId') })),
      distinctUntilChanged((previous, current) =>
        previous.workspaceId === current.workspaceId && previous.kwestieId === current.kwestieId),
      takeUntilDestroyed(this.destroyRef),
    ).subscribe(ids => {
      this.workspaceId.set(ids.workspaceId);
      this.kwestieId = ids.kwestieId;
      this.load();
    });
  }

  retry(): void {
    if (!this.loading() && this.error()?.retry) this.load();
  }

  private load(): void {
    this.request?.unsubscribe();
    this.details.set(null);
    this.error.set(null);
    this.loading.set(true);
    const workspaceId = this.workspaceId();
    const kwestieId = this.kwestieId;
    if (!workspaceId || !kwestieId) {
      this.error.set({ message: 'Este asunto no está disponible.', retry: false });
      this.loading.set(false);
      return;
    }

    this.request = this.service.get(workspaceId, kwestieId).pipe(
      takeUntilDestroyed(this.destroyRef),
    ).subscribe({
      next: details => {
        this.details.set(details);
        this.loading.set(false);
      },
      error: (error: unknown) => {
        this.error.set(this.loadError(error));
        this.loading.set(false);
      },
    });
  }

  private loadError(error: unknown): { message: string; retry: boolean } {
    if (error instanceof HttpErrorResponse) {
      if (error.status === 404) return { message: 'Este asunto no está disponible.', retry: false };
      if (error.status === 401) return {
        message: 'Tu sesión no permite acceder a este asunto. Inicia sesión de nuevo.', retry: false,
      };
    }
    return { message: 'No pudimos cargar el asunto. Inténtalo de nuevo.', retry: true };
  }
}
