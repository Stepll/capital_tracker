import { createContext, useContext, useMemo, useState, type ReactNode } from "react";
import { apiClient, TOKEN_STORAGE_KEY } from "../api/client";

interface AuthContextValue {
  isAuthenticated: boolean;
  login: (email: string, password: string) => Promise<void>;
  logout: () => void;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [token, setToken] = useState<string | null>(() =>
    localStorage.getItem(TOKEN_STORAGE_KEY),
  );

  const login = async (email: string, password: string) => {
    const { data } = await apiClient.post<{ token: string }>("/auth/login", {
      email,
      password,
    });
    localStorage.setItem(TOKEN_STORAGE_KEY, data.token);
    setToken(data.token);
  };

  const logout = () => {
    localStorage.removeItem(TOKEN_STORAGE_KEY);
    setToken(null);
  };

  const value = useMemo(
    () => ({ isAuthenticated: token !== null, login, logout }),
    [token],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used within AuthProvider");
  return ctx;
}
