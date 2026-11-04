// Бонус · Remove Guest — сервіс. Запуск: npm run test:hw
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { GroupOrdersService } from './group-orders.service';

describe('Бонус · removeSession$', () => {
  it('DELETE /guest/groups/{id}/sessions/{guestSessionId}', () => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), { provide: API_BASE_URL, useValue: 'https://api.test' }],
    });
    const http = TestBed.inject(HttpTestingController);
    TestBed.inject(GroupOrdersService).removeSession$('grp-1', 'gs-2').subscribe();
    const req = http.expectOne('https://api.test/guest/groups/grp-1/sessions/gs-2');
    expect(req.request.method).toBe('DELETE');
    req.flush({ participants: [], total: 0 });
    http.verify();
  });
});
