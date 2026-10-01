import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Subject } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { RegisterRequest, RegisterResponse } from '../../../core/auth/auth.models';
import { Register } from './register';

describe('Register', () => {
  let response: Subject<RegisterResponse>;
  let auth: { register: ReturnType<typeof vi.fn>; login: ReturnType<typeof vi.fn> };
  const credentials: RegisterRequest = { email: 'user@example.com', password: 'password' };

  beforeEach(() => {
    response = new Subject<RegisterResponse>();
    auth = { register: vi.fn(() => response.asObservable()), login: vi.fn() };
    TestBed.configureTestingModule({ providers: [provideRouter([]), { provide: AuthService, useValue: auth }] });
  });

  it('rejects invalid fields without calling the service', () => {
    const fixture = TestBed.createComponent(Register);
    fixture.componentInstance.submit();
    fixture.detectChanges();
    expect(auth.register).not.toHaveBeenCalled();
    expect(fixture.nativeElement.querySelector('#register-email-error')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('#register-password-error')).not.toBeNull();
  });

  it('sends the correct request once while pending and never starts login on success', () => {
    const fixture = TestBed.createComponent(Register);
    fixture.componentInstance.form.setValue(credentials);
    fixture.componentInstance.submit();
    fixture.componentInstance.submit();
    fixture.detectChanges();
    expect(auth.register).toHaveBeenCalledExactlyOnceWith(credentials);
    expect(fixture.nativeElement.querySelector('button[type="submit"]').disabled).toBe(true);
    response.next({ userId: 'new-user-id' });
    response.complete();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Cuenta creada correctamente.');
    expect(fixture.nativeElement.querySelector('a.auth-submit').getAttribute('href')).toBe('/login');
    expect(auth.login).not.toHaveBeenCalled();
    fixture.componentInstance.submit();
    expect(auth.register).toHaveBeenCalledTimes(1);
  });

  it('shows returned API errors and falls back when their format is unexpected', () => {
    const fixture = TestBed.createComponent(Register);
    fixture.componentInstance.form.setValue(credentials);
    fixture.componentInstance.submit();
    response.error(new HttpErrorResponse({ status: 400, error: { errors: ['Email ya registrado.'] } }));
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role="alert"]').textContent).toContain('Email ya registrado.');

    response = new Subject<RegisterResponse>();
    fixture.componentInstance.submit();
    response.error(new HttpErrorResponse({ status: 400, error: { errors: 'unexpected' } }));
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role="alert"]').textContent)
      .toContain('No pudimos crear la cuenta.');
  });
});
