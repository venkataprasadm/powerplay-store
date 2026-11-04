import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { api } from '../api/client';
import type { Cart, CartLine } from '../api/types';
import { useAuth } from './AuthContext';

/** The most recent successful add, for the header "+N" bump and the confirmation toast. `seq` changes on every add. */
export interface AddedEvent { seq: number; quantity: number; line: CartLine; cart: Cart }

interface CartContextValue {
  cart: Cart | null;
  loading: boolean;
  error: unknown;
  lastAdded: AddedEvent | null;
  dismissAdded: () => void;
  reload: () => Promise<void>;
  add: (key: string, productId: string, quantity: number) => Promise<Cart>;
  setQuantity: (key: string, productId: string, quantity: number) => Promise<Cart>;
  remove: (key: string, productId: string) => Promise<Cart>;
  accept: (key: string) => Promise<Cart>;
}
const CartContext = createContext<CartContextValue | null>(null);

/** Server is the source of truth for the cart; this context only caches the last response. */
export function CartProvider({ children }: { children: ReactNode }) {
  const { user } = useAuth();
  const userId = user?.userId ?? null;
  const [cart, setCart] = useState<Cart | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<unknown>(null);
  const [lastAdded, setLastAdded] = useState<AddedEvent | null>(null);

  const reload = useCallback(async () => {
    if (!userId) { setCart(null); return; }
    setLoading(true);
    setError(null);
    try { setCart(await api.cart()); } catch (e) { setError(e); } finally { setLoading(false); }
  }, [userId]);

  useEffect(() => { setCart(null); setLastAdded(null); void reload(); }, [reload]);

  const keep = useCallback(async (call: Promise<Cart>) => { const next = await call; setCart(next); return next; }, []);

  const add = useCallback(async (key: string, productId: string, quantity: number) => {
    const next = await keep(api.addToCart(key, productId, quantity));
    const line = next.lines.find((l) => l.productId === productId);
    if (line) setLastAdded((prev) => ({ seq: (prev?.seq ?? 0) + 1, quantity, line, cart: next }));
    return next;
  }, [keep]);

  const dismissAdded = useCallback(() => setLastAdded(null), []);

  const value = useMemo<CartContextValue>(() => ({
    cart, loading, error, lastAdded, dismissAdded, reload, add,
    setQuantity: (key, productId, quantity) => keep(api.setQuantity(key, productId, quantity)),
    remove: (key, productId) => keep(api.removeFromCart(key, productId)),
    accept: (key) => keep(api.acceptChanges(key)),
  }), [cart, loading, error, lastAdded, dismissAdded, reload, add, keep]);

  return <CartContext.Provider value={value}>{children}</CartContext.Provider>;
}

export function useCart(): CartContextValue {
  const ctx = useContext(CartContext);
  if (!ctx) throw new Error('useCart must be used inside CartProvider');
  return ctx;
}
