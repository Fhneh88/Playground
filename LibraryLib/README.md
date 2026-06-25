# LibraryLib

Учебный проект, в котором принципы SOLID разобраны на двух конкретных примерах - сначала простая библиотека книг, потом более реальный сценарий регистрации пользователей.

## Библиотека книг

`Library` - класс для добавления книг, поиска по автору и выдачи. Выдать книгу дважды нельзя: `BorrowBook` возвращает `false` для уже взятой.

Намеренно простой пример, чтобы сфокусироваться не на бизнес-логике, а на дальнейшем применении SOLID.

## Регистрация пользователей - SOLID в деле

`UserManager.RegisterUser` выполняет четыре действия: валидирует данные, хеширует пароль, сохраняет пользователя, отправляет письмо и записывает лог. Каждый шаг - отдельный интерфейс с отдельной реализацией:

| Интерфейс | Что делает |
| --------- | ---------- |
| `IUserValidator` | Проверяет имя, email и длину пароля |
| `IPasswordHasher` | Хеширует пароль (здесь - Base64, в реальном проекте BCrypt) |
| `IUserRepository` | Сохраняет в файл |
| `IEmailSender` | Отправляет welcome-письмо через SMTP |
| `ILogger` | Пишет строку в лог-файл |

`UserManager` зависит только от этих интерфейсов - он не знает, как именно реализована каждая из пяти операций. Хочешь заменить хранение в файле на базу данных? Создаёшь новую реализацию `IUserRepository`, меняешь одну строчку в DI-регистрации - и больше ничего не трогаешь.

Все зависимости подключаются через `Microsoft.Extensions.DependencyInjection`:

```csharp
services.AddTransient<IUserValidator, UserValidator>();
services.AddTransient<IPasswordHasher, Base64PasswordHasher>();
services.AddTransient<IUserRepository>(_ => new FileUserRepository("users.txt"));
services.AddTransient<IEmailSender>(_ => new SmtpEmailSender("smtp.example.com", "noreply@app.com"));
services.AddTransient<ILogger>(_ => new FileLogger("log.txt"));
services.AddTransient<UserManager>();
```

## Запуск

```bash
dotnet run --project LibraryLib
```
