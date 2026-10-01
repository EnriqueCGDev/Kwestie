import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { AuthService } from './core/auth/auth.service';
import { Login } from './features/auth/login/login';
import { Register } from './features/auth/register/register';
import { routes } from './app.routes';

describe('authentication routes', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideRouter(routes), { provide: AuthService, useValue: {} }] });
  });

  it('loads the login component and redirects the root there', async () => {
    const harness = await RouterTestingHarness.create();
    expect(await harness.navigateByUrl('/', Login)).toBeInstanceOf(Login);
    expect(TestBed.inject(Router).url).toBe('/login');
  });

  it('loads register and redirects unknown routes to login', async () => {
    const harness = await RouterTestingHarness.create();
    expect(await harness.navigateByUrl('/register', Register)).toBeInstanceOf(Register);
    expect(await harness.navigateByUrl('/missing', Login)).toBeInstanceOf(Login);
    expect(TestBed.inject(Router).url).toBe('/login');
  });
});
