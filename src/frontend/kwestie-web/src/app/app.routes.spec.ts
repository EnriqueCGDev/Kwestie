import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { AuthService } from './core/auth/auth.service';
import { Login } from './features/auth/login/login';
import { Register } from './features/auth/register/register';
import { of } from 'rxjs';
import { Workspaces } from './features/workspaces/workspaces';
import { WorkspaceService } from './features/workspaces/workspace.service';
import { Workspace } from './features/workspaces/workspace/workspace';
import { KwestieService } from './features/kwesties/kwestie.service';
import { routes } from './app.routes';

describe('authentication routes', () => {
  const authenticated = signal(false);
  const auth = { isAuthenticated: authenticated, refresh: vi.fn(), logout: vi.fn() };

  beforeEach(() => {
    authenticated.set(false);
    auth.refresh.mockReset();
    TestBed.configureTestingModule({ providers: [provideRouter(routes), { provide: AuthService, useValue: auth },
      { provide: WorkspaceService, useValue: { list: vi.fn(() => of([])), get: vi.fn((workspaceId: string) => of({
        workspaceId, name: 'Direct workspace', createdAt: '2026-10-08T12:00:00Z',
      })) } }, { provide: KwestieService, useValue: { create: vi.fn(), list: vi.fn(() => of([])) } }] });
  });

  it('redirects an unauthenticated root or protected route to login without refreshing', async () => {
    const harness = await RouterTestingHarness.create();
    expect(await harness.navigateByUrl('/', Login)).toBeInstanceOf(Login);
    expect(TestBed.inject(Router).url).toBe('/login');
    expect(await harness.navigateByUrl('/app', Login)).toBeInstanceOf(Login);
    expect(await harness.navigateByUrl('/app/workspaces/selected-id', Login)).toBeInstanceOf(Login);
    expect(auth.refresh).not.toHaveBeenCalled();
  });

  it('allows direct entry and recreation at an individual Workspace without loading the list', async () => {
    authenticated.set(true);
    const service = TestBed.inject(WorkspaceService);
    const harness = await RouterTestingHarness.create();
    const first = await harness.navigateByUrl('/app/workspaces/direct-id', Workspace);
    expect(first.workspace()?.name).toBe('Direct workspace');
    expect(service.get).toHaveBeenCalledWith('direct-id');
    expect(service.list).not.toHaveBeenCalled();
    expect(harness.routeNativeElement?.querySelector('header a')?.getAttribute('href')).toBe('/app');
    // Recreate the route component as on a reload; it obtains its own data again.
    await harness.navigateByUrl('/login', Login);
    const second = await harness.navigateByUrl('/app/workspaces/direct-id', Workspace);
    expect(second).not.toBe(first);
    expect(second.workspace()?.workspaceId).toBe('direct-id');
    expect(service.get).toHaveBeenCalledTimes(2);
    expect(service.list).not.toHaveBeenCalled();
    expect(auth.refresh).not.toHaveBeenCalled();
  });

  it('allows a restored session through root to the protected app route', async () => {
    authenticated.set(true);
    const harness = await RouterTestingHarness.create();
    expect(await harness.navigateByUrl('/', Workspaces)).toBeInstanceOf(Workspaces);
    expect(TestBed.inject(Router).url).toBe('/app');
    expect(auth.refresh).not.toHaveBeenCalled();
  });

  it('keeps login and register public and routes unknown paths through the guard', async () => {
    const harness = await RouterTestingHarness.create();
    expect(await harness.navigateByUrl('/login', Login)).toBeInstanceOf(Login);
    expect(await harness.navigateByUrl('/register', Register)).toBeInstanceOf(Register);
    expect(await harness.navigateByUrl('/missing', Login)).toBeInstanceOf(Login);
    expect(TestBed.inject(Router).url).toBe('/login');
    expect(auth.refresh).not.toHaveBeenCalled();
  });
});
