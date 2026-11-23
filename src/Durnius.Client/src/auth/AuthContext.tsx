import {
  createContext,
  useContext,
  useEffect,
  useMemo,
  useSyncExternalStore,
} from "react";
import type { ReactNode } from "react";
import { useNavigate } from "react-router-dom";
import {
  getToken,
  notifyUnauthorized,
  readSession,
  setToken,
  setUnauthorizedHandler,
  subscribeToken,
} from "../lib/auth";

export interface AuthContextValue {
  token: string | null;
  username: string | null;
  isAuthenticated: boolean;
  login: (token: string) => void;
  logout: () => void;
}

const AuthContext = createContext<AuthContextValue | null>(null);

/** setTimeout delays must fit in a signed 32-bit integer. */
const MAX_TIMER_MS = 2 ** 31 - 1;

export function AuthProvider({ children }: { children: ReactNode }) {
  const token = useSyncExternalStore(subscribeToken, getToken);
  const navigate = useNavigate();

  // Any API call or hub handshake that reports "not logged in" ends up here:
  // the token has already been cleared, we only need to move to /login.
  useEffect(() => {
    setUnauthorizedHandler(() =>
      navigate("/login", { replace: true, state: { expired: true } })
    );
    return () => setUnauthorizedHandler(null);
  }, [navigate]);

  // Log out exactly when the JWT expires, even in the middle of a match.
  useEffect(() => {
    if (!token) return;
    const { expiresAt } = readSession(token);
    if (expiresAt === null) return;

    let timer: number | undefined;
    const arm = () => {
      const remaining = expiresAt - Date.now();
      if (remaining <= 0) {
        notifyUnauthorized();
      } else {
        timer = window.setTimeout(arm, Math.min(remaining, MAX_TIMER_MS));
      }
    };
    arm();
    return () => window.clearTimeout(timer);
  }, [token]);

  const value = useMemo<AuthContextValue>(() => {
    const { username } = readSession(token);
    return {
      token,
      username,
      isAuthenticated: token !== null,
      login: (next) => setToken(next),
      // JWT auth is stateless: logging out only means discarding the token.
      // Protected routes redirect to /login on their own.
      logout: () => setToken(null),
    };
  }, [token]);

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error("useAuth must be used within an AuthProvider.");
  }
  return context;
}
