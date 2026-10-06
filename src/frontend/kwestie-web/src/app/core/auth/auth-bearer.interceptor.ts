import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthService } from './auth.service';

export const authBearerInterceptor: HttpInterceptorFn = (request, next) => {
  if (!request.url.startsWith('/api/') || request.url === '/api/auth' ||
    request.url.startsWith('/api/auth/')) {
    return next(request);
  }

  const accessToken = inject(AuthService).session()?.accessToken;
  return next(accessToken
    ? request.clone({ setHeaders: { Authorization: `Bearer ${accessToken}` } })
    : request);
};
