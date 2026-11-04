import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from 'react';
import { api, setAuthToken } from '../api/client';
import type { Role } from '../api/types';

export interface SessionUser { userId: string; role: Role; accessToken: string; expiresAtUtc: string }

export const DEV_USERS: ReadonlyArray<{ userId: string; label: string; role: Role }> = [
  { userId: 'asha', label: 'Asha', role: 'shopper' },
  { userId: 'ravi', label: 'Ravi', role: 'shopper' },
  { userId: 'admin', label: 'Admin', role: 'admin' },
];

const STORAGE_KEY = 'ipl.auth';

function restore(): SessionUser | null {
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY);
    if (!raw) return null;
    const user = JSON.parse(raw) as SessionUser;
    return new Date(user.expiresAtUtc).getTime() > Date.now() ? user : null;
  } catch { return null; }
}
function persist(user: SessionUser | null): void {
  try {
    if (user) sessionStorage.setItem(STORAGE_KEY, JSON.stringify(user));
    else sessionStorage.removeItem(STORAGE_KEY);
  } catch { /* ignore: in-memory session still works */ }
}

interface AuthContextValue {
  user: SessionUser | null;
  busy: boolean;
  error: unknown;
  signIn: (userId: string) => Promise<void>;
  signOut: () => void;
}
const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  // Set the token synchronously during init so child effects (cart load) already see it.
  const [user, setUser] = useState<SessionUser | null>(() => {
    const restored = restore();
    setAuthToken(restored?.accessToken ?? null);
    return restored;
  });
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<unknown>(null);

  const signIn = useCallback(async (userId: string) => {
    const known = DEV_USERS.find((u) => u.userId === userId);
    if (!known) return;
    setBusy(true);
    setError(null);
    try {
      const t = await api.devToken(known.userId, known.role);
      const next: SessionUser = { userId: t.userId, role: t.role, accessToken: t.accessToken, expiresAtUtc: t.expiresAtUtc };
      setAuthToken(next.accessToken);
      persist(next);
      setUser(next);
    } catch (e) {
      setError(e);
    } finally {
      setBusy(false);
    }
  }, []);

  const signOut = useCallback(() => {
    setAuthToken(null);
    persist(null);
    setUser(null);
  }, []);

  const value = useMemo(() => ({ user, busy, error, signIn, signOut }), [user, busy, error, signIn, signOut]);
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used inside AuthProvider');
  return ctx;
}
