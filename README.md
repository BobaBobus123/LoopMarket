# LoopMarket API

Платформа для закрытой студенческой барахолки.

## Етап 1: Ініціалізація та БД
- Створено базову структуру ASP.NET Core Web API.
- Налаштовано підключення до SQLite (EF Core).
- Спроєктовано доменні моделі: User, Category, Listing.
- Створено та застосовано початкову міграцію схеми БД.
- Додано health-check endpoint (`GET /api/health`).

### Як запустити
1. Клонувати репозиторій.
2. Виконати `dotnet restore`.
3. Виконати `dotnet ef database update` для створення БД `loopmarket.db`.
4. Виконати `dotnet run` та перейти за адресою `https://localhost:7274/api/health`.