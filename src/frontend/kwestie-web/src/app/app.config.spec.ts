import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApplicationInitStatus } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { appConfig } from './app.config';
import { AuthService } from './core/auth/auth.service';

describe('application session restoration', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [...appConfig.providers, provideHttpClientTesting()] });
  });

  it('makes one cookie-backed refresh attempt before startup completes', async () => {
    const status = TestBed.inject(ApplicationInitStatus);
    const http = TestBed.inject(HttpTestingController);
    const request = http.expectOne('/api/auth/refresh');
    expect(request.request.withCredentials).toBe(true);
    expect(status.done).toBe(false);
    request.flush({
      userId: 'user-id', accessToken: 'test-access-token',
      accessTokenExpiresAtUtc: '2030-01-01T00:15:00Z',
      refreshTokenExpiresAtUtc: '2030-01-31T00:00:00Z',
    });
    await status.donePromise;
    expect(TestBed.inject(AuthService).session()?.userId).toBe('user-id');
    http.verify();
  });

  it('finishes startup without a session after a failed refresh', async () => {
    const status = TestBed.inject(ApplicationInitStatus);
    const http = TestBed.inject(HttpTestingController);
    http.expectOne('/api/auth/refresh').flush(null, { status: 401, statusText: 'Unauthorized' });
    await expect(status.donePromise).resolves.toBeUndefined();
    expect(TestBed.inject(AuthService).session()).toBeNull();
    http.verify();
  });
});
