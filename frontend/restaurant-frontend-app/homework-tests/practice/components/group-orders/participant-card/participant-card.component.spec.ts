// Практичне · картка учасника. Тести готові — пиши код, поки всі не стануть зеленими.
// Запуск: npm run test:hw
import { TestBed } from '@angular/core/testing';
import { Participant } from '@app/models/group-orders/participant.model';
import { ParticipantCardComponent } from './participant-card.component';

const OLENA: Participant = {
  id: 'olena',
  type: 'Guest',
  name: 'Olena',
  isSubmitted: false,
  total: 440,
  items: [{ dish: { id: 'dish2', name: 'Tom Yum', price: 220, imageUrl: 'assets/img/dishes/spring-salad.jpg' }, quantity: 2 }],
};
const OWNER: Participant = { id: 'owner', type: 'Owner', name: 'Yevhenii', isSubmitted: true, total: 0, items: [] };

function render(participant: Participant): HTMLElement {
  const fixture = TestBed.createComponent(ParticipantCardComponent);
  fixture.componentRef.setInput('participant', participant); // так у тесті передаємо input
  fixture.detectChanges();
  return fixture.nativeElement as HTMLElement;
}

describe('Практичне · картка учасника', () => {
  describe('крок 1 · заголовок картки', () => {
    it('гість: <h2> «Guest: Olena»', () => {
      expect(render(OLENA).querySelector('h2')?.textContent).toContain('Guest: Olena');
    });

    it('власник: <h2> «Owner: Yevhenii»', () => {
      expect(render(OWNER).querySelector('h2')?.textContent).toContain('Owner: Yevhenii');
    });
  });

  describe('крок 3 · страви і Subtotal', () => {
    it('показує назву страви, кількість і ціну × кількість через currency', () => {
      const text = render(OLENA).textContent ?? '';
      expect(text).toContain('Tom Yum');
      expect(text).toContain('2');
      expect(text).toContain('$440.00');
    });

    it('показує Subtotal учасника', () => {
      expect(render(OLENA).textContent).toContain('Subtotal');
    });

    it('порожній кошик: «No dishes yet»', () => {
      expect(render(OWNER).textContent).toContain('No dishes yet');
    });

    it('фото страви має розмір 40×40', () => {
      const img = render(OLENA).querySelector('img');
      expect(img?.getAttribute('width')).toBe('40');
      expect(img?.getAttribute('height')).toBe('40');
    });
  });

  describe('крок 4 · статус кошика текстом', () => {
    it('«Still choosing», якщо кошик не підтверджений', () => {
      expect(render(OLENA).textContent).toContain('Still choosing');
    });

    it('«Submitted», якщо підтверджений', () => {
      expect(render({ ...OLENA, isSubmitted: true }).textContent).toContain('Submitted');
    });
  });
});
