import { useEffect, useRef, useState } from 'react';

export interface AsyncState<T> {
  data: T | undefined;
  error: unknown;
  loading: boolean;
  reload: () => void;
}

/** Loads data when `deps` change. Stale responses from superseded requests are ignored. */
export function useAsync<T>(load: () => Promise<T>, deps: readonly unknown[]): AsyncState<T> {
  const [state, setState] = useState<{ data?: T; error?: unknown; loading: boolean }>({ loading: true });
  const [tick, setTick] = useState(0);
  const latest = useRef(0);
  const loadRef = useRef(load);
  loadRef.current = load;

  useEffect(() => {
    const id = ++latest.current;
    setState((s) => ({ data: s.error ? undefined : s.data, loading: true }));
    loadRef.current().then(
      (data) => { if (id === latest.current) setState({ data, loading: false }); },
      (error: unknown) => { if (id === latest.current) setState({ error, loading: false }); },
    );
    return () => { latest.current++; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [...deps, tick]);

  return { data: state.data, error: state.error, loading: state.loading, reload: () => setTick((t) => t + 1) };
}
