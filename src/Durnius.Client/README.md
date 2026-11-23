# Durnius.Client

React + TypeScript app for Durnius, built with Vite. Dark card-room theme,
no UI dependencies — just React and plain CSS.

## Getting started

```
npm install
npm run dev
```

The dev server runs on http://localhost:5173. It calls the Durnius.Web API
at `VITE_API_URL` (default `http://localhost:8080`); copy `.env.example` to
`.env.local` and adjust it to match the port your backend is on.

## Scripts

- `npm run dev` starts the dev server
- `npm run build` type-checks and emits a production build into
  `../Durnius.Web/wwwroot`, where ASP.NET Core serves it
- `npm run typecheck` type-checks without building
- `npm run preview` serves the production build locally

## Routes

- `/login` — log in with an existing account
- `/register` — create a new account (logs you in immediately)
- `/lobby` — protected; game tiles for both modes plus the daily leaderboard
- `/karas` — protected; joins the Karas queue and plays the match
- `/durnius` — protected; joins the Durnius queue and plays the match
- `/account` — protected; profile and password change

## Auth handling

The JWT is stored in `localStorage` (`src/lib/auth.ts`). Any API response
with a 401/403 (except bad credentials on `/api/login` and `/api/register`)
clears the token and redirects to `/login`; the same happens when a SignalR
handshake is rejected or the token reaches its `exp` time.

All SignalR connection handling, hub messages and DTOs live in
`src/lib/protocol.ts`; each game page only contains its own client logic and
UI. Card types and rule helpers are in `src/lib/cards.ts`.
