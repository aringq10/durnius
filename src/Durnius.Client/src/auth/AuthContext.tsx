import { createContext, useContext, useMemo, useState } from "react";
import type { ReactNode } from "react";

const TOKEN_KEY = "durnius.token";

export interface AuthContextValue {
  token: string | null;
  isAuthenticated: boolean;
  storeToken: (nextToken: string | null) => void;
  logout: () => void;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [token, setTokenState] = useState<string | null>(() =>
    localStorage.getItem(TOKEN_KEY)
  );

  const value = useMemo<AuthContextValue>(() => {
    const storeToken = (nextToken: string | null) => {
      if (nextToken) {
        localStorage.setItem(TOKEN_KEY, nextToken);
      } else {
        localStorage.removeItem(TOKEN_KEY);
      }
      setTokenState(nextToken);
    };

    return {
      token,
      isAuthenticated: Boolean(token),
      storeToken,
      logout: () => storeToken(null),
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
