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
import { routes } from './app.routes';

describe('authentication routes', () => {
  const authenticated = signal(false);
  const auth = { isAuthenticated: authenticated, refresh: vi.fn(), logout: vi.fn() };

  beforeEach(() => {
    authenticated.set(false);
    auth.refresh.mockReset();
    TestBed.configureTestingModule({ providers: [provideRouter(routes), { provide: AuthService, useValue: auth },
      { provide: WorkspaceService, useValue: { list: vi.fn(() => of([])) } }] });
  });

  it('redirects an unauthenticated root or protected route to login without refreshing', async () => {
    const harness = await RouterTestingHarness.create();
    expect(await harness.navigateByUrl('/', Login)).toBeInstanceOf(Login);
    expect(TestBed.inject(Router).url).toBe('/login');
    expect(await harness.navigateByUrl('/app', Login)).toBeInstanceOf(Login);
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
