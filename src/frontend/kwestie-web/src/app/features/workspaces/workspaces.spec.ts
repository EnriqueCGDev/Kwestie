import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { Subject } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { CreateWorkspaceResponse, WorkspaceSummary } from './workspace.models';
import { WorkspaceService } from './workspace.service';
import { Workspaces } from './workspaces';

describe('Workspaces', () => {
  let listing: Subject<WorkspaceSummary[]>;
  let creation: Subject<CreateWorkspaceResponse>;
  let logoutResult: Subject<void>;
  let service: { list: ReturnType<typeof vi.fn>; create: ReturnType<typeof vi.fn> };
  let auth: { logout: ReturnType<typeof vi.fn> };
  let navigate: ReturnType<typeof vi.spyOn>;
  let fixture: ComponentFixture<Workspaces>;

  beforeEach(() => {
    listing = new Subject<WorkspaceSummary[]>();
    creation = new Subject<CreateWorkspaceResponse>();
    logoutResult = new Subject<void>();
    service = { list: vi.fn(() => listing.asObservable()), create: vi.fn(() => creation.asObservable()) };
    auth = { logout: vi.fn(() => logoutResult.asObservable()) };
    TestBed.configureTestingModule({ providers: [provideRouter([]),
      { provide: WorkspaceService, useValue: service }, { provide: AuthService, useValue: auth }] });
    navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    fixture = TestBed.createComponent(Workspaces);
    fixture.detectChanges();
  });

  function finishListing(workspaces: WorkspaceSummary[] = []) {
    listing.next(workspaces);
    listing.complete();
    fixture.detectChanges();
  }

  it('loads once on entry and shows loading followed by a useful empty state', () => {
    expect(service.list).toHaveBeenCalledOnce();
    expect(fixture.nativeElement.textContent).toContain('Cargando Workspaces');
    fixture.detectChanges();
    expect(service.list).toHaveBeenCalledOnce();
    finishListing();
    expect(fixture.nativeElement.textContent).toContain('Aún no tienes Workspaces');
    expect(fixture.nativeElement.querySelector('.workspace-list').getAttribute('aria-busy')).toBe('false');
  });

  it('renders server names and readable dates without technical IDs or fictitious navigation', () => {
    finishListing([{ workspaceId: 'technical-id', name: 'Support team', createdAt: '2026-10-07T12:00:00Z' }]);
    const card: HTMLElement = fixture.nativeElement.querySelector('.workspace-card');
    expect(card.textContent).toContain('Support team');
    expect(card.querySelector('time')?.textContent).toMatch(/07\/10\/2026, \d{2}:\d{2}/);
    expect(card.querySelector('time')?.getAttribute('datetime')).toBe('2026-10-07T12:00:00Z');
    expect(card.textContent).not.toContain('technical-id');
    expect(card.querySelector('a, button')).toBeNull();
    expect(navigate).not.toHaveBeenCalled();
  });

  it('does not create with an invalid required field', () => {
    finishListing();
    fixture.componentInstance.create();
    fixture.detectChanges();
    expect(service.create).not.toHaveBeenCalled();
    expect(fixture.nativeElement.querySelector('#name-error').textContent).toContain('Introduce un nombre');
  });

  it('creates only once while pending, clears the form and reloads authoritative data after success', () => {
    finishListing();
    const component = fixture.componentInstance;
    component.form.setValue({ name: 'New team' });
    component.create();
    component.create();
    fixture.detectChanges();
    expect(service.create).toHaveBeenCalledExactlyOnceWith({ name: 'New team' });
    expect(fixture.nativeElement.querySelector('[type="submit"]').disabled).toBe(true);
    expect(service.list).toHaveBeenCalledOnce();

    listing = new Subject<WorkspaceSummary[]>();
    creation.next({ workspaceId: 'new-id' });
    creation.complete();
    fixture.detectChanges();
    expect(service.list).toHaveBeenCalledTimes(2);
    expect(component.form.getRawValue()).toEqual({ name: '' });
    expect(component.workspaces()).toEqual([]);
    expect(fixture.nativeElement.textContent).toContain('Workspace creado correctamente');
    // Submission stays blocked during the reload, preventing overlapping lists.
    component.form.setValue({ name: 'Another team' });
    component.create();
    expect(service.create).toHaveBeenCalledOnce();
    finishListing([{ workspaceId: 'new-id', name: 'New team', createdAt: '2026-10-07T12:00:00Z' }]);
    expect(fixture.nativeElement.textContent).toContain('New team');
  });

  it('shows a generic loading error without removing the form or Logout', () => {
    listing.error(new HttpErrorResponse({ status: 401, error: { detail: 'internal detail' } }));
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role="alert"]').textContent).toContain('No pudimos cargar');
    expect(fixture.nativeElement.textContent).not.toContain('internal detail');
    expect(fixture.nativeElement.querySelector('form')).not.toBeNull();
    expect(fixture.componentInstance.loading()).toBe(false);
    expect(navigate).not.toHaveBeenCalled();
  });

  it('shows a generic creation error and preserves the entered name without reloading', () => {
    finishListing();
    fixture.componentInstance.form.setValue({ name: 'Support' });
    fixture.componentInstance.create();
    creation.error(new HttpErrorResponse({ status: 400, error: { detail: 'internal exception' } }));
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role="alert"]').textContent).toContain('No pudimos crear');
    expect(fixture.nativeElement.textContent).not.toContain('internal exception');
    expect(fixture.componentInstance.form.controls.name.value).toBe('Support');
    expect(fixture.componentInstance.creating()).toBe(false);
    expect(service.list).toHaveBeenCalledOnce();
  });

  it('keeps existing data and creation confirmation if the subsequent reload fails', () => {
    const existing = { workspaceId: 'existing-id', name: 'Existing', createdAt: '2026-10-07T12:00:00Z' };
    finishListing([existing]);
    fixture.componentInstance.form.setValue({ name: 'New team' });
    fixture.componentInstance.create();
    listing = new Subject<WorkspaceSummary[]>();
    creation.next({ workspaceId: 'new-id' });
    creation.complete();
    listing.error(new Error('network failure'));
    fixture.detectChanges();
    expect(fixture.componentInstance.workspaces()).toEqual([existing]);
    expect(fixture.nativeElement.textContent).toContain('Workspace creado correctamente');
    expect(fixture.nativeElement.querySelector('[role="alert"]').textContent).toContain('No pudimos cargar');
  });

  it('logs out once and navigates only after success', () => {
    fixture.componentInstance.logout();
    fixture.componentInstance.logout();
    fixture.detectChanges();
    expect(auth.logout).toHaveBeenCalledOnce();
    expect(fixture.nativeElement.querySelector('header button').disabled).toBe(true);
    expect(navigate).not.toHaveBeenCalled();
    logoutResult.next();
    logoutResult.complete();
    expect(navigate).toHaveBeenCalledWith('/login');
  });

  it('stays in place and shows an error when logout fails', () => {
    fixture.componentInstance.logout();
    logoutResult.error(new Error('server error'));
    fixture.detectChanges();
    expect(navigate).not.toHaveBeenCalled();
    expect(fixture.nativeElement.querySelector('[role="alert"]').textContent).toContain('No pudimos cerrar sesión');
    expect(fixture.nativeElement.querySelector('header button').disabled).toBe(false);
  });

  it('cancels pending feature requests when the screen is destroyed', () => {
    expect(listing.observed).toBe(true);
    fixture.destroy();
    expect(listing.observed).toBe(false);
  });
});
