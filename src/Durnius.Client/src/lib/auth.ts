// JWT storage and session helpers shared by the API layer, the SignalR
// client and the React auth context. Keeping the token outside React lets
// non-React code (fetch wrappers, hub connections) read it directly.

const TOKEN_KEY = "durnius.token";

function readStoredToken(): string | null {
  try {
    return localStorage.getItem(TOKEN_KEY);
  } catch {
    return null;
  }
}

let token: string | null = readStoredToken();
const listeners = new Set<() => void>();

export function getToken(): string | null {
  return token;
}

export function setToken(next: string | null): void {
  token = next;
  try {
    if (next) {
      localStorage.setItem(TOKEN_KEY, next);
    } else {
      localStorage.removeItem(TOKEN_KEY);
    }
  } catch {
    // Storage may be unavailable (e.g. private browsing); the in-memory
    // token still works for the rest of the session.
  }
  listeners.forEach((listener) => listener());
}

export function subscribeToken(listener: () => void): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

export interface SessionInfo {
  username: string | null;
  /** Expiry as a Unix timestamp in milliseconds, when the token has `exp`. */
  expiresAt: number | null;
}

/** Decodes the public claims of a JWT. The API verifies it; we only read it. */
export function readSession(jwt: string | null): SessionInfo {
  const empty: SessionInfo = { username: null, expiresAt: null };
  if (!jwt) return empty;
  try {
    const payload = jwt.split(".")[1];
    const base64 = payload.replace(/-/g, "+").replace(/_/g, "/");
    const padded = base64.padEnd(Math.ceil(base64.length / 4) * 4, "=");
    const bytes = Uint8Array.from(atob(padded), (char) => char.charCodeAt(0));
    const claims = JSON.parse(new TextDecoder().decode(bytes)) as {
      name?: unknown;
      exp?: unknown;
    };
    return {
      username: typeof claims.name === "string" ? claims.name : null,
      expiresAt: typeof claims.exp === "number" ? claims.exp * 1000 : null,
    };
  } catch {
    return empty;
  }
}

// --- "not logged in" fan-out -----------------------------------------------

let unauthorizedHandler: (() => void) | null = null;
let sessionExpired = false;

/** Registered by AuthProvider; navigates to /login after the token is gone. */
export function setUnauthorizedHandler(handler: (() => void) | null): void {
  unauthorizedHandler = handler;
}

/**
 * Clears the JWT and redirects to /login. Called whenever any endpoint or
 * hub handshake answers with a "not logged in" status (401/403), and when
 * the stored token reaches its expiry time.
 */
export function notifyUnauthorized(): void {
  sessionExpired = true;
  setToken(null);
  unauthorizedHandler?.();
}

/**
 * Whether the last logout was forced (expired/invalid session). Both the
 * auth handler and the RequireAuth guard may perform the redirect, so both
 * read this flag to show the "session expired" notice on the login page.
 */
export function isSessionExpired(): boolean {
  return sessionExpired;
}

/** Called once the login page has picked up the notice. */
export function clearSessionExpired(): void {
  sessionExpired = false;
}
