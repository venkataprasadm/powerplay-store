import type { ReactNode } from 'react';
import { ApiError, describeError } from '../api/errors';

interface Props { error: unknown; onRetry?: () => void; children?: ReactNode }

/** Announced to screen readers (role=alert). Shows failing lines for out-of-stock rejections. */
export function ErrorBanner({ error, onRetry, children }: Props) {
  const api = error instanceof ApiError ? error : null;
  return (
    <div role="alert" className="banner banner-error">
      <p>{describeError(error)}</p>
      {api && api.failedLines.length > 0 && (
        <ul>
          {api.failedLines.map((l) => (
            <li key={l.productId}>
              {l.productName}: you asked for {l.requested}, {l.available === 0 ? 'none' : `only ${l.available}`} available.
            </li>
          ))}
        </ul>
      )}
      {children}
      {api?.correlationId && <p className="muted">Reference: {api.correlationId}</p>}
      {onRetry && <button type="button" onClick={onRetry}>Retry</button>}
    </div>
  );
}
