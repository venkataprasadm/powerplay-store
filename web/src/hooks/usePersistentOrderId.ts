import { useCallback, useState } from 'react';
import { newId } from '../lib/ids';

const keyFor = (userId: string) => `ipl.orderId.${userId}`;

function read(userId: string): string | null {
  try { return sessionStorage.getItem(keyFor(userId)); } catch { return null; }
}
function write(userId: string, value: string | null): void {
  try {
    if (value) sessionStorage.setItem(keyFor(userId), value);
    else sessionStorage.removeItem(keyFor(userId));
  } catch { /* storage unavailable: the id then only lives in memory, which still protects double-clicks */ }
}

/**
 * One order id per checkout attempt. It survives failures and page reloads (so retrying after a
 * timeout hits PUT /orders/{sameId}) and is only renewed after the order is confirmed.
 */
export function usePersistentOrderId(userId: string) {
  const [orderId, setOrderId] = useState(() => {
    const existing = read(userId);
    if (existing) return existing;
    const fresh = newId();
    write(userId, fresh);
    return fresh;
  });
  const renew = useCallback(() => {
    const fresh = newId();
    write(userId, fresh);
    setOrderId(fresh);
  }, [userId]);
  return { orderId, renew };
}
