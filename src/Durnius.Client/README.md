# Durnius.Client

React (JavaScript) app for Durnius, built with Vite.

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
- `npm run build` creates a production build in `dist/`
- `npm run preview` serves the production build locally

## Routes

- `/login` — log in with an existing account
- `/register` — create a new account
- `/lobby` — protected; requires a valid stored JWT, otherwise redirects to `/login`. Lists the games returned by the API.