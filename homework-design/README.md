# Дизайн домашки — Owner Flow (експорт з Figma)

Оригінал: [Figma — EPAM IT Marathon 6.0](https://www.figma.com/design/P9rkBNLJZ3WabXSQX9b5DO/EPAM-IT-Marathon-6.0?node-id=62645-20592) → сторінка *FOR DEVELOPERS* → секція **Owner Flow**. Тут — PNG тих самих екранів, щоб не шукати їх у Figma.

| Файл | Екран | Рівень |
|---|---|---|
| `01-owner-review.png` | Owner бачить кошики всіх учасників, Grand Total, кнопка Submit Pre-Order | 1 (верстка), 2 |
| `02-sent-to-restaurant.png` | Після відправки: бейдж «Sent to Restaurant», кнопка неактивна | 2 |
| `03-remove-guest.png` | Діалог Remove Guest; на фоні — картка бронювання з кнопкою «Group Pre-Order» | 3 (бонус), 2 (кнопка) |

## Що з макета **не** входить у домашку

- Кнопки «−/+» і «кошик» біля страв, «Edit my Pre-Order»: редагувати чужі кошики бекенд не вміє. Показуй кількість текстом (`× 2` або `Qty: 2`).
- Сповіщення «Maria has filled her pre-order…» — замість нього жовта плашка з `homework-frontend.md`: «N participants haven't submitted their cart yet…».
- Екран відкривається окремою сторінкою (`/reservations/:id/group-order`), а не модалкою поверх списку бронювань. Хочеш модалку — можна, але це не обов'язково.

## Що з макета **обов'язково**

- Шапка: «Group Pre-Order», бейдж статусу, адреса, дата, час, кількість гостей з іконками.
- Картка на кожного учасника: «My Pre-Order» / «Guest: Alex», рядок страви (фото або перша літера на зеленому колі, назва, кількість, ціна), Subtotal.
- Grand Total зеленим і широка зелена кнопка Submit Pre-Order; неактивна — сіра.
- Статус кошика гостя — текстом (Submitted / Still choosing): у макеті його немає, але без нього Owner не зрозуміє, чиї страви підуть у замовлення.
