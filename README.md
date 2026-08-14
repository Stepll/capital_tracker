# Capital Tracker

Персональний застосунок для обліку капіталу (нерухомість, акції, інвестиції з
різних сервісів) з AI-аналітикою по галузях портфеля.

Стек: React + TypeScript · .NET 8 · PostgreSQL. Деталі архітектури й доменної
моделі — у [CLAUDE.md](CLAUDE.md).

## Запуск локально

### Через Docker Compose (рекомендовано)

```bash
cp .env.example .env   # заповнити JWT_SECRET / INITIAL_USER_EMAIL / INITIAL_USER_PASSWORD
docker compose up --build
```

- Frontend: http://localhost:3000
- API: http://localhost:5000/api/health
- Postgres: localhost:5432

### Вручну

```bash
# Backend
cd backend
dotnet run --project src/CapitalTracker.Api

# Frontend (в іншому терміналі)
cd frontend
npm install
npm run dev
```

Потрібен локальний Postgres (див. `ConnectionStrings:Default` в
`backend/src/CapitalTracker.Api/appsettings.json`). Dev-секрет для JWT і
дефолтний користувач (`dev@local` / `devpassword123`) вже прописані в
`appsettings.Development.json` — для локального запуску нічого додатково
налаштовувати не треба.

## Auth

Застосунок персональний, для одного користувача. `POST /api/auth/login` з
`{ "email": ..., "password": ... }` повертає JWT, який треба передавати як
`Authorization: Bearer <token>` — усі інші ендпоінти закриті за замовчуванням.

## Статус

Проєкт на ранній стадії — див. розділ "Роадмап" у [CLAUDE.md](CLAUDE.md).
