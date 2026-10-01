import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'login' },
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
  { path: '**', redirectTo: 'login' },
];
