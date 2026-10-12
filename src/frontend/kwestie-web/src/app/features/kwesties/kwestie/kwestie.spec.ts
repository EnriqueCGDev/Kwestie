import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, ParamMap, provideRouter } from '@angular/router';
import { BehaviorSubject, Subject } from 'rxjs';
import { KwestieDetails } from '../kwestie.models';
import { KwestieService } from '../kwestie.service';
import { Kwestie } from './kwestie';

describe('Kwestie', () => {
  let params: BehaviorSubject<ParamMap>;
  let response: Subject<KwestieDetails>;
  let service: { get: ReturnType<typeof vi.fn>; list: ReturnType<typeof vi.fn> };
  let fixture: ComponentFixture<Kwestie>;
  const selected: KwestieDetails = {
    kwestieId: 'selected-issue', title: 'Printer broken', description: 'Cannot print\nNeeds inspection',
    status: 1, priority: 2, createdAt: '2026-10-09T12:00:00Z', updatedAt: '2026-10-09T13:00:00Z',
    resolvedAt: null, closedAt: null,
  };

  beforeEach(() => {
    params = new BehaviorSubject(convertToParamMap({ workspaceId: 'selected-workspace', kwestieId: 'selected-issue' }));
    response = new Subject<KwestieDetails>();
    service = { get: vi.fn(() => response.asObservable()), list: vi.fn() };
    TestBed.configureTestingModule({ providers: [provideRouter([]),
      { provide: ActivatedRoute, useValue: { paramMap: params.asObservable() } },
      { provide: KwestieService, useValue: service }] });
    fixture = TestBed.createComponent(Kwestie);
    fixture.detectChanges();
  });

  function finishLoad(details = selected) {
    response.next(details);
    response.complete();
    fixture.detectChanges();
  }

  it('loads directly using both route IDs, with one request and a link back to that Workspace', () => {
    expect(service.get).toHaveBeenCalledExactlyOnceWith('selected-workspace', 'selected-issue');
    expect(service.list).not.toHaveBeenCalled();
    expect(fixture.nativeElement.querySelector('[role="status"]').textContent).toContain('Cargando asunto');
    expect(fixture.nativeElement.querySelector('article')).toBeNull();
    expect(fixture.nativeElement.querySelector('header a').getAttribute('href')).toBe('/app/workspaces/selected-workspace');
    fixture.detectChanges();
    params.next(convertToParamMap({ workspaceId: 'selected-workspace', kwestieId: 'selected-issue' }));
    expect(service.get).toHaveBeenCalledOnce();
    finishLoad();
    expect(fixture.componentInstance.details()).toEqual(selected);
    expect(fixture.nativeElement.querySelector('main').getAttribute('aria-busy')).toBe('false');
  });

  it.each([
    [1, 'Abierta', 1, 'Baja'], [2, 'En progreso', 2, 'Normal'],
    [3, 'Resuelta', 3, 'Alta'], [4, 'Cerrada', 4, 'Crítica'],
  ])('translates status %s (%s) and priority %s (%s) into readable labels', (status, state, priority, label) => {
    finishLoad({ ...selected, status, priority });
    expect(fixture.nativeElement.querySelector('.status-label').textContent.trim()).toBe(state);
    expect(fixture.nativeElement.querySelector('.priority-label').textContent.trim()).toBe(label);
  });

  it('shows the returned title, description and four real dates without technical fields or editing actions', () => {
    const details = { ...selected, status: 4,
      resolvedAt: '2026-10-10T14:00:00Z', closedAt: '2026-10-11T15:00:00Z' };
    finishLoad(details);
    expect(fixture.nativeElement.querySelector('h1').textContent).toBe(selected.title);
    expect(fixture.nativeElement.querySelector('.description').textContent).toBe(selected.description);
    const dates: HTMLTimeElement[] = Array.from(fixture.nativeElement.querySelectorAll('time'));
    expect(dates.map(date => date.getAttribute('datetime'))).toEqual([
      details.createdAt, details.updatedAt, details.resolvedAt, details.closedAt,
    ]);
    expect(dates.map(date => date.textContent)).toEqual([
      expect.stringMatching(/09\/10\/2026, \d{2}:\d{2}/), expect.stringMatching(/09\/10\/2026, \d{2}:\d{2}/),
      expect.stringMatching(/10\/10\/2026, \d{2}:\d{2}/), expect.stringMatching(/11\/10\/2026, \d{2}:\d{2}/),
    ]);
    expect(fixture.nativeElement.textContent).not.toContain(selected.kwestieId);
    expect(fixture.nativeElement.textContent).not.toContain('KW-');
    expect(fixture.nativeElement.querySelector('form, button')).toBeNull();
  });

  it('omits an empty description and null resolution/closure dates instead of inventing them', () => {
    finishLoad({ ...selected, description: '' });
    expect(fixture.nativeElement.querySelector('.description')).toBeNull();
    expect(fixture.nativeElement.querySelectorAll('time')).toHaveLength(2);
    const labels: HTMLElement[] = Array.from(fixture.nativeElement.querySelectorAll('dt'));
    expect(labels.map(label => label.textContent)).toEqual(['Estado', 'Prioridad', 'Creación', 'Última actualización']);
  });

  it.each([
    [401, 'Tu sesión no permite acceder'], [404, 'Este asunto no está disponible'],
  ])('shows a safe message for %s without data or an unnecessary retry', (status, message) => {
    response.error(new HttpErrorResponse({ status, error: { detail: 'internal exception' } }));
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role="alert"]').textContent).toContain(message);
    expect(fixture.nativeElement.textContent).not.toContain('internal exception');
    expect(fixture.nativeElement.querySelector('article, button')).toBeNull();
    expect(fixture.componentInstance.loading()).toBe(false);
    fixture.componentInstance.retry();
    expect(service.get).toHaveBeenCalledOnce();
  });

  it.each([0, 500])('handles load failure %s and retries the same IDs without duplicate requests', status => {
    response.error(new HttpErrorResponse({ status, error: { detail: 'internal exception' } }));
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role="alert"]').textContent).toContain('No pudimos cargar');
    expect(fixture.nativeElement.textContent).not.toContain('internal exception');
    response = new Subject<KwestieDetails>();
    fixture.nativeElement.querySelector('button').click();
    fixture.componentInstance.retry();
    fixture.detectChanges();
    expect(service.get).toHaveBeenCalledTimes(2);
    expect(service.get).toHaveBeenLastCalledWith('selected-workspace', 'selected-issue');
    expect(fixture.nativeElement.querySelector('[role="status"]').textContent).toContain('Cargando asunto');
    expect(fixture.nativeElement.querySelector('article')).toBeNull();
    finishLoad();
    expect(fixture.nativeElement.querySelector('[role="alert"]')).toBeNull();
    fixture.componentInstance.retry();
    expect(service.get).toHaveBeenCalledTimes(2);
  });

  it.each([
    ['next-workspace', 'selected-issue'], ['selected-workspace', 'next-issue'],
  ])('cancels a pending request when either route ID changes to %s / %s', (workspaceId, kwestieId) => {
    const previous = response;
    response = new Subject<KwestieDetails>();
    params.next(convertToParamMap({ workspaceId, kwestieId }));
    params.next(convertToParamMap({ workspaceId, kwestieId }));
    expect(previous.observed).toBe(false);
    expect(service.get).toHaveBeenCalledTimes(2);
    expect(service.get).toHaveBeenLastCalledWith(workspaceId, kwestieId);
    previous.next(selected);
    previous.error(new Error('obsolete error'));
    expect(fixture.componentInstance.details()).toBeNull();
    expect(fixture.componentInstance.error()).toBeNull();
    finishLoad({ ...selected, kwestieId, title: 'Next issue' });
    expect(fixture.nativeElement.querySelector('h1').textContent).toBe('Next issue');
    expect(fixture.nativeElement.querySelector('header a').getAttribute('href')).toBe(`/app/workspaces/${workspaceId}`);
  });

  it('clears already displayed data as soon as the route changes, including previous lifecycle dates', () => {
    finishLoad({ ...selected, resolvedAt: '2026-10-10T14:00:00Z', closedAt: '2026-10-11T15:00:00Z' });
    response = new Subject<KwestieDetails>();
    params.next(convertToParamMap({ workspaceId: 'next-workspace', kwestieId: 'next-issue' }));
    fixture.detectChanges();
    expect(fixture.componentInstance.details()).toBeNull();
    expect(fixture.nativeElement.querySelector('article, time')).toBeNull();
    expect(fixture.nativeElement.textContent).not.toContain(selected.title);
    finishLoad({ ...selected, kwestieId: 'next-issue', title: 'Next issue' });
    expect(fixture.nativeElement.querySelectorAll('time')).toHaveLength(2);
    expect(fixture.nativeElement.textContent).not.toContain('Resolución');
  });

  it('cancels a pending request and the route subscription on destruction, ignoring late values', () => {
    expect(response.observed).toBe(true);
    fixture.destroy();
    expect(response.observed).toBe(false);
    expect(params.observed).toBe(false);
    response.next(selected);
    params.next(convertToParamMap({ workspaceId: 'later-workspace', kwestieId: 'later-issue' }));
    expect(fixture.componentInstance.details()).toBeNull();
    expect(service.get).toHaveBeenCalledOnce();
  });
});
