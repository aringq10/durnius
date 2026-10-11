import { createContext, useContext, useMemo, useState } from "react";

const TOKEN_KEY = "durnius.token";

const AuthContext = createContext(null);

export function AuthProvider({ children }) {
  const [token, setTokenState] = useState(() => localStorage.getItem(TOKEN_KEY));

  const value = useMemo(() => {
    const storeToken = (nextToken) => {
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

export function useAuth() {
  return useContext(AuthContext);
}