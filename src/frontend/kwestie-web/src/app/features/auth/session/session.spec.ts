import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { Subject } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { Session } from './session';

describe('Session', () => {
  let logoutResult: Subject<void>;
  let auth: { logout: ReturnType<typeof vi.fn> };
  let navigate: ReturnType<typeof vi.spyOn>;

  beforeEach(() => {
    logoutResult = new Subject<void>();
    auth = { logout: vi.fn(() => logoutResult.asObservable()) };
    TestBed.configureTestingModule({ providers: [provideRouter([]), { provide: AuthService, useValue: auth }] });
    navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
  });

  it('logs out once and navigates to login after success', () => {
    const fixture = TestBed.createComponent(Session);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Sesión iniciada');

    fixture.componentInstance.logout();
    fixture.componentInstance.logout();
    fixture.detectChanges();
    expect(auth.logout).toHaveBeenCalledOnce();
    expect(fixture.nativeElement.querySelector('button').disabled).toBe(true);
    expect(navigate).not.toHaveBeenCalled();

    logoutResult.next();
    logoutResult.complete();
    expect(navigate).toHaveBeenCalledWith('/login');
  });

  it('stays on the current screen and shows an error when logout fails', () => {
    const fixture = TestBed.createComponent(Session);
    fixture.componentInstance.logout();
    logoutResult.error(new Error('server error'));
    fixture.detectChanges();
    expect(navigate).not.toHaveBeenCalled();
    expect(fixture.nativeElement.querySelector('[role="alert"]').textContent)
      .toContain('No pudimos cerrar sesión.');
    expect(fixture.nativeElement.querySelector('button').disabled).toBe(false);
  });
});
