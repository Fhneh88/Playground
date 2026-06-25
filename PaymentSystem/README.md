# PaymentSystem

Учебный проект на полиморфизм: четыре способа оплаты с разной бизнес-логикой, один `PaymentProcessor`, который обрабатывает их все одинаково.

## Методы оплаты

Каждый класс переопределяет виртуальный метод `Process(decimal amount)` по-своему:

- **`CardPayment`** - добавляет комиссию 2%
- **`WalletPayment`** - проверяет лимит 10 000 руб. и возвращает ошибку при превышении
- **`CryptoPayment`** - конвертирует сумму в BTC по курсу 60 000 руб./BTC
- **`BankTransferPayment`** - просто выполняет перевод без дополнительных условий

`PaymentProcessor.ProcessAll` принимает массив `PaymentMethod[]` и сумму, вызывает `Process` у каждого и возвращает все результаты. Ему не важно, какой именно это метод - полиморфизм берёт на себя маршрутизацию.

## Запуск

```bash
# Демонстрация
dotnet run --project PaymentSystem

# Тесты
dotnet test PaymentModule.Tests
```

## Структура

```text
PaymentSystem/
|-- Program.cs                    - точка входа с примером использования
|-- PaymentModule/
│   |--- PaymentMethod.cs          - базовый класс и четыре реализации
|-- PaymentModule.Tests/
    |--- PaymentModule.Tests.cs    - юнит-тесты
```
