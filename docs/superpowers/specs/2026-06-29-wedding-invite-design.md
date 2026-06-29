# Wedding Invite — Design Spec

**Date:** 2026-06-29  
**Status:** Approved

---

## Overview

Сайт-приглашение на свадьбу с блоком выбора блюд из меню. Каждый гость вводит своё имя, выбирает позиции из меню и подтверждает участие. Выборы автоматически записываются в Google Таблицу для удобного отслеживания.

---

## Tech Stack

| Компонент | Технология |
|-----------|------------|
| Framework | ASP.NET Core 8, Razor Pages |
| Стиль | CSS (романтичный, цветочный, розово-зелёные тона) |
| Хранение данных | Google Sheets v4 API (сервисный аккаунт) |
| NuGet | `Google.Apis.Sheets.v4` |
| Хостинг | Любой .NET-совместимый (Azure, Railway, Render и т.д.) |

---

## Project Structure

```
WeddingInvite/
├── Pages/
│   ├── Index.cshtml            ← Основная страница-приглашение
│   ├── Index.cshtml.cs         ← PageModel: обработка POST формы
│   └── ThankYou.cshtml         ← Страница благодарности после отправки
├── Services/
│   └── GoogleSheetsService.cs  ← Запись строки в Google Таблицу
├── Models/
│   └── RsvpModel.cs            ← Имя гостя + выборы из меню
├── wwwroot/
│   ├── css/style.css           ← Романтичный визуальный стиль
│   └── images/                 ← Фото для секций сайта
├── appsettings.json            ← SpreadsheetId, путь к credentials
└── Program.cs
```

---

## Page Sections (Index.cshtml)

Одна длинная страница со следующими секциями:

1. **Hero** — имена молодожёнов, дата и место проведения, фоновое фото
2. **Наша история** — 2–3 фото + короткий текст о паре
3. **Программа дня** — timeline: церемония → фуршет → банкет
4. **Меню** — блок выбора блюд (4 категории)
5. **RSVP форма** — имя гостя + кнопка «Подтвердить участие»

---

## Menu Block

Четыре категории с placeholder-позициями (владелец заменяет своим реальным меню):

| Категория | Тип выбора | Кол-во позиций |
|-----------|-----------|----------------|
| Холодные закуски | Radio (один вариант) | 3 |
| Горячее | Radio (один вариант) | 3 |
| Десерт | Radio (один вариант) | 3 |
| Напитки | Checkbox (несколько) | 4 |

---

## Data Model

```csharp
public class RsvpModel
{
    [Required]
    public string GuestName { get; set; }

    [Required]
    public string Starter { get; set; }    // Холодная закуска

    [Required]
    public string MainCourse { get; set; } // Горячее

    [Required]
    public string Dessert { get; set; }    // Десерт

    public List<string> Drinks { get; set; } = new(); // Напитки (0..N)
}
```

---

## Data Flow

```
Гость открывает сайт
    → вводит имя
    → выбирает блюда из меню
    → нажимает «Подтвердить участие»
    → POST /Index
    → IndexModel.OnPostAsync()
    → GoogleSheetsService.AppendRowAsync(rsvp)
    → Запись строки в Google Таблицу
    → RedirectToPage("ThankYou", new { name = rsvp.GuestName })
    → ThankYou.cshtml отображает «Спасибо, {Имя}!»
```

---

## Google Sheets Integration

- Аутентификация через **сервисный аккаунт** (файл `credentials.json`, не коммитится в git)
- `SpreadsheetId` хранится в `appsettings.json`
- `credentials.json` путь хранится в `appsettings.json` (или env variable на проде)
- Строка записывается методом `Append` в первый лист таблицы

**Формат строки в таблице:**

| Timestamp | Имя гостя | Закуска | Горячее | Десерт | Напитки |
|-----------|-----------|---------|---------|--------|---------|
| 29.06.2026 14:32 | Иван Петров | Карпаччо | Говядина | Торт | Вино, Сок |

---

## Visual Style

- **Палитра:** мягкий розовый (`#f8e8ee`), зелёный (`#a8c5a0`), белый, золотой акцент
- **Типографика:** serif-шрифт для заголовков (Google Fonts: Playfair Display), sans-serif для текста
- **Декор:** CSS цветочные орнаменты / SVG паттерн на фоне секций
- **Анимации:** плавное появление секций при скролле (Intersection Observer)

---

## ThankYou Page

Простая страница с:
- Именем гостя (передаётся через query param)
- Текстом «Спасибо, мы ждём вас!»
- Кратким итогом выборов гостя

---

## Security & Config

- `credentials.json` добавляется в `.gitignore`
- Валидация формы на стороне сервера (`[Required]`, ModelState)
- Client-side валидация через jQuery Unobtrusive Validation (стандарт Razor Pages)

---

## Out of Scope

- Авторизация / личный кабинет
- Уникальные ссылки для каждого гостя
- Email-уведомления
- Редактирование ответа после отправки
