import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Observable } from 'rxjs';
import { AuthService } from './auth.service';
import { AuthenticationResponse } from './auth.models';

describe('AuthService', () => {
  let service: AuthService;
  let http: HttpTestingController;
  const credentials = { email: 'user@example.com', password: 'test-only-password' };
  const session: AuthenticationResponse = {
    userId: 'user-id',
    accessToken: 'test-access-token',
    accessTokenExpiresAtUtc: '2030-01-01T00:15:00Z',
    refreshTokenExpiresAtUtc: '2030-01-31T00:00:00Z',
  };

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function login() {
    service.login(credentials).subscribe();
    http.expectOne('/api/auth/login').flush(session);
  }

  it('starts with no session and does not restore it automatically', () => {
    expect(service.session()).toBeNull();
    expect(service.isAuthenticated()).toBe(false);
  });

  it('registers with the HTTP contract without logging in', () => {
    const received = vi.fn();
    service.register(credentials).subscribe(received);
    const request = http.expectOne('/api/auth/register');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(credentials);
    request.flush({ userId: session.userId }, { status: 201, statusText: 'Created' });
    expect(received).toHaveBeenCalledWith({ userId: session.userId });
    expect(service.session()).toBeNull();
  });

  it('logs in with credentials enabled and stores only the session HTTP fields', () => {
    service.login(credentials).subscribe();
    const request = http.expectOne('/api/auth/login');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(credentials);
    expect(request.request.withCredentials).toBe(true);
    expect(service.session()).toBeNull();
    request.flush(session);
    expect(service.session()).toEqual(session);
    expect(service.isAuthenticated()).toBe(true);
    expect(Object.keys(service.session()!).sort()).toEqual([
      'accessToken', 'accessTokenExpiresAtUtc', 'refreshTokenExpiresAtUtc', 'userId',
    ]);
  });

  it('refreshes without a token body and replaces the session', () => {
    login();
    const renewed = { ...session, accessToken: 'renewed-access-token', refreshTokenExpiresAtUtc: '2030-02-01T00:00:00Z' };
    service.refresh().subscribe();
    const request = http.expectOne('/api/auth/refresh');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toBeNull();
    expect(request.request.withCredentials).toBe(true);
    request.flush(renewed);
    expect(service.session()).toEqual(renewed);
  });

  it('clears the session only after logout succeeds with 204', () => {
    login();
    service.logout().subscribe();
    const request = http.expectOne('/api/auth/logout');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toBeNull();
    expect(request.request.withCredentials).toBe(true);
    expect(service.session()).toEqual(session);
    request.flush(null, { status: 204, statusText: 'No Content' });
    expect(service.session()).toBeNull();
    expect(service.isAuthenticated()).toBe(false);
  });

  it.each(['login', 'refresh', 'logout'] as const)('propagates %s errors without changing the session', operation => {
    login();
    const error = vi.fn();
    const response: Observable<unknown> = operation === 'login' ? service.login(credentials) : service[operation]();
    response.subscribe({ error });
    http.expectOne(`/api/auth/${operation}`).flush(null, { status: 500, statusText: 'Server Error' });
    expect(error).toHaveBeenCalledOnce();
    expect(service.session()).toEqual(session);
  });
});
