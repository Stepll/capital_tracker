# Capital Tracker

Персональний застосунок для обліку капіталу (нерухомість, акції, інвестиції з
різних сервісів) з AI-аналітикою по активах і секторах портфеля. Один
користувач (власник), не multi-tenant.

**Живий прод:** https://capital-tracker.75.119.152.4.sslip.io/api (бекенд на VPS)
Фронтенд деплоїться на Vercel окремо (root directory `frontend`).

## Стек

- **Backend:** .NET 8, ASP.NET Core Web API, EF Core + Npgsql, MediatR (CQRS,
  включно зі стрім-реквестами), Hangfire (фонові job'и), BCrypt.Net-Next
  (хешування пароля), AES-256-GCM (шифрування секретів холдингів),
  `Anthropic` SDK (`claude-opus-5` — AI-аналіз активів)
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
      Insights/      Стрім-команда аналізу, події, DTO фактів, cooldown-опції
                     (сектор-рівнева генерація — досі заглушка)
      Settings/      DisplayCurrency, ExchangeRate
      Dashboard/     GetDashboardSummaryQuery (алокація, історія, конвертація)
      Common/        SupportedCurrencies, IApplicationDbContext, IEncryptionService
    CapitalTracker.Domain/         Entities, Enums — без зовнішніх залежностей
    CapitalTracker.Infrastructure/
      Auth/          BCryptPasswordHasher, JwtTokenService, UserSeeder
      Security/      AesEncryptionService (секрети холдингів)
      Ai/            AnthropicHoldingAnalysisGenerator, InsightPrompts,
                     SaveAnalysisTool (JSON-схема strict-tool'а)
      MarketData/    NbuExchangeRateClient, ExchangeRateSyncService, FinnhubClient
      Persistence/   DbContext, Configurations/, Migrations/, SectorSeeder
    CapitalTracker.Worker/         Hangfire: щоденна синхронізація курсів НБУ +
                                   щоденне оновлення цін тікерних активів (22:00 UTC)
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
      insights/      HoldingAnalysisModal (картки фактів), streamHoldingInsight
                     (SSE через fetch), insightTypes (мітки + safeHttpUrl),
                     InsightsPage (сектор-рівневий фід, ще заглушка)
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
- Секрети (`Jwt:Secret`, `InitialUser:*`, `Encryption:Key`, `Anthropic:ApiKey`) —
  тільки env vars, ніколи в `appsettings.json`. Валідуються **eager** при старті
  (падає одразу при деплої, а не на першому запиті)
- **Локально** `Anthropic:ApiKey` — через user-secrets, не в
  `appsettings.Development.json` (той комітиться, а це ключ із реальною вартістю):
  `dotnet user-secrets set "Anthropic:ApiKey" "sk-ant-..." --project src/CapitalTracker.Api`
- `Finnhub:ApiKey` — свідомо **опційний**: без нього аналіз просто працює на
  самому веб-пошуку, застосунок стартує нормально

## Валюта й конвертація

- `User.DisplayCurrency` (UAH/USD/EUR) — сторінка `/settings`
- `ExchangeRate` синхронізується `Worker`-ом раз на день з НБУ API
  (безкоштовний, без ключа) + одноразово при старті Worker'а
- Конвертація в дашборді: `GetDashboardSummaryQuery` рахує все **в пам'яті**
  (не через EF LINQ) — детальніше в розділі "EF Core — набиті ґулі" нижче
- **Валютою обліку є `ValuationSnapshot.Currency`, а не рахунок.** `Holding` не має
  поля валюти й не повинен його мати — третє місце для валюти вимагало б правил
  синхронізації зі знімками й нічого не дає. Актив цілком легітимно буває
  номінований інакше за свій рахунок (доларова акція на гривневому брокерському)
- Через це **будь-який тотал, що охоплює кілька холдингів, мусить іти через
  `CurrencyConverter`** (`Application/Common/`), а не сумувати сирі значення.
  Наступити на це вже вдавалося: `GetAccountsQuery` і `AccountDetailPage` складали
  USD із UAH і підписували валютою рахунку
- Нова оцінка успадковує валюту **останнього знімка** (не рахунку), а наявний рядок
  не перештамповується без явного вибору — інакше виправлення валюти неможливе
- `ValuationSnapshot` має **унікальний індекс `(HoldingId, Date)`**. До нього прод
  устиг накопичити три рядки на одну дату, після чого апсерт із `SingleOrDefaultAsync`
  кидав виняток і сторінка активу не оновлювалась зовсім

## AI-аналітика активу (реальна, працює)

`POST /api/holdings/{id}/insights/stream` — генерує аналіз і **стрімить прогрес
через SSE**. Результат: короткий `Summary` + список `AnalysisFact` (claim,
категорія, полярність, впевненість, `IsNew`, джерело), збережений у jsonb.

**Пайплайн** (`StreamHoldingInsightCommand` → `IHoldingAnalysisGenerator`):
1. Дешеві перевірки **до** будь-якого платного виклику: холдинг існує,
   `ExcludeFromAiAnalysis`, cooldown
2. Пре-фетч Finnhub (тільки якщо є `Holding.Symbol` **і** рахунок `Brokerage` —
   для крипти Finnhub потребує префіксів типу `BINANCE:BTCUSDT`, які не вивести)
3. `claude-opus-5` з server-tool `WebSearchTool20260209` + strict-tool
   `save_analysis`; фази стріму мапляться з подій SDK
4. Збереження — тільки на успіх

**Ключові рішення й пастки:**
- **`SecretAttributes` ніколи не йдуть у модель.** Гарантія структурна:
  `HoldingAnalysisRequest` не має поля, здатного їх нести. Тест це фіксує.
- **Структурований вивід через strict-tool, не `output_config.format`** —
  бо format документовано несумісний з citations, які повертає web search.
- **Cooldown = час останнього збереженого інсайту.** Оскільки зберігаємо лише
  на успіх, невдалий чи скасований прогін не витрачає вікно. Окремого стану нема.
- **`SectorId` у холдингових інсайтах завжди `null`** — стара заглушка ставила
  обидва FK, через що вони текли в секторний фід. Є регресійний тест.
- **`yield return` не можна в `try` з `catch`** — звідси хелпер `TryAsync` у
  хендлері; помилки БД стають подією `Failed(Internal)`, а не обірваним з'єднанням.
- **У стрімі tool input приходить фрагментами `input_json_delta`** — готового
  словника нема, треба акумулювати по індексу блоку й парсити на `content_block_stop`.
- **`StopReason` (перевірка на `refusal`) приходить на `message_delta`**, і це
  `ApiEnum` — порівнювати через `.Raw()`, бо `.ToString()` дає `"Refusal"`
  замість дротового `"refusal"` (компілюється, але ніколи не збігається).
- **Без `Temperature`/`TopP`/`TopK`** — Opus 5 віддає на них 400.
- Вартість ≈ **$0.10–0.50 за аналіз** — це те, що захищає cooldown.

**SSE крізь nginx — конфіг VPS чіпати не треба:** заголовок `X-Accel-Buffering: no`
вимикає буферизацію для цієї відповіді, а heartbeat `: ping` кожні 15с тримає
`proxy_read_timeout` (він ресетиться на кожному читанні). Фронтенд читає стрім
через `fetch` + `ReadableStream` — `EventSource` не вміє слати `Authorization`.

**Сектор-рівневий `/api/insights/generate` досі заглушка** — стара сторінка
`/insights` не переписана.

**Кольори карток фактів:** категорія — текстовий чіп **без** відтінку. Це не
недогляд: 7-слотова категоріальна палітра провалює валідатор `dataviz` на цьому
тлі (найгірша пара ΔE 1.6 при дейтеранопії, 9.8 при нормальному зорі — нижче
порогу 15). Колір віддано полярності (`--success`/`--danger`/`--text-faint`), і
вона **обов'язково** з гліфом ▲/▼/•, бо червоний↔зелений дає ΔE 6.4.

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
  `INITIAL_USER_EMAIL/PASSWORD`, `ENCRYPTION_KEY`, `ANTHROPIC_API_KEY`;
  опційно `FINNHUB_API_KEY`, `INSIGHTS_COOLDOWN_HOURS`) — не в git,
  `.env.example` в репо як шаблон
- **Нові обов'язкові змінні додавати в `.env` на сервері ДО деплою:** guard
  `${VAR:?...}` у compose валить саму команду `docker compose`, а не контейнер
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
- **`FinnhubClient` ковтає всі помилки й повертає `null`** («аналізуй без ринкових
  даних»). Наслідок: забутий `BaseAddress` (клієнт ходить відносними шляхами) або
  відсутній ключ дають **вічно тихий no-op**, не помилку. Тому `BaseUrl` — константа
  на клієнті, а price-job перевіряє `IsConfigured` наперед і пише підсумковий рядок
  щопрогону — інакше «не налаштовано» не відрізнити від «спрацювало, нічого робити»
- **In-memory провайдер EF ігнорує унікальні індекси** — індекс
  `(HoldingId, Date)` перевіряти лише на живому Postgres
- **`dotnet ef` будує хост Api**, тому eager-валідація `Anthropic:ApiKey` блокувала
  скафолд міграцій. Полагоджено `CapitalTrackerDbContextFactory`
  (`IDesignTimeDbContextFactory`) — design time більше не залежить від LLM-ключа
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
5. ~~Реальна AI-аналітика активу — web search + Finnhub + `claude-opus-5`,
   структуровані факти з тегами, SSE-стрім прогресу, cooldown, opt-out~~ ✅
6. ~~Автооновлення цін тікерних активів (Hangfire-job + Finnhub), валюта на рівні
   знімка, унікальний індекс `(HoldingId, Date)`~~ ✅
7. **Наступне:** графік історії конвертує **всі** минулі точки за сьогоднішнім
   курсом (`GetDashboardSummaryQuery`, історична серія) — після щоденних
   USD-знімків це стало помітно. Фікс: `CurrencyConverter.ConvertAsOf(date)`,
   що бере останній курс ≤ дати; два місця виклику — історія дашборду й
   `GetHoldingByIdQuery`
8. Пізніше: сектор-рівневий `/insights` на той самий пайплайн (єдина заглушка,
   що лишилася) або прибрати сторінку; CRUD транзакцій (buy/sell/dividend —
   сутність є, форм нема); CSV-імпорт з брокерів/банків; "закрити/продати"
   актив без втрати історії; нагадування про оновлення оцінки неліквідних
   активів
