import type { ReactNode } from 'react';
import type { AsyncState } from '../hooks/useAsync';
import { ApiError } from '../api/errors';
import { ErrorBanner } from './ErrorBanner';

interface Props<T> { state: AsyncState<T>; what: string; children: (data: T) => ReactNode; placeholder?: ReactNode }

/** Uniform loading / failure (with Retry) / success rendering for anything fetched. `placeholder` is an optional visual skeleton. */
export function AsyncView<T>({ state, what, children, placeholder }: Props<T>) {
  if (state.error) {
    const retryable = !(state.error instanceof ApiError) || state.error.status !== 404;
    return <ErrorBanner error={state.error} onRetry={retryable ? state.reload : undefined} />;
  }
  if (state.data === undefined) {
    if (!placeholder) return <p role="status" className="loading">Loading {what}…</p>;
    return <div><p role="status" className="visually-hidden">Loading {what}…</p><div aria-hidden="true">{placeholder}</div></div>;
  }
  return <div aria-busy={state.loading}>{children(state.data)}</div>;
}
