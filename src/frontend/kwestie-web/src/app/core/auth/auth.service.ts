import { HttpClient } from '@angular/common/http';
import { computed, inject, Service, signal } from '@angular/core';
import { tap } from 'rxjs';
import { AuthenticationResponse, LoginRequest, RegisterRequest, RegisterResponse } from './auth.models';

@Service()
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly currentSession = signal<AuthenticationResponse | null>(null);

  readonly session = this.currentSession.asReadonly();
  // Presence of a successful session, not a timer or a guarantee that its JWT is still valid.
  readonly isAuthenticated = computed(() => this.session() !== null);

  register(request: RegisterRequest) {
    return this.http.post<RegisterResponse>('/api/auth/register', request);
  }

  login(request: LoginRequest) {
    return this.http.post<AuthenticationResponse>('/api/auth/login', request, { withCredentials: true })
      .pipe(tap(session => this.storeSession(session)));
  }

  refresh() {
    return this.http.post<AuthenticationResponse>('/api/auth/refresh', null, { withCredentials: true })
      .pipe(tap(session => this.storeSession(session)));
  }

  logout() {
    return this.http.post<void>('/api/auth/logout', null, { withCredentials: true })
      .pipe(tap(() => this.currentSession.set(null)));
  }

  private storeSession(session: AuthenticationResponse): void {
    this.currentSession.set({
      userId: session.userId,
      accessToken: session.accessToken,
      accessTokenExpiresAtUtc: session.accessTokenExpiresAtUtc,
      refreshTokenExpiresAtUtc: session.refreshTokenExpiresAtUtc,
    });
  }
}
