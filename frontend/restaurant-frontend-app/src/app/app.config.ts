import { ApplicationConfig, isDevMode, provideAppInitializer, provideZoneChangeDetection } from '@angular/core';
import { provideRouter } from '@angular/router';

import { routes } from './app.routes';
import { provideHttpClient, withInterceptors, withXhr } from '@angular/common/http';
import { tokenInterceptor } from './auth/token.interceptor';
import { groupOrdersMockInterceptor } from './shared/mocks/group-orders/group-orders-mock.interceptor';
import { applyDemoLoginFromUrl } from './shared/mocks/auth/demo-auth';
import { provideDaterangepickerLocale } from 'ngx-daterangepicker-bootstrap';

import { environment } from '../environments/environment';
import { API_BASE_URL } from './shared/config/api-base-url.token';

export const appConfig: ApplicationConfig = {
  providers: [
    // DEV-ONLY: ?demo у будь-якій адресі — мок-логін як Owner демо-бронювання (домашка, рівні 1–2).
    provideAppInitializer(() => {
      if (isDevMode()) applyDemoLoginFromUrl();
    }),
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes),
    provideHttpClient(
      withXhr(),
      // DEV-ONLY: мок групового pre-order для demo-reservation (домашка). У production не реєструється.
      withInterceptors([tokenInterceptor, ...(isDevMode() ? [groupOrdersMockInterceptor] : [])]),
    ),
    provideDaterangepickerLocale({
      separator: ' - ',
      applyLabel: 'Apply',
      cancelLabel: 'Cancel',
      format: 'MMM D, YYYY',
    }),
    { provide: API_BASE_URL, useValue: environment.backendUrl },
  ],
};
