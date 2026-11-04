# Практичне завдання — Frontend (~15 хв)

**Що зробимо:** картку учасника групового pre-order і сторінку, яка показує всіх учасників. Це перша цеглинка домашки: на рівні 1 ти лише доведеш цю сторінку до макета.

**Дані вже є в архіві:** `src/app/shared/mocks/group-orders/group-order.preview.ts` → `GROUP_ORDER_PREVIEW.sessions.participants`. Це 4 учасники: Owner і 3 гості з кошиками.

Усі шляхи нижче — від `frontend/restaurant-frontend-app/src/app/`.

---

## Крок 0 · Тести (1 хв)

Тести вже написані — вони кажуть, що має вміти картка. З папки `frontend/restaurant-frontend-app`:

```bash
cp -R homework-tests/practice/. src/app/          # Windows: Copy-Item -Recurse -Force homework-tests\practice\* src\app\
npm run test:hw
```

Тест не скомпілюється — компонента ще немає. Це нормально. Після кожного кроку запускай `npm run test:hw` і дивись, як зеленіють групи «крок 1», «крок 3», «крок 4».

## Крок 1 · Компонент картки (3 хв)

Створи `components/group-orders/participant-card/participant-card.component.ts`:

```ts
import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { Participant } from '@app/models/group-orders/participant.model';

@Component({
  selector: 'app-participant-card',
  templateUrl: './participant-card.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class ParticipantCardComponent {
  // що картка отримує ззовні — як параметр функції
  readonly participant = input.required<Participant>();

  // рахується саме, коли змінюється participant
  readonly title = computed(() => {
    const person = this.participant();
    return person.type === 'Owner' ? `Owner: ${person.name}` : `Guest: ${person.name}`;
  });
}
```

і шаблон `participant-card.component.html`:

```html
<article class="card">
  <h2 class="title">{{ title() }}</h2>
</article>
```

## Крок 2 · Сторінка і маршрут (3 хв)

`pages/group-order-page/group-order-page.component.ts`:

```ts
import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { GROUP_ORDER_PREVIEW } from '@app/shared/mocks/group-orders/group-order.preview';
import { ParticipantCardComponent } from '@app/components/group-orders/participant-card/participant-card.component';

@Component({
  selector: 'app-group-order-page',
  imports: [ParticipantCardComponent],
  templateUrl: './group-order-page.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class GroupOrderPageComponent {
  // поки що статичні дані; на рівні 2 їх замінить відповідь API
  readonly participants = signal(GROUP_ORDER_PREVIEW.sessions.participants);
}
```

`group-order-page.component.html`:

```html
<main class="page">
  <h1>Group Pre-Order</h1>

  <ul class="participants">
    @for (person of participants(); track person.id) {
      <li><app-participant-card [participant]="person" /></li>
    }
  </ul>
</main>
```

В `app.routes.ts`, у `children` головного layout (поруч із `join/:token`):

```ts
{
  path: 'reservations/:reservationId/group-order',
  canActivate: [AuthGuard],
  data: { authorized: true },
  loadComponent: () =>
    import('./pages/group-order-page/group-order-page.component').then((m) => m.GroupOrderPageComponent),
},
```

Відкрий `http://localhost:4200/reservations/demo-reservation/group-order?demo` (`?demo` — мок-логін, акаунт не потрібен): чотири заголовки «Owner: Yevhenii», «Guest: Kateryna»…

## Крок 3 · Страви і Subtotal (4 хв)

У картку додай `imports: [CurrencyPipe]` (з `@angular/common`) і розшир шаблон:

```html
<article class="card">
  <h2 class="title">{{ title() }}</h2>

  @if (participant().items.length) {
    <ul class="items">
      @for (item of participant().items; track item.dish.id) {
        <li class="item">
          @if (item.dish.imageUrl) {
            <img class="avatar" [src]="item.dish.imageUrl" alt="" width="40" height="40" loading="lazy" />
          } @else {
            <span class="avatar initial" aria-hidden="true">{{ item.dish.name.charAt(0) }}</span>
          }
          <p class="name">{{ item.dish.name }}</p>
          <p class="qty">× {{ item.quantity }}</p>
          <p class="price">{{ item.dish.price * item.quantity | currency }}</p>
        </li>
      }
    </ul>
  } @else {
    <p class="empty">No dishes yet</p>
  }

  <p class="subtotal"><span>Subtotal</span><span>{{ participant().total | currency }}</span></p>
</article>
```

`width="40" height="40"` — обов'язково: фото страв великі (до кількох МБ і тисяч пікселів), а стилів на практичному ще немає. Атрибути одразу задають розмір і резервують місце, тож сторінка не «стрибає» під час завантаження (CLS). `loading="lazy"` — фото вантажаться, лише коли з'являються на екрані.

## Крок 4 · Статус текстом (2 хв)

Під `<h2>` — статус кошика. Саме **текстом**, а не лише кольором:

```html
@if (participant().isSubmitted) {
  <span class="chip submitted">Submitted</span>
} @else {
  <span class="chip pending">Still choosing</span>
}
```

## Крок 5 · Усі тести зелені (2 хв)

```bash
npm run test:hw
```

Очікувано: **8 тестів картки зелені** (плюс тести `shared/helpers`, які вже були в архіві).

Відкрий `src/app/components/group-orders/participant-card/participant-card.component.spec.ts` і подивись, як він влаштований:
- `TestBed.createComponent(ParticipantCardComponent)` — створює картку окремо від сторінки;
- `fixture.componentRef.setInput('participant', OLENA)` — так у тесті передаємо input;
- перевіряємо те, що бачить користувач: текст заголовка, ціни, статус, розмір фото.

## Готово, якщо

- [ ] на сторінці 4 картки зі стравами, цінами і Subtotal;
- [ ] статус видно текстом;
- [ ] `npm run test:hw` — 8 тестів картки зелені.

Стилі на практичному не потрібні — це вже рівень 1 домашки.
