import { createContext, useContext, useState, useEffect, useCallback, type ReactNode } from 'react';
import { jwtDecode } from 'jwt-decode';

interface User {
  id: string;
  email: string;
  displayName: string;
  role: string;
}

interface AuthContextType {
  user: User | null;
  token: string | null;
  isAuthenticated: boolean;
  isAdmin: boolean;
  isLoading: boolean;
  login: (email: string, password: string) => Promise<void>;
  register: (email: string, password: string, displayName: string) => Promise<void>;
  logout: () => Promise<void>;
  getToken: () => string | null;
}

interface JwtPayload {
  sub: string;
  email: string;
  display_name: string;
  role: string;
  exp: number;
}

interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
  user: User;
}

const AuthContext = createContext<AuthContextType | null>(null);

const TOKEN_KEY = 'gpx_access_token';
const REFRESH_KEY = 'gpx_refresh_token';

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null);
  const [token, setToken] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  const clearAuth = useCallback(() => {
    setUser(null);
    setToken(null);
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(REFRESH_KEY);
  }, []);

  const setAuth = useCallback((data: AuthResponse) => {
    setToken(data.accessToken);
    setUser(data.user);
    localStorage.setItem(TOKEN_KEY, data.accessToken);
    localStorage.setItem(REFRESH_KEY, data.refreshToken);
  }, []);

  const isTokenExpired = useCallback((t: string): boolean => {
    try {
      const decoded = jwtDecode<JwtPayload>(t);
      return decoded.exp * 1000 < Date.now() - 30_000; // 30s margin
    } catch {
      return true;
    }
  }, []);

  const refreshAccessToken = useCallback(async (): Promise<string | null> => {
    const refreshToken = localStorage.getItem(REFRESH_KEY);
    if (!refreshToken) return null;

    try {
      const res = await fetch('/api/auth/refresh', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken }),
      });

      if (!res.ok) {
        clearAuth();
        return null;
      }

      const data: AuthResponse = await res.json();
      setToken(data.accessToken);
      localStorage.setItem(TOKEN_KEY, data.accessToken);
      localStorage.setItem(REFRESH_KEY, data.refreshToken);
      return data.accessToken;
    } catch {
      clearAuth();
      return null;
    }
  }, [clearAuth]);

  const getToken = useCallback((): string | null => {
    const stored = localStorage.getItem(TOKEN_KEY);
    if (!stored) return null;
    return stored;
  }, []);

  // Initialize auth state on mount
  useEffect(() => {
    const init = async () => {
      const stored = localStorage.getItem(TOKEN_KEY);
      if (!stored) {
        setIsLoading(false);
        return;
      }

      if (isTokenExpired(stored)) {
        const newToken = await refreshAccessToken();
        if (!newToken) {
          setIsLoading(false);
          return;
        }
      } else {
        setToken(stored);
      }

      // Fetch user info
      const currentToken = localStorage.getItem(TOKEN_KEY);
      if (!currentToken) {
        setIsLoading(false);
        return;
      }

      try {
        const res = await fetch('/api/auth/me', {
          headers: { Authorization: `Bearer ${currentToken}` },
        });
        if (res.ok) {
          const userData: User = await res.json();
          setUser(userData);
          setToken(currentToken);
        } else {
          clearAuth();
        }
      } catch {
        clearAuth();
      }

      setIsLoading(false);
    };

    init();
  }, [clearAuth, isTokenExpired, refreshAccessToken]);

  const login = async (email: string, password: string) => {
    const res = await fetch('/api/auth/login', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, password }),
    });

    if (!res.ok) {
      const data = await res.json().catch(() => ({}));
      throw new Error(data.code || 'LOGIN_FAILED');
    }

    const data: AuthResponse = await res.json();
    setAuth(data);
  };

  const register = async (email: string, password: string, displayName: string) => {
    const res = await fetch('/api/auth/register', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, password, displayName }),
    });

    if (!res.ok) {
      const data = await res.json().catch(() => ({}));
      throw new Error(data.code || 'REGISTER_FAILED');
    }

    const data: AuthResponse = await res.json();
    setAuth(data);
  };

  const logout = async () => {
    const currentToken = localStorage.getItem(TOKEN_KEY);
    if (currentToken) {
      try {
        await fetch('/api/auth/logout', {
          method: 'POST',
          headers: { Authorization: `Bearer ${currentToken}` },
        });
      } catch {
        // Ignore logout API errors
      }
    }
    clearAuth();
  };

  return (
    <AuthContext.Provider
      value={{
        user,
        token,
        isAuthenticated: !!user,
        isAdmin: user?.role === 'Admin',
        isLoading,
        login,
        register,
        logout,
        getToken,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used within AuthProvider');
  return ctx;
}
