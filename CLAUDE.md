# Capital Tracker

Персональний застосунок для обліку капіталу (нерухомість, акції, інвестиції з
різних джерел) з AI-аналітикою по галузях/секторах портфеля.

## Стек

- **Backend:** .NET 8, ASP.NET Core Web API, EF Core + Npgsql, MediatR (CQRS),
  FluentValidation, Hangfire (фонові job'и)
- **Frontend:** React 19 + TypeScript, Vite, TanStack Query, React Router, Recharts
- **DB:** PostgreSQL 16
- **Деплой:** Docker Compose на VPS (одна машина, single-user застосунок)

## Структура репозиторію

```
backend/
  CapitalTracker.sln
  src/
    CapitalTracker.Api/            ASP.NET Core Web API, контролери, auth, Swagger
    CapitalTracker.Application/    CQRS use-case'и (MediatR), DTO, валідація
    CapitalTracker.Domain/         Entities, Enums — без зовнішніх залежностей
    CapitalTracker.Infrastructure/ EF Core, зовнішні клієнти (MarketData, News, Ai, Import)
    CapitalTracker.Worker/         Фонові job'и (Hangfire): оновлення цін, AI-звіти
  tests/
    CapitalTracker.Tests/          xUnit
frontend/
  src/
    features/
      dashboard/    net worth, алокація активів, динаміка
      accounts/     CRUD джерел (брокер, банк, нерухомість, готівка)
      holdings/     активи + історія транзакцій
      import/       CSV-імпорт, мапінг колонок
      insights/     AI-звіти по секторах
    shared/
      api/          axios-клієнт
      ui/           перевикористовувані компоненти
      auth/         auth-логіка
docker-compose.yml   postgres + api + worker + frontend
```

## Доменна модель (Domain Layer)

```
Account (Brokerage/Bank/RealEstate/Cash/Crypto)
  └─ Holding (акція/ETF/обʼєкт нерухомості/депозит), опційно прив'язаний до Sector
      ├─ Transaction (Buy/Sell/Dividend/Rent/Expense/Deposit/Withdrawal)
      └─ ValuationSnapshot (вартість на дату; IsManual для нерухомості)
Sector (довідник галузей — для групування холдингів в аналітиці)
AiInsight (кешований згенерований звіт по сектору: текст + SourceUrls)
```

Принципи:
- `Domain` не залежить ні від чого — чисті сутності й енами.
- Вартість активу — не поле, а історія `ValuationSnapshot` (потрібно для графіків
  динаміки net worth у часі).
- AI-інсайти **генеруються фоновим job'ом і кешуються** в `AiInsight`, а не
  обчислюються на кожен HTTP-запит (дорого й повільно).

## Правила імпорту даних

Дані з брокерів/банків заводяться **вручну через CSV/форми** (без live-інтеграцій
з банківськими API на цьому етапі). Кожен формат виписки — окремий парсер у
`CapitalTracker.Infrastructure/Import/`. UI завжди показує крок звірки
(мапінг колонок → preview → підтвердження) перед збереженням — формати різні,
помилки мапінгу неминучі.

## AI-конвеєр (Infrastructure/Ai, Infrastructure/News)

Двоетапний, виконується фоновим job'ом (не на льоту):
1. Group holdings по `Sector`.
2. Тягнути свіжі новини по кожному сектору/тікеру через `Infrastructure/News`.
3. Промпт до LLM: поточна алокація портфеля по сектору + новини → короткий
   аналіз ризиків/можливостей.
4. Зберегти результат в `AiInsight` (з `SourceUrls` для трасування джерел).

## Команди розробки

```bash
# Backend
cd backend
dotnet build
dotnet run --project src/CapitalTracker.Api

# Frontend
cd frontend
npm install
npm run dev

# Все разом (Postgres + Api + Worker + Frontend)
docker compose up --build
```

## Роадмап (фази)

1. **Кістяк** — auth, CRUD accounts/holdings, ручні транзакції, dashboard net worth
2. **Імпорт + котирування** — CSV-імпорт, автооновлення цін акцій, графіки динаміки
3. **AI-аналітика** — секторальна алокація, інтеграція новин, генерація інсайтів
4. **Полірування** — нерухомість (ручні оцінки), борги/liabilities, експорт звітів

Зараз реалізовано лише каркас (Фаза 0): проєкти, доменні сутності, DbContext,
health-check ендпоінт, порожній React-фронтенд, docker-compose.
