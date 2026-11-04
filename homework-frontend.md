# Домашнє завдання — Frontend

**Фіча:** Owner групового бронювання бачить, що обрав кожен гість, і підтверджує одне спільне замовлення для ресторану.
Бекенд для неї роблять на бекенд-треку (`homework-backend.md`), фронтенд — ви.

**Дизайн:** PNG-експорт з Figma — у папці `homework-design/` поруч із цим файлом (там же — що з макета входить у домашку, а що ні). Оригінал: [Figma — EPAM IT Marathon 6.0](https://www.figma.com/design/P9rkBNLJZ3WabXSQX9b5DO/EPAM-IT-Marathon-6.0?node-id=62645-20592) → сторінка *FOR DEVELOPERS* → секція **Owner Flow**.

**Тести вже написані:** у `frontend/restaurant-frontend-app/homework-tests/` — окремо для практичного і кожного рівня. На початку рівня копіюєш його тести в `src/app/`, запускаєш `npm run test:hw` — спершу вони червоні. Пишеш код, поки не стануть зеленими. Як скопіювати — у `homework-tests/README.md`.

**Де писати код:** `frontend/restaurant-frontend-app`, за правилами з `frontend/restaurant-frontend-app/CLAUDE.md` (куди класти файл, як називати, стилі).

## Старт

```bash
# розпакуй архів і відкрий папку проєкту
cd frontend/restaurant-frontend-app
npm ci
npm start          # http://localhost:4200
```

В архіві вже є:
- моделі API — `src/app/models/group-orders/`;
- статичні дані для верстки — `src/app/shared/mocks/group-orders/group-order.preview.ts`;
- **мок-бекенд** (працює лише в `npm start`) — `src/app/shared/mocks/group-orders/`. Він відповідає на запити для демо-бронювання `demo-reservation` за правилами з `homework-backend.md`. Решта запитів іде на справжній API;
- **мок-логін** — `src/app/shared/mocks/auth/demo-auth.ts`: додай `?demo` до будь-якої адреси, і ти залогінений як Owner демо-бронювання. Реєстрація й акаунт не потрібні;
- **тести до кожного рівня** — `homework-tests/` і команда `npm run test:hw`;
- готова функція `consolidatePreOrder()` з тестами — `src/app/shared/helpers/consolidate-pre-order.ts`: рахує замовлення для ресторану (знадобиться на рівні 2).

**Рівні 1–2 повністю на моках:** нічого піднімати, реєструвати й логінитись не треба. Відкрий:

```
http://localhost:4200/reservations/demo-reservation/group-order?demo
```

Справжній бекенд потрібен лише на рівні 3.

---

## Практичне завдання (~15 хв)

`practice-frontend.md` — картка учасника і сторінка зі списком учасників, крок за кроком з готовим кодом. **Тести:** `homework-tests/practice/` (8 тестів). Це основа рівня 1: після практичного в тебе вже є маршрут, сторінка і картка.

---

## 1й рівень — верстка екрана Owner Review

Довести сторінку з практичного до макета **на статичних даних** — `homework-design/01-owner-review.png`. Жодних HTTP-запитів — тільки HTML, SCSS і компоненти.

**Тести:** скопіюй `homework-tests/level-1/` в `src/app/` (5 тестів сторінки + 8 картки з практичного).

**Що додати до практичного:**
- шапку «Group Pre-Order»: адреса, дата, час, «4 of 5 guests» — з `GROUP_ORDER_PREVIEW.invite` (поля `LocationAddress`, `StartTime`, `EndTime`, `MaxGuestCount`; дату й час форматуй пайпом `date`);
- Grand Total — `GROUP_ORDER_PREVIEW.sessions.total` через пайп `currency`;
- стилі сторінки і картки за макетом (`.scss` поруч з компонентом, `styleUrl` у `@Component`).

**Умови:**
- семантика: `<main>`, один `<h1>`, учасники — `<ul>`/`<li>`, картка — `<article>`;
- статус кошика передається не лише кольором;
- кольори — з `app/shared/styles/abstracts/colors` (`@use … as color`, не `@import`);
- екран не «ламається» на ширині 375 px (телефон);
- `npm run test:hw` — усі тести практичного і рівня 1 зелені.

Перевір: `http://localhost:4200/reservations/demo-reservation/group-order?demo`.

---

## 2й рівень — груповий pre-order на моках

Уся логіка екрана — на **мок-бекенді** з архіву. Він повторює правила `homework-backend.md`, тож справжній бекенд не потрібен. Працюєш з демо-бронюванням під мок-логіном: `http://localhost:4200/reservations/demo-reservation/group-order?demo`.

**Ендпоінти** (у dev на `demo-reservation` відповідає мок):
- `GET /reservations/{reservationId}/invite-token` → `GET /guest/invites/{token}` — уже є в `GuestInviteService`. З відповіді беремо `GroupReservationId` і дані для шапки;
- **`GET /guest/groups/{groupReservationId}/sessions`** — учасники та їхні кошики;
- **`GET /reservations/{reservationId}/guest-session-id`** — id власної сесії Owner;
- `GET /guest/groups/{groupReservationId}/status` — статус групи (`Open` / `Submitted` / `Closed`);
- **`POST /group-reservations/{groupReservationId}/submit`**, тіло `{ "reservationId": "…" }`.

**Що зробити:**
- Сервіс `src/app/services/group-orders/group-orders.service.ts` з методами під кожен ендпоінт.
- Сторінка бере `reservationId` з URL і завантажує дані замість `GROUP_ORDER_PREVIEW`. Стан — `signal()`, похідні значення — `computed()`.
- Власна картка Owner підписана **My Pre-Order** (`participant.id === guestSessionId`).
- Блок «Consolidated pre-order»: `computed(() => consolidatePreOrder(participants()))` — функція вже є в архіві, просто підключи.
- **Grand Total = сума консолідованого замовлення**, а не `total` з API: у замовлення ресторану йдуть лише підтверджені кошики.
- Жовта плашка, якщо є непідтверджені учасники: «N participants haven't submitted their cart yet — their dishes won't be included in the order».
- Бейдж статусу біля заголовка: Open → «Collecting pre-orders», Submitted → «Sent to Restaurant».
- Кнопка **Submit Pre-Order** → діалог підтвердження через спільний `<app-modal>` → submit. Після успіху: toast «Pre-order sent to the restaurant», бейдж «Sent to Restaurant», екран read-only (`homework-design/02-sent-to-restaurant.png`).

**Умови:**
- стани: loading, error з кнопкою «Try again», empty («Participants haven't added any dishes yet»);
- кнопка Submit неактивна, якщо в підтверджених учасників немає страв і поки запит виконується (подвійний клік не відправляє двічі);
- при помилці submit екран не ламається, кнопку можна натиснути знову. Toast уже показує `tokenInterceptor` — не дублюй;
- тексти, по яких перевіряють тести: кнопка «Submit Pre-Order», у діалозі — «Submit» і «Cancel», бейдж «Collecting pre-orders» / «Sent to Restaurant», «Try again», «Group reservation not found»;
- **тести:** скопіюй `homework-tests/level-2/` в `src/app/` — він **замінить** тест сторінки з рівня 1 (тепер сторінка бере дані з сервісу). 5 тестів сервісу + 13 сторінки: дані, «My Pre-Order», **$540 а не $760**, стани, Submit один раз. `npm run test:hw` — усе зелене.

Мок скидається після перезавантаження сторінки — можна натискати Submit скільки завгодно.

---

## 3й рівень — інтеграція з API

Замінити мок **справжнім API**: ендпоінти з `homework-backend.md` (їх роблять на бекенд-треку), реальний логін і реальне бронювання. Плюс тримати екран у синхроні з тим, що роблять гості.

**Що підняти:** бекенд з ендпоінтами з `homework-backend.md` (бекенд-трек марафону) і вписати його адресу в `src/environments/environment.development.ts` → `backendUrl` (URL стейджу API Gateway, наприклад `https://abc123.execute-api.eu-central-1.amazonaws.com/develop`). Після зміни перезапусти `npm start`.

В архіві бекенду немає, а `backendUrl` — заглушка: на рівнях 1–2 вона не потрібна, бо все йде через мок. Тому інші сторінки застосунку (меню, бронювання) без бекенду показуватимуть помилки — це нормально.

**Як перевірити на справжніх даних:**
1. Вийди з демо-акаунта (**Sign out** у шапці): справжній API демо-токен не прийме. Зареєструйся або залогінься як Customer і забронюй столик на 2+ гостей.
2. Після бронювання (Book a Table) у вікні підтвердження є **Invite Link** — скопіюй посилання `/join/…`.
3. Відкрий посилання в іншому браузері або інкогніто — це гість: ім'я → страви → підтвердити кошик.
4. Зроби власний pre-order (без нього бекенд поверне 400 на submit).
5. Відкрий свій екран: `/reservations/<твій reservationId>/group-order`. Мок на справжні id не реагує — запити йдуть на API.

**Що зробити:**
- **Фонова синхронізація:** поки група `Open`, кожні 15 секунд підтягуй свіжі кошики і статус. Гість підтвердив кошик — Owner бачить це без перезавантаження сторінки. Після Submit синхронізація зупиняється.
- Без лоадера на весь екран під час фонового оновлення: дані на екрані просто змінюються.
- Рядок «Updated at 14:05:30» і кнопка **Refresh** — синхронізуватись зараз.
- Якщо фонове оновлення не вдалось — лишаємо останні дані й пишемо «Couldn't refresh. Showing data from …». Toast на кожну невдалу спробу не показуємо: для цього в запит передай `HttpContext` з `SKIP_ERROR_TOAST` (`src/app/auth/skip-error-toast.context.ts`).
- Помилки справжнього бекенду на Submit (немає підтверджених кошиків, немає order) — показуються toast-ом з текстом від API, екран не ламається.

**Умови:**
- таймер не плодиться: повторне завантаження (Try again) не запускає другий таймер, а при виході зі сторінки таймер зупиняється;
- якщо попередній запит ще не відповів, новий не стартує (підказка: `exhaustMap`);
- `getSessions$(id, { silent: true })` і `getStatus$(id, { silent: true })` — другий параметр для тихих запитів; рядок «Updated at …», кнопка «Refresh», при помилці — «Couldn't refresh…»;
- **тести:** скопіюй `homework-tests/level-3/` в `src/app/` (нові файли `*.sync.spec.ts`, тести рівня 2 лишаються): 3 тести сервісу + 6 синхронізації з `fakeAsync` + `tick(15_000)`.

### Бонус: Remove Guest

**`DELETE /guest/groups/{groupReservationId}/sessions/{guestSessionId}`** — відповідь у форматі `GET …/sessions`.
- Посилання **Remove Guest** у картці кожного гостя (`type: 'Guest'`), біля Owner його немає; після submit — теж немає.
- Підтвердження через `<app-modal>` (`homework-design/03-remove-guest.png`): «Are you sure you want to remove **Alex** and their pre-order from this reservation? This action cannot be undone.»
- Після успіху список і суми оновлюються з відповіді сервера — через ті самі `computed()`.
- Кнопка має `aria-label` з ім'ям: «Remove Alex»; у картці — `canRemove = input(false)` і `removeClick = output<Participant>()`, у сервісі — `removeSession$(groupId, guestSessionId)`.
- **Тести:** `homework-tests/level-3-bonus/` (1 + 5).

---

## Для всіх рівнів

- Standalone-компоненти, `inject()`, `input()`/`output()`, signals для стану — без нових `Subject`/`BehaviorSubject`.
- Перед здачею — чотири перевірки:
  ```bash
  npx tsc -p tsconfig.app.json  --noEmit
  npx tsc -p tsconfig.spec.json --noEmit
  BACKEND_URL=https://example.com npm run build
  npm run test:hw    # тести домашки (решта тестів проєкту до домашки не належить — частина з них уже червона в main)
  ```
- До здачі додай короткий опис: що зроблено, скріни станів екрана, чи використовувався AI і як ти перевірив результат.
