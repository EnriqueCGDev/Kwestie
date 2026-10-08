import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, ParamMap, provideRouter } from '@angular/router';
import { BehaviorSubject, Subject } from 'rxjs';
import { CreateKwestieResponse } from '../../kwesties/kwestie.models';
import { KwestieService } from '../../kwesties/kwestie.service';
import { WorkspaceSummary } from '../workspace.models';
import { WorkspaceService } from '../workspace.service';
import { Workspace } from './workspace';

describe('Workspace', () => {
  let params: BehaviorSubject<ParamMap>;
  let loading: Subject<WorkspaceSummary>;
  let creation: Subject<CreateKwestieResponse>;
  let workspaces: { get: ReturnType<typeof vi.fn>; list: ReturnType<typeof vi.fn> };
  let kwesties: { create: ReturnType<typeof vi.fn> };
  let fixture: ComponentFixture<Workspace>;
  const selected = { workspaceId: 'selected-id', name: 'Support team', createdAt: '2026-10-08T12:00:00Z' };
  const input = { title: 'Printer broken', description: 'Cannot print', priority: 4 };

  beforeEach(() => {
    params = new BehaviorSubject(convertToParamMap({ workspaceId: 'selected-id' }));
    loading = new Subject<WorkspaceSummary>();
    creation = new Subject<CreateKwestieResponse>();
    workspaces = { get: vi.fn(() => loading.asObservable()), list: vi.fn() };
    kwesties = { create: vi.fn(() => creation.asObservable()) };
    TestBed.configureTestingModule({ providers: [provideRouter([]),
      { provide: ActivatedRoute, useValue: { paramMap: params.asObservable() } },
      { provide: WorkspaceService, useValue: workspaces },
      { provide: KwestieService, useValue: kwesties }] });
    fixture = TestBed.createComponent(Workspace);
    fixture.detectChanges();
  });

  function finishLoad(workspace = selected) {
    loading.next(workspace);
    loading.complete();
    fixture.detectChanges();
  }

  it('loads directly from the route using only the individual GET and shows the real name and back link', () => {
    expect(workspaces.get).toHaveBeenCalledExactlyOnceWith('selected-id');
    expect(workspaces.list).not.toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain('Cargando Workspace');
    expect(fixture.nativeElement.querySelector('form')).toBeNull();
    finishLoad();
    expect(fixture.nativeElement.querySelector('h1').textContent).toBe('Support team');
    expect(fixture.nativeElement.querySelector('header a').getAttribute('href')).toBe('/app');
    expect(fixture.componentInstance.form.getRawValue()).toEqual({ title: '', description: '', priority: 2 });
  });

  it.each([
    [404, 'Este Workspace no está disponible'],
    [401, 'Tu sesión no permite acceder'],
  ])('shows a neutral load message for %s without a creation form', (status, message) => {
    loading.error(new HttpErrorResponse({ status, error: { detail: 'internal' } }));
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role="alert"]').textContent).toContain(message);
    expect(fixture.nativeElement.querySelector('form')).toBeNull();
    expect(fixture.nativeElement.textContent).not.toContain('internal');
    fixture.componentInstance.form.setValue(input);
    fixture.componentInstance.create();
    expect(kwesties.create).not.toHaveBeenCalled();
  });

  it.each([0, 500])('blocks creation after load failure %s and allows one retry', status => {
    loading.error(new HttpErrorResponse({ status }));
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('form')).toBeNull();
    expect(fixture.nativeElement.querySelector('[role="alert"]').textContent).toContain('No pudimos cargar');
    loading = new Subject<WorkspaceSummary>();
    fixture.nativeElement.querySelector('main button').click();
    fixture.componentInstance.retry();
    expect(workspaces.get).toHaveBeenCalledTimes(2);
    finishLoad();
    expect(fixture.nativeElement.querySelector('form')).not.toBeNull();
  });

  it.each(['', '   ', '\t\n'])('rejects an empty or whitespace Title %j with an accessible error', title => {
    finishLoad();
    fixture.componentInstance.form.controls.title.setValue(title);
    fixture.componentInstance.create();
    fixture.detectChanges();
    expect(kwesties.create).not.toHaveBeenCalled();
    expect(fixture.nativeElement.querySelector('#kwestie-title').getAttribute('aria-invalid')).toBe('true');
    expect(fixture.nativeElement.querySelector('#title-error').textContent).toContain('Introduce un título');
  });

  it('sends a numeric selected Priority with an optional empty description and prevents duplicate POSTs', () => {
    finishLoad();
    fixture.componentInstance.form.controls.title.setValue('Printer broken');
    const select: HTMLSelectElement = fixture.nativeElement.querySelector('select');
    select.selectedIndex = 3;
    select.dispatchEvent(new Event('change'));
    fixture.nativeElement.querySelector('form').dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    fixture.componentInstance.create();
    fixture.detectChanges();
    expect(kwesties.create).toHaveBeenCalledExactlyOnceWith('selected-id', {
      title: 'Printer broken', description: '', priority: 4,
    });
    expect(fixture.nativeElement.querySelector('[type="submit"]').disabled).toBe(true);
  });

  it('confirms success, resets all fields and allows another creation without inventing a list', () => {
    finishLoad();
    fixture.componentInstance.form.setValue(input);
    fixture.componentInstance.create();
    creation.next({ kwestieId: 'technical-created-id' });
    creation.complete();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role="status"]').textContent).toContain('Kwestie creada correctamente');
    expect(fixture.componentInstance.form.getRawValue()).toEqual({ title: '', description: '', priority: 2 });
    expect(fixture.nativeElement.textContent).not.toContain('technical-created-id');
    expect(fixture.nativeElement.textContent).not.toContain('Printer broken');
    expect(fixture.nativeElement.querySelector('ul, .kwestie-card')).toBeNull();
    expect(workspaces.get).toHaveBeenCalledOnce();
    creation = new Subject<CreateKwestieResponse>();
    fixture.componentInstance.form.controls.title.setValue('Second issue');
    fixture.componentInstance.create();
    expect(kwesties.create).toHaveBeenCalledTimes(2);
  });

  it.each([
    [400, 'Los datos no son válidos'],
    [401, 'Tu sesión no permite completar'],
    [403, 'No tienes acceso para crear'],
    [500, 'No pudimos crear'],
    [0, 'No pudimos crear'],
  ])('preserves input and shows a safe creation error for %s', (status, message) => {
    finishLoad();
    fixture.componentInstance.form.setValue(input);
    fixture.componentInstance.create();
    creation.error(new HttpErrorResponse({ status, error: { detail: 'internal exception' } }));
    fixture.detectChanges();
    expect(fixture.componentInstance.form.getRawValue()).toEqual(input);
    expect(fixture.componentInstance.creating()).toBe(false);
    expect(fixture.nativeElement.querySelector('[role="alert"]').textContent).toContain(message);
    expect(fixture.nativeElement.textContent).not.toContain('internal exception');
    expect(fixture.nativeElement.querySelector('[role="status"]')).toBeNull();
  });

  it('cancels obsolete GETs and ignores repeated route IDs when the component is reused', () => {
    const previous = loading;
    loading = new Subject<WorkspaceSummary>();
    params.next(convertToParamMap({ workspaceId: 'next-id' }));
    params.next(convertToParamMap({ workspaceId: 'next-id' }));
    expect(previous.observed).toBe(false);
    expect(workspaces.get).toHaveBeenCalledTimes(2);
    previous.next(selected);
    expect(fixture.componentInstance.workspace()).toBeNull();
    finishLoad({ ...selected, workspaceId: 'next-id', name: 'Next team' });
    expect(fixture.nativeElement.querySelector('h1').textContent).toBe('Next team');
  });

  it('clears old data and cancels a pending POST on route change before allowing creation for the next Workspace', () => {
    finishLoad();
    fixture.componentInstance.form.setValue(input);
    fixture.componentInstance.create();
    const previousCreation = creation;
    loading = new Subject<WorkspaceSummary>();
    params.next(convertToParamMap({ workspaceId: 'next-id' }));
    fixture.detectChanges();
    expect(previousCreation.observed).toBe(false);
    expect(fixture.componentInstance.creating()).toBe(false);
    expect(fixture.componentInstance.form.getRawValue()).toEqual({ title: '', description: '', priority: 2 });
    expect(fixture.nativeElement.querySelector('form')).toBeNull();
    fixture.componentInstance.create();
    expect(kwesties.create).toHaveBeenCalledOnce();
    previousCreation.next({ kwestieId: 'obsolete-id' });
    expect(fixture.componentInstance.created()).toBe(false);
    finishLoad({ ...selected, workspaceId: 'next-id', name: 'Next team' });
    creation = new Subject<CreateKwestieResponse>();
    fixture.componentInstance.form.controls.title.setValue('New issue');
    fixture.componentInstance.create();
    expect(kwesties.create).toHaveBeenLastCalledWith('next-id', { title: 'New issue', description: '', priority: 2 });
  });

  it('clears the previous confirmation on route change and cancels requests on destruction', () => {
    finishLoad();
    fixture.componentInstance.form.setValue(input);
    fixture.componentInstance.create();
    creation.next({ kwestieId: 'created-id' });
    creation.complete();
    loading = new Subject<WorkspaceSummary>();
    params.next(convertToParamMap({ workspaceId: 'next-id' }));
    expect(fixture.componentInstance.created()).toBe(false);
    fixture.destroy();
    expect(loading.observed).toBe(false);
  });
});
