import { useCallback, useRef, useState } from 'react';
import { newId } from '../lib/ids';

/**
 * Wraps a mutating call so that one user intent maps to exactly one idempotency key.
 *  - Re-entrancy guard: a second click while a call is in flight is ignored (double-click safe).
 *  - The key is reused when the same intent (same arguments) is retried after a failure, because the
 *    failed attempt may actually have been applied (timeout). The server then replays the original result.
 *  - The key is renewed after success, or when the arguments change (a different intent).
 */
export function useIdempotentAction<A extends unknown[], R>(action: (key: string, ...args: A) => Promise<R>) {
  const keyRef = useRef<{ key: string; intent: string } | null>(null);
  const inFlight = useRef(false);
  const actionRef = useRef(action);
  actionRef.current = action;
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<unknown>(null);

  const run = useCallback(async (...args: A): Promise<R | undefined> => {
    if (inFlight.current) return undefined;
    const intent = JSON.stringify(args);
    if (!keyRef.current || keyRef.current.intent !== intent) keyRef.current = { key: newId(), intent };
    inFlight.current = true;
    setPending(true);
    setError(null);
    try {
      const result = await actionRef.current(keyRef.current.key, ...args);
      keyRef.current = null;
      return result;
    } catch (e) {
      setError(e);
      return undefined;
    } finally {
      inFlight.current = false;
      setPending(false);
    }
  }, []);

  return { run, pending, error, clearError: useCallback(() => setError(null), []) };
}
