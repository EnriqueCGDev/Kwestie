import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Subject } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { AuthenticationResponse, LoginRequest } from '../../../core/auth/auth.models';
import { Login } from './login';

describe('Login', () => {
  let response: Subject<AuthenticationResponse>;
  let auth: { login: ReturnType<typeof vi.fn> };
  const credentials: LoginRequest = { email: 'user@example.com', password: 'password' };
  const session: AuthenticationResponse = {
    userId: 'user-id', accessToken: 'test-access-token',
    accessTokenExpiresAtUtc: '2030-01-01T00:15:00Z',
    refreshTokenExpiresAtUtc: '2030-01-31T00:00:00Z',
  };

  beforeEach(() => {
    response = new Subject<AuthenticationResponse>();
    auth = { login: vi.fn(() => response.asObservable()) };
    TestBed.configureTestingModule({ providers: [provideRouter([]), { provide: AuthService, useValue: auth }] });
  });

  it('rejects invalid fields without calling the service and exposes accessible messages', () => {
    const fixture = TestBed.createComponent(Login);
    fixture.componentInstance.submit();
    fixture.detectChanges();
    expect(auth.login).not.toHaveBeenCalled();
    expect(fixture.nativeElement.querySelector('#login-email-error')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('#login-password-error')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('#login-email').getAttribute('aria-invalid')).toBe('true');

    fixture.componentInstance.form.patchValue({ email: 'invalid', password: 'password' });
    fixture.componentInstance.submit();
    fixture.detectChanges();
    expect(auth.login).not.toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain('Introduce un correo válido.');
  });

  it('sends the correct credentials once while pending, then confirms success', () => {
    const fixture = TestBed.createComponent(Login);
    fixture.componentInstance.form.setValue(credentials);
    fixture.componentInstance.submit();
    fixture.componentInstance.submit();
    fixture.detectChanges();
    expect(auth.login).toHaveBeenCalledExactlyOnceWith(credentials);
    expect(fixture.nativeElement.querySelector('button[type="submit"]').disabled).toBe(true);
    response.next(session);
    response.complete();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Sesión iniciada correctamente.');
    expect(fixture.nativeElement.querySelector('button[type="submit"]').disabled).toBe(false);
  });

  it('shows a generic message for invalid credentials and a simple message for other errors', () => {
    const fixture = TestBed.createComponent(Login);
    fixture.componentInstance.form.setValue(credentials);
    fixture.componentInstance.submit();
    response.error(new HttpErrorResponse({ status: 401, error: { detail: 'private' } }));
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role="alert"]').textContent)
      .toContain('Revisa tu correo y contraseña.');
    expect(fixture.nativeElement.textContent).not.toContain('private');

    response = new Subject<AuthenticationResponse>();
    fixture.componentInstance.submit();
    response.error(new HttpErrorResponse({ status: 503 }));
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role="alert"]').textContent)
      .toContain('Inténtalo de nuevo.');
  });
});
