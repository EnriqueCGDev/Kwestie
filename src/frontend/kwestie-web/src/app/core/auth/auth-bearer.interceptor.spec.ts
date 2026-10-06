import { HttpClient, HttpHeaders, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AuthService } from './auth.service';
import { authBearerInterceptor } from './auth-bearer.interceptor';
import { AuthenticationResponse } from './auth.models';

describe('authBearerInterceptor', () => {
  let client: HttpClient;
  let http: HttpTestingController;
  let auth: AuthService;
  const session: AuthenticationResponse = {
    userId: 'user-id', accessToken: 'test-access-token',
    accessTokenExpiresAtUtc: '2030-01-01T00:15:00Z',
    refreshTokenExpiresAtUtc: '2030-01-31T00:00:00Z',
  };

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [
      provideHttpClient(withInterceptors([authBearerInterceptor])),
      provideHttpClientTesting(),
    ] });
    client = TestBed.inject(HttpClient);
    http = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthService);
  });

  afterEach(() => http.verify());

  function signIn(): void {
    auth.login({ email: 'user@example.com', password: 'password' }).subscribe();
    const request = http.expectOne('/api/auth/login');
    expect(request.request.headers.has('Authorization')).toBe(false);
    request.flush(session);
  }

  it('passes an API request unchanged without a session', () => {
    client.get('/api/kwesties').subscribe();
    const request = http.expectOne('/api/kwesties');
    expect(request.request.headers.has('Authorization')).toBe(false);
    request.flush([]);
  });

  it('adds the current access token while preserving other headers', () => {
    signIn();
    client.get('/api/kwesties', { headers: new HttpHeaders({ 'X-Request-Id': 'abc' }) }).subscribe();
    const request = http.expectOne('/api/kwesties');
    expect(request.request.headers.get('Authorization')).toBe('Bearer test-access-token');
    expect(request.request.headers.get('X-Request-Id')).toBe('abc');
    request.flush([]);
  });

  it('never adds Bearer to authentication endpoints', () => {
    signIn();
    for (const operation of ['register', 'login', 'refresh', 'logout']) {
      client.post(`/api/auth/${operation}`, null).subscribe();
      const request = http.expectOne(`/api/auth/${operation}`);
      expect(request.request.headers.has('Authorization')).toBe(false);
      request.flush(null);
    }
  });

  it('does not add Bearer to external or non-API URLs', () => {
    signIn();
    for (const url of ['https://example.com/api/kwesties', '/assets/logo.svg', '/apiary']) {
      client.get(url).subscribe();
      const request = http.expectOne(url);
      expect(request.request.headers.has('Authorization')).toBe(false);
      request.flush(null);
    }
  });

  it('does not refresh or retry after a normal API 401', () => {
    signIn();
    const error = vi.fn();
    client.get('/api/kwesties').subscribe({ error });
    const request = http.expectOne('/api/kwesties');
    expect(request.request.headers.get('Authorization')).toBe('Bearer test-access-token');
    request.flush(null, { status: 401, statusText: 'Unauthorized' });
    expect(error).toHaveBeenCalledOnce();
    http.expectNone('/api/auth/refresh');
    http.expectNone('/api/kwesties');
    expect(auth.session()).toEqual(session);
  });
});
