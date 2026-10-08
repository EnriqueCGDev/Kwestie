import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'app' },
  {
    path: 'app',
    title: 'Workspaces · Kwestie',
    canActivate: [authGuard],
    loadComponent: () => import('./features/workspaces/workspaces').then(module => module.Workspaces),
  },
  {
    path: 'login',
    title: 'Iniciar sesión · Kwestie',
    loadComponent: () => import('./features/auth/login/login').then(module => module.Login),
  },
  {
    path: 'register',
    title: 'Crear cuenta · Kwestie',
    loadComponent: () => import('./features/auth/register/register').then(module => module.Register),
  },
  { path: '**', redirectTo: 'app' },
];
