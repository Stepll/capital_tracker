# Capital Tracker

Персональний застосунок для обліку капіталу (нерухомість, акції, інвестиції з
різних сервісів) з AI-аналітикою по галузях портфеля.

Стек: React + TypeScript · .NET 8 · PostgreSQL. Деталі архітектури й доменної
моделі — у [CLAUDE.md](CLAUDE.md).

## Запуск локально

### Через Docker Compose (рекомендовано)

```bash
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
`backend/src/CapitalTracker.Api/appsettings.json`).

## Статус

Проєкт на ранній стадії — див. розділ "Роадмап" у [CLAUDE.md](CLAUDE.md).
