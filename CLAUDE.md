# Capital Tracker

Персональний застосунок для обліку капіталу (нерухомість, акції, інвестиції з
різних сервісів) з AI-аналітикою по активах і секторах портфеля. Один
користувач (власник), не multi-tenant.

**Живий прод:** https://capital-tracker.75.119.152.4.sslip.io/api (бекенд на VPS)
Фронтенд деплоїться на Vercel окремо (root directory `frontend`).

## Стек

- **Backend:** .NET 8, ASP.NET Core Web API, EF Core + Npgsql, MediatR (CQRS),
  Hangfire (фонові job'и), BCrypt.Net-Next (хешування пароля), AES-256-GCM
  (шифрування секретів холдингів)
- **Frontend:** React 19 + TypeScript, Vite, TanStack Query, React Router,
  Recharts (графіки — кольори через `dataviz` skill, валідована палітра)
- **DB:** PostgreSQL 16
- **Деплой:** Docker Compose на спільному VPS (є інші проєкти на тій же
  машині — `homegroup-crm`, `vypusknyk-plus`, боти), nginx + Let's Encrypt
  через `sslip.io` (безкоштовний DNS без покупки домену)

## Структура репозиторію

```
backend/
  CapitalTracker.sln
  src/
    CapitalTracker.Api/            Контролери, JWT-auth, CORS, DI, Program.cs
    CapitalTracker.Application/    CQRS use-case'и (MediatR), DTO
      Auth/          Login
      Accounts/      CRUD + список з реальними сумами (TotalValue)
      Holdings/      CRUD, деталі, оцінки з датою, атрибути, секрети
      Sectors/       CRUD + seed дефолтних
      Insights/      AI-заглушки (сектор- і холдинг-рівневі)
      Settings/      DisplayCurrency, ExchangeRate
      Dashboard/     GetDashboardSummaryQuery (алокація, історія, конвертація)
      Common/        SupportedCurrencies, IApplicationDbContext, IEncryptionService
    CapitalTracker.Domain/         Entities, Enums — без зовнішніх залежностей
    CapitalTracker.Infrastructure/
      Auth/          BCryptPasswordHasher, JwtTokenService, UserSeeder
      Security/      AesEncryptionService (секрети холдингів)
      MarketData/    NbuExchangeRateClient, ExchangeRateSyncService
      Persistence/   DbContext, Configurations/, Migrations/, SectorSeeder
    CapitalTracker.Worker/         Hangfire: щоденна синхронізація курсів НБУ
  tests/CapitalTracker.Tests/      xUnit (мінімальний, каркас)
frontend/
  src/
    features/
      auth/          LoginPage
      dashboard/     DashboardPage, AllocationChart (donut), useDashboardSummary
      accounts/      AccountCard, AccountDetailPage, accountTypeColors
      holdings/      HoldingDetailPage (2-колонковий десктопний лейаут),
                     HoldingAttributesSection, SecretField, HoldingInsightsPanel,
                     attributeTemplates (шаблони полів per AccountType)
      insights/      InsightsPage (сектор-рівневий фід)
      sectors/       useSectors
      settings/      SettingsPage (валюта, курси)
    shared/
      api/client.ts  axios + JWT-interceptor + редірект на /login при 401
      auth/          AuthContext, ProtectedRoute
      ui/            ValueOverTimeChart, Modal.module.css, Charts.module.css
                     (перевикористовувані між features — не дублювати стилі)
docker-compose.yml    postgres (без публічного порту) + api + worker + frontend
```

## Доменна модель

```
User (єдиний власник; DisplayCurrency — в якій валюті рахувати капітал)
Account (Brokerage/Bank/RealEstate/Cash/Crypto/Other)
  └─ Holding
      ├─ Quantity (decimal?) — кількість одиниць (акції, крипта)
      ├─ Notes (string?)
      ├─ Attributes (Dictionary<string,string>, JSONB) — довільні публічні поля
      │    (забудовник/адреса для нерухомості, сервіс для брокера тощо;
      │    шаблони запропонованих полів — фронтенд, attributeTemplates.ts)
      ├─ SecretAttributes (Dictionary<string,string>, JSONB, значення —
      │    AES-256-GCM ciphertext) — логін/пароль до сервісів. Ключ шифрування
      │    (`Encryption:Key`) окремий від JWT-секрету, обов'язковий через .env
      ├─ SectorId (nullable, зараз не виставляється з UI — прибрали селектор)
      ├─ Transaction[] (Buy/Sell/Dividend/...) — сутність є, CRUD ще нема
      └─ ValuationSnapshot[] (Date, Value, Currency, IsManual) — апсерт по
           даті (друге значення на ту саму дату заміщує перше)
Sector (довідник; 8 дефолтних заseed-жені автоматично при старті)
AiInsight — scoped або на Sector, або на Holding (nullable обидва, рівно
  один заповнений). HoldingId має OnDelete(Cascade), SectorId — SetNull
  (важливо: за замовчуванням EF Core ставить Restrict для nullable FK,
  не Cascade — звідси був баг, див. нижче)
ExchangeRate (Date, Currency, RateToUah) — курс НБУ, UAH — анкер (rate=1)
```

## Auth

- Один рядок у `Users`, пароль хешується BCrypt
- `POST /api/auth/login` (`[AllowAnonymous]`) → JWT (тиждень)
- Все інше закрито за замовчуванням (`FallbackPolicy.RequireAuthenticatedUser()`
  в `Program.cs`) — новий контролер без `[AllowAnonymous]` автоматично захищений
- `UserSeeder` створює власника при старті з `InitialUser:Email/Password`
  (тільки якщо users порожні)
- **Важливо:** `options.MapInboundClaims = false` на `AddJwtBearer` — інакше
  ASP.NET тихо перемаповує claim `sub` в `ClaimTypes.NameIdentifier`, і
  `User.FindFirstValue(JwtRegisteredClaimNames.Sub)` завжди повертає `null`
- Секрети (`Jwt:Secret`, `InitialUser:*`, `Encryption:Key`) — тільки env vars,
  ніколи в `appsettings.json`. Всі три валідуються **eager** при старті
  (падає одразу при деплої, а не на першому запиті)

## Валюта й конвертація

- `User.DisplayCurrency` (UAH/USD/EUR) — сторінка `/settings`
- `ExchangeRate` синхронізується `Worker`-ом раз на день з НБУ API
  (безкоштовний, без ключа) + одноразово при старті Worker'а
- Конвертація в дашборді: `GetDashboardSummaryQuery` рахує все **в пам'яті**
  (не через EF LINQ) — детальніше в розділі "EF Core — набиті ґулі" нижче
- Холдинг **успадковує валюту рахунку** автоматично, не обирається окремо —
  так сума в межах рахунку завжди рахується без конвертації

## AI-аналітика — поточний стан (заглушка, за дизайном)

Домовленість: механіка (клік → збереження → показ у стрічці) готова
end-to-end **зараз**, реальна LLM+новини інтеграція — наступний крок.

- `POST /api/holdings/{id}/insights/generate` — генерує `AiInsight` для
  конкретного активу. Текст-заглушка вже **перелічує `Holding.Attributes`**
  як прев'ю контексту, який врахує майбутній реальний аналіз (наприклад,
  забудовника для нерухомості) — щоб дизайн уже демонстрував намір
- `POST /api/insights/generate` — те саме на рівні сектору (стара, ще жива
  сторінка `/insights` з фідом по секторах)
- Права колонка `HoldingDetailPage` показує: кнопка → останній аналіз →
  історія попередніх. Немає більше селектора сектору в UI холдингу.

**Наступний крок (не зроблено):** замінити тіло `GenerateHoldingInsightCommand`
на реальний пайплайн — новини по тікеру/сектору + промпт до LLM. Вся
інфраструктура (`AiInsight`, ендпоінти, UI) вже готова, міняти тільки логіку.

## Розгортання (VPS)

- Сервер: `ssh capital-tracker-vps` (alias в `~/.ssh/config`, окремий deploy-ключ,
  пароль-логін вимкнено)
- Код у `/root/capital_tracker` на сервері, деплой:
  ```bash
  ssh capital-tracker-vps "cd /root/capital_tracker && git pull && \
    docker compose build api worker && docker compose up -d api worker"
  ```
- **Важливо:** `Api` застосовує EF-міграції при старті, `Worker` — ні
  (обидва одночасно = race condition на `ALTER TABLE`, Worker падав)
- Секрети на сервері — `/root/capital_tracker/.env` (`JWT_SECRET`,
  `INITIAL_USER_EMAIL/PASSWORD`, `ENCRYPTION_KEY`) — не в git, `.env.example`
  в репо як шаблон
- Postgres без публічного порту (тільки docker-мережа) — не конфліктує з
  іншими проєктами на тому ж сервері
- CORS дозволяє `*.vercel.app` (прод + preview-деплої) і localhost

## EF Core — набиті ґулі (щоб не наступати знову)

Кілька патернів у LINQ-запитах ламали трансляцію в SQL і падали 500 **лише
в рантаймі**, компіляція проходила нормально:
1. `GroupBy(...).GroupJoin(...).DefaultIfEmpty()` для "останнє значення в
   групі" — не транслюється, щойно композиція ускладнюється
2. Fallback на navigation-властивість всередині `??` в проєкції (`?? h.Account!.Currency`)
3. `.OrderBy()` **після** `.Select()` в DTO-record — не транслюється

**Правило, яке тепер діє в проєкті:** для дашборду/деталей активу — фетчити
плоскі таблиці (`ToListAsync()` без хитрощів) і агрегувати/сортувати в C#.
Дані персонального застосунку крихітні, продуктивність не постраждає, а
багів менше. Дивись `GetDashboardSummaryQuery`, `GetHoldingByIdQuery` як
референс цього підходу.

Інші дрібні уроки:
- Hangfire в plain Worker (не ASP.NET Core) — `RecurringJob` (статичний API)
  падає з "JobStorage not initialized", треба `IRecurringJobManager` з DI
- nginx повертає 400 на POST без `Content-Length`/chunked-заголовка (curl без
  `-d` це не ставить) — фронтенд (axios) сам це коректно робить, але явний
  порожній body (`post(url, {})`) безпечніший

## Команди розробки

```bash
# Backend
cd backend
dotnet build
dotnet run --project src/CapitalTracker.Api
dotnet ef migrations add <Name> --project src/CapitalTracker.Infrastructure \
  --startup-project src/CapitalTracker.Api -o Persistence/Migrations

# Frontend
cd frontend
npm install
npm run dev

# Все разом локально
docker compose up --build
```

## Роадмап

1. ~~Кістяк — auth, CRUD accounts/holdings, dashboard net worth~~ ✅
2. ~~Графіки (кругова + лінійна), конвертація валют~~ ✅
3. ~~Сторінка активу — атрибути per тип, шифровані секрети, дата оцінки,
   Quantity~~ ✅
4. ~~AI-заглушки на рівні активу й сектору, редизайн дашборду й сторінки
   активу (широкий 2-колоночний лейаут)~~ ✅
5. **Наступне: реальна AI-аналітика** — новини по тікеру/сектору + LLM-промпт
   замість тексту-заглушки в `GenerateHoldingInsightCommand`/`GenerateInsightCommand`
6. Пізніше: CRUD транзакцій (buy/sell/dividend — сутність є, форм нема),
   автооновлення цін акцій/крипти через зовнішнє API (окремий job у Worker,
   `ValuationSnapshot.IsManual=false`), CSV-імпорт з брокерів/банків,
   "закрити/продати" актив без втрати історії, нагадування про оновлення
   оцінки неліквідних активів
