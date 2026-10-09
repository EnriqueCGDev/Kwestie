import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, ParamMap, provideRouter } from '@angular/router';
import { BehaviorSubject, Subject } from 'rxjs';
import { CreateKwestieResponse, KwestieSummary } from '../../kwesties/kwestie.models';
import { KwestieService } from '../../kwesties/kwestie.service';
import { WorkspaceSummary } from '../workspace.models';
import { WorkspaceService } from '../workspace.service';
import { Workspace } from './workspace';

describe('Workspace', () => {
  let params: BehaviorSubject<ParamMap>;
  let loading: Subject<WorkspaceSummary>;
  let creation: Subject<CreateKwestieResponse>;
  let listing: Subject<KwestieSummary[]>;
  let workspaces: { get: ReturnType<typeof vi.fn>; list: ReturnType<typeof vi.fn> };
  let kwesties: { create: ReturnType<typeof vi.fn>; list: ReturnType<typeof vi.fn> };
  let fixture: ComponentFixture<Workspace>;
  const selected = { workspaceId: 'selected-id', name: 'Support team', createdAt: '2026-10-08T12:00:00Z' };
  const input = { title: 'Printer broken', description: 'Cannot print', priority: 4 };
  const persisted = { kwestieId: 'persisted-id', title: 'Server title', description: 'Server description',
    status: 1, priority: 2, createdAt: '2026-10-09T12:00:00Z' };

  beforeEach(() => {
    params = new BehaviorSubject(convertToParamMap({ workspaceId: 'selected-id' }));
    loading = new Subject<WorkspaceSummary>();
    creation = new Subject<CreateKwestieResponse>();
    listing = new Subject<KwestieSummary[]>();
    workspaces = { get: vi.fn(() => loading.asObservable()), list: vi.fn() };
    kwesties = { create: vi.fn(() => creation.asObservable()), list: vi.fn(() => listing.asObservable()) };
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

  function finishListing(summaries: KwestieSummary[] = []) {
    listing.next(summaries);
    listing.complete();
    fixture.detectChanges();
  }

  it('loads directly from the route using only the individual GET and shows the real name and back link', () => {
    expect(workspaces.get).toHaveBeenCalledExactlyOnceWith('selected-id');
    expect(workspaces.list).not.toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain('Cargando Workspace');
    expect(fixture.nativeElement.querySelector('form')).toBeNull();
    expect(kwesties.list).not.toHaveBeenCalled();
    finishLoad();
    expect(fixture.nativeElement.querySelector('h1').textContent).toBe('Support team');
    expect(fixture.nativeElement.querySelector('header a').getAttribute('href')).toBe('/app');
    expect(fixture.componentInstance.form.getRawValue()).toEqual({ title: '', description: '', priority: 2 });
    expect(kwesties.list).toHaveBeenCalledExactlyOnceWith('selected-id');
    expect(fixture.nativeElement.querySelector('.kwestie-list [role="status"]').textContent).toContain('Cargando asuntos');
    fixture.detectChanges();
    params.next(convertToParamMap({ workspaceId: 'selected-id' }));
    expect(kwesties.list).toHaveBeenCalledOnce();
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
    expect(kwesties.list).not.toHaveBeenCalled();
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

  it('confirms success, resets fields and reloads persisted summaries without inventing a list from the POST ID', () => {
    finishLoad();
    fixture.componentInstance.form.setValue(input);
    fixture.componentInstance.create();
    const pendingInitialList = listing;
    listing = new Subject<KwestieSummary[]>();
    creation.next({ kwestieId: 'technical-created-id' });
    creation.complete();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role="status"]').textContent).toContain('Kwestie creada correctamente');
    expect(fixture.componentInstance.form.getRawValue()).toEqual({ title: '', description: '', priority: 2 });
    expect(fixture.nativeElement.textContent).not.toContain('technical-created-id');
    expect(fixture.nativeElement.textContent).not.toContain('Printer broken');
    expect(fixture.nativeElement.querySelector('ul, .kwestie-card')).toBeNull();
    expect(workspaces.get).toHaveBeenCalledOnce();
    expect(kwesties.list).toHaveBeenCalledTimes(2);
    expect(pendingInitialList.observed).toBe(false);
    pendingInitialList.next([{ ...persisted, title: 'Obsolete title' }]);
    expect(fixture.componentInstance.kwestieList()).toEqual([]);
    finishListing([persisted]);
    expect(fixture.nativeElement.querySelector('.kwestie-card h3').textContent).toBe('Server title');
    expect(fixture.nativeElement.textContent).not.toContain('Obsolete title');
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
    expect(fixture.nativeElement.querySelector('.create-panel [role="status"]')).toBeNull();
    expect(kwesties.list).toHaveBeenCalledOnce();
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
    const previousListing = listing;
    loading = new Subject<WorkspaceSummary>();
    listing = new Subject<KwestieSummary[]>();
    params.next(convertToParamMap({ workspaceId: 'next-id' }));
    fixture.detectChanges();
    expect(previousCreation.observed).toBe(false);
    expect(previousListing.observed).toBe(false);
    expect(fixture.componentInstance.creating()).toBe(false);
    expect(fixture.componentInstance.form.getRawValue()).toEqual({ title: '', description: '', priority: 2 });
    expect(fixture.nativeElement.querySelector('form')).toBeNull();
    fixture.componentInstance.create();
    expect(kwesties.create).toHaveBeenCalledOnce();
    previousCreation.next({ kwestieId: 'obsolete-id' });
    expect(fixture.componentInstance.created()).toBe(false);
    previousListing.next([{ ...persisted, title: 'Old workspace issue' }]);
    expect(fixture.componentInstance.kwestieList()).toEqual([]);
    finishLoad({ ...selected, workspaceId: 'next-id', name: 'Next team' });
    expect(kwesties.list).toHaveBeenLastCalledWith('next-id');
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

  it('renders persisted data in server order with all four readable status/priority labels and Spanish dates', () => {
    finishLoad();
    const values = [
      { ...persisted, kwestieId: 'id-four', status: 4, priority: 4, title: 'Closed title', description: '' },
      { ...persisted, kwestieId: 'id-one', status: 1, priority: 1, title: 'Open title' },
      { ...persisted, kwestieId: 'id-three', status: 3, priority: 3, title: 'Resolved title' },
      { ...persisted, kwestieId: 'id-two', status: 2, priority: 2, title: 'In progress title' },
    ];
    finishListing(values);
    const cards: HTMLElement[] = Array.from(fixture.nativeElement.querySelectorAll('.kwestie-card'));
    expect(cards.map(card => card.querySelector('h3')?.textContent)).toEqual(values.map(value => value.title));
    expect(cards.map(card => card.querySelector('.status-label')?.textContent?.trim())).toEqual([
      'Estado: Cerrada', 'Estado: Abierta', 'Estado: Resuelta', 'Estado: En progreso',
    ]);
    expect(cards.map(card => card.querySelector('.priority-label')?.textContent?.trim())).toEqual([
      'Prioridad: Crítica', 'Prioridad: Baja', 'Prioridad: Alta', 'Prioridad: Normal',
    ]);
    expect(cards[0].querySelector('.description')).toBeNull();
    expect(cards[1].querySelector('.description')?.textContent).toContain('Server description');
    for (const card of cards) {
      expect(card.querySelector('time')?.textContent).toMatch(/09\/10\/2026, \d{2}:\d{2}/);
      expect(card.querySelector('time')?.getAttribute('datetime')).toBe(persisted.createdAt);
      expect(card.querySelector('a')).toBeNull();
    }
    expect(fixture.nativeElement.textContent).not.toContain('id-four');
    expect(fixture.nativeElement.textContent).not.toContain('KW-');
    expect(kwesties.list).toHaveBeenCalledOnce();
  });

  it('shows the empty state only after a successful empty response and keeps creation available', () => {
    finishLoad();
    expect(fixture.nativeElement.querySelector('.empty-state')).toBeNull();
    finishListing();
    expect(fixture.nativeElement.querySelector('.empty-state').textContent).toContain('Todavía no existen asuntos');
    expect(fixture.nativeElement.querySelector('.kwestie-list').getAttribute('aria-busy')).toBe('false');
    expect(fixture.nativeElement.querySelector('form')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('ul')).toBeNull();
  });

  it.each([0, 401, 404, 500])('shows a safe list error for %s and retries only the list without duplicate requests', status => {
    finishLoad();
    listing.error(new HttpErrorResponse({ status, error: { detail: 'internal exception' } }));
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.kwestie-list [role="alert"]').textContent).toContain('No pudimos cargar los asuntos');
    expect(fixture.nativeElement.textContent).not.toContain('internal exception');
    expect(fixture.nativeElement.querySelector('.empty-state')).toBeNull();
    expect(fixture.nativeElement.querySelector('form')).not.toBeNull();
    listing = new Subject<KwestieSummary[]>();
    fixture.nativeElement.querySelector('.kwestie-list button').click();
    fixture.componentInstance.retryKwesties();
    expect(kwesties.list).toHaveBeenCalledTimes(2);
    expect(workspaces.get).toHaveBeenCalledOnce();
    expect(kwesties.create).not.toHaveBeenCalled();
    finishListing([persisted]);
    expect(fixture.nativeElement.querySelector('.kwestie-list [role="alert"]')).toBeNull();
    expect(fixture.componentInstance.kwestieList()).toEqual([persisted]);
    fixture.componentInstance.retryKwesties();
    expect(kwesties.list).toHaveBeenCalledTimes(2);
  });

  it('keeps creation confirmation and existing data when the reload fails, retrying GET without repeating POST', () => {
    finishLoad();
    finishListing([persisted]);
    fixture.componentInstance.form.setValue(input);
    fixture.componentInstance.create();
    listing = new Subject<KwestieSummary[]>();
    creation.next({ kwestieId: 'created-id' });
    creation.complete();
    listing.error(new HttpErrorResponse({ status: 500, error: 'internal error' }));
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.create-panel [role="status"]').textContent).toContain('Kwestie creada correctamente');
    expect(fixture.nativeElement.querySelector('.kwestie-list [role="alert"]').textContent).toContain('No pudimos cargar los asuntos');
    expect(fixture.componentInstance.kwestieList()).toEqual([persisted]);
    expect(fixture.componentInstance.form.getRawValue()).toEqual({ title: '', description: '', priority: 2 });
    expect(kwesties.create).toHaveBeenCalledOnce();
    listing = new Subject<KwestieSummary[]>();
    fixture.componentInstance.retryKwesties();
    finishListing([persisted, { ...persisted, kwestieId: 'created-id', title: 'Persisted new issue' }]);
    expect(kwesties.create).toHaveBeenCalledOnce();
    expect(kwesties.list).toHaveBeenCalledTimes(3);
    expect(fixture.componentInstance.created()).toBe(true);
  });

  it('clears loaded issues immediately on route change and ignores a pending reload from the previous Workspace', () => {
    finishLoad();
    finishListing([persisted]);
    fixture.componentInstance.form.setValue(input);
    fixture.componentInstance.create();
    const previousListing = new Subject<KwestieSummary[]>();
    listing = previousListing;
    creation.next({ kwestieId: 'created-id' });
    creation.complete();
    expect(previousListing.observed).toBe(true);
    loading = new Subject<WorkspaceSummary>();
    listing = new Subject<KwestieSummary[]>();
    params.next(convertToParamMap({ workspaceId: 'next-id' }));
    fixture.detectChanges();
    expect(previousListing.observed).toBe(false);
    expect(fixture.componentInstance.kwestieList()).toEqual([]);
    expect(fixture.componentInstance.created()).toBe(false);
    expect(fixture.nativeElement.querySelector('.kwestie-card')).toBeNull();
    previousListing.next([{ ...persisted, title: 'Obsolete issue' }]);
    previousListing.error(new Error('obsolete error'));
    expect(fixture.componentInstance.listError()).toBeNull();
    expect(kwesties.list).toHaveBeenCalledTimes(2);
    finishLoad({ ...selected, workspaceId: 'next-id', name: 'Next workspace' });
    finishListing([{ ...persisted, kwestieId: 'next-issue', title: 'Next workspace issue' }]);
    expect(kwesties.list).toHaveBeenCalledTimes(3);
    expect(kwesties.list).toHaveBeenLastCalledWith('next-id');
    expect(fixture.nativeElement.textContent).not.toContain('Server title');
    expect(fixture.nativeElement.textContent).not.toContain('Obsolete issue');
    expect(fixture.nativeElement.textContent).toContain('Next workspace issue');
  });

  it('cancels a pending list and POST when destroyed without accepting late responses', () => {
    finishLoad();
    fixture.componentInstance.form.setValue(input);
    fixture.componentInstance.create();
    expect(listing.observed).toBe(true);
    expect(creation.observed).toBe(true);
    fixture.destroy();
    expect(listing.observed).toBe(false);
    expect(creation.observed).toBe(false);
    expect(params.observed).toBe(false);
    listing.next([persisted]);
    creation.next({ kwestieId: 'late-id' });
    expect(fixture.componentInstance.kwestieList()).toEqual([]);
    expect(fixture.componentInstance.created()).toBe(false);
    expect(kwesties.list).toHaveBeenCalledOnce();
  });
});
