// Рівень 3 · тихі запити для фонової синхронізації: { silent: true } → SKIP_ERROR_TOAST.
// Запуск: npm run test:hw
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { SKIP_ERROR_TOAST } from '@app/auth/skip-error-toast.context';
import { GroupOrdersService } from './group-orders.service';

const BASE_URL = 'https://api.test';

describe('Рівень 3 · тихі запити сервісу', () => {
  let service: GroupOrdersService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), { provide: API_BASE_URL, useValue: BASE_URL }],
    });
    service = TestBed.inject(GroupOrdersService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('getSessions$(id, { silent: true }) не показує toast на помилку', () => {
    service.getSessions$('grp-1', { silent: true }).subscribe();
    const req = http.expectOne(`${BASE_URL}/guest/groups/grp-1/sessions`);
    expect(req.request.context.get(SKIP_ERROR_TOAST)).toBeTrue();
    req.flush({ participants: [], total: 0 });
  });

  it('getStatus$(id, { silent: true }) не показує toast на помилку', () => {
    service.getStatus$('grp-1', { silent: true }).subscribe();
    const req = http.expectOne(`${BASE_URL}/guest/groups/grp-1/status`);
    expect(req.request.context.get(SKIP_ERROR_TOAST)).toBeTrue();
    req.flush({ currentParticipants: 3, maxCapacity: 5, status: 'Open' });
  });

  it('звичайний запит (без silent) показує toast як завжди', () => {
    service.getSessions$('grp-1').subscribe();
    const req = http.expectOne(`${BASE_URL}/guest/groups/grp-1/sessions`);
    expect(req.request.context.get(SKIP_ERROR_TOAST)).toBeFalse();
    req.flush({ participants: [], total: 0 });
  });
});
