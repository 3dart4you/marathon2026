// Рівень 1 · верстка сторінки на статичних даних (GROUP_ORDER_PREVIEW).
// Запуск: npm run test:hw
import { TestBed } from '@angular/core/testing';
import { GroupOrderPageComponent } from './group-order-page.component';

describe('Рівень 1 · верстка сторінки', () => {
  let element: HTMLElement;

  beforeEach(() => {
    const fixture = TestBed.createComponent(GroupOrderPageComponent);
    fixture.detectChanges();
    element = fixture.nativeElement as HTMLElement;
  });

  it('один <main> і один <h1> «Group Pre-Order»', () => {
    expect(element.querySelectorAll('main').length).toBe(1);
    expect(element.querySelectorAll('h1').length).toBe(1);
    expect(element.querySelector('h1')?.textContent).toContain('Group Pre-Order');
  });

  it('шапка: адреса, дата і «4 of 5 guests»', () => {
    const text = element.textContent ?? '';
    expect(text).toContain('Rustaveli Avenue 48');
    expect(text).toContain('Oct 21, 2026');
    expect(text).toContain('4 of 5 guests');
  });

  it('учасники — список <ul>/<li> з картками', () => {
    expect(element.querySelectorAll('li app-participant-card').length).toBe(4);
  });

  it('статус кошика видно текстом', () => {
    expect(element.textContent).toContain('Submitted');
    expect(element.textContent).toContain('Still choosing');
  });

  it('Grand Total — сума з даних, $74.50', () => {
    expect(element.textContent).toContain('Grand Total');
    expect(element.textContent).toContain('$74.50');
  });
});
