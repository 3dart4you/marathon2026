# Тести домашки

Тести вже написані — ти пишеш код, поки вони не стануть зеленими.

| Папка | Для чого | Скільки тестів |
|---|---|---|
| `practice/` | практичне: картка учасника | 8 |
| `level-1/` | рівень 1: верстка сторінки | 5 |
| `level-2/` | рівень 2: сервіс і сторінка на моках | 5 + 13 |
| `level-3/` | рівень 3: синхронізація | 3 + 6 |
| `level-3-bonus/` | бонус: Remove Guest | 1 + 5 |

## Як користуватись

1. **На початку рівня** скопіюй вміст папки рівня в `src/app/` (з заміною файлів, якщо спитає). Структура папок уже збігається.
   - macOS / Linux, з `frontend/restaurant-frontend-app`:
     ```bash
     cp -R homework-tests/practice/. src/app/
     ```
   - Windows PowerShell:
     ```powershell
     Copy-Item -Recurse -Force homework-tests\practice\* src\app\
     ```
   - або просто перетягни папки в IDE.
2. Запусти тести:
   ```bash
   npm run test:hw
   ```
   Спершу вони **червоні** (або не компілюються — файлів ще немає). Це нормально.
3. Пиши код за `homework-frontend.md` і запускай `npm run test:hw` після кожного кроку. Зелених ставатиме більше.
4. Рівень готовий, коли всі тести рівня зелені.

`level-2/` **замінює** тест сторінки з `level-1/` — на рівні 2 сторінка бере дані з сервісу, а не зі статичних даних. Тест картки з `practice/` лишається.

## Що тести перевіряють

Тести дивляться на те, що бачить користувач (текст, кнопки, діалог), тому вони прив'язані до назв і текстів з `homework-frontend.md`:
- класи `ParticipantCardComponent`, `GroupOrderPageComponent`, `GroupOrdersService` у файлах, які вказано в завданні;
- input картки — `participant` (і `isMe` на рівні 2, `canRemove` / output `removeClick` для бонусу);
- методи сервісу — `getGroupInvite$`, `getSessions$`, `getOwnerSessionId$`, `getStatus$`, `submit$` (+ `removeSession$` для бонусу; на рівні 3 `getSessions$` і `getStatus$` приймають другий параметр `{ silent: true }`);
- тексти кнопок — «Submit Pre-Order», у діалозі «Submit», «Try again», «Refresh», «Remove Guest».

Червоний тест — це підказка: прочитай його назву і повідомлення, там написано, чого бракує.
