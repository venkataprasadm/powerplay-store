export interface FailedLine { productId: string; productName: string; requested: number; available: number }

export interface ProblemDetails {
  type?: string; title?: string; status?: number; detail?: string; code?: string;
  errors?: Record<string, string[]>; failedLines?: FailedLine[]; correlationId?: string; traceId?: string;
}

interface ApiErrorInit {
  status: number; code?: string; title?: string; detail?: string;
  errors?: Record<string, string[]>; failedLines?: FailedLine[];
  retryAfterSeconds?: number | null; correlationId?: string | null;
}

/** Typed error for every non-2xx (status >= 400) and transport failure (status 0). */
export class ApiError extends Error {
  readonly status: number;
  readonly code: string;
  readonly title: string | undefined;
  readonly detail: string | undefined;
  readonly errors: Record<string, string[]>;
  readonly failedLines: FailedLine[];
  readonly retryAfterSeconds: number | null;
  readonly correlationId: string | null;

  constructor(init: ApiErrorInit) {
    super(init.detail ?? init.title ?? `Request failed (${init.status})`);
    this.name = 'ApiError';
    this.status = init.status;
    this.code = init.code ?? 'unknown';
    this.title = init.title;
    this.detail = init.detail;
    this.errors = init.errors ?? {};
    this.failedLines = init.failedLines ?? [];
    this.retryAfterSeconds = init.retryAfterSeconds ?? null;
    this.correlationId = init.correlationId ?? null;
  }

  /** Worth offering a Retry button: transport failure, throttling or server-side trouble. */
  get retryable(): boolean {
    return this.status === 0 || this.status === 429 || this.status >= 500;
  }
}

/** Field-level validation messages; key matching is case-insensitive and tolerates "$.price" style paths. */
export function fieldErrors(error: unknown, field: string): string[] {
  if (!(error instanceof ApiError)) return [];
  const wanted = field.toLowerCase();
  return Object.entries(error.errors)
    .filter(([key]) => {
      const k = key.toLowerCase();
      return k === wanted || k.endsWith(`.${wanted}`);
    })
    .flatMap(([, messages]) => messages);
}

/** Human readable message for any thrown value. Never leaks stack traces or raw JSON. */
export function describeError(error: unknown): string {
  if (!(error instanceof ApiError)) return 'Something went wrong. Please try again.';
  switch (error.status) {
    case 0:
      return error.code === 'timeout'
        ? 'The server took too long to respond. Please retry.'
        : 'Could not reach the server. Check your connection and retry.';
    case 400:
      return error.code === 'validation_failed'
        ? 'Please correct the highlighted fields.'
        : (error.detail ?? 'The request was not valid.');
    case 401: return 'Please sign in to continue.';
    case 403: return 'You do not have permission to do that.';
    case 404: return 'We could not find that item.';
    case 412:
      return 'This item was changed by someone else after you loaded it. Reload to see the latest version, then try again.';
    case 428: return 'The latest version of this item is required. Reload it and try again.';
    case 429: {
      const wait = error.retryAfterSeconds;
      return wait ? `You are doing that too quickly. Try again in ${wait} seconds.` : 'You are doing that too quickly. Try again shortly.';
    }
    case 409:
      switch (error.code) {
        case 'out_of_stock':
          return 'Some items are out of stock. No order was created and your cart was kept.';
        case 'insufficient_stock':
          return error.detail ?? 'There is not enough stock for that quantity.';
        case 'cart_changed':
          return 'Your cart total changed. Review the new total before checking out.';
        case 'idempotency_key_reused':
          return 'That change could not be applied safely. Please try again.';
        case 'cart_empty': return 'Your cart is empty.';
        case 'order_id_in_use': return 'That order reference is already used. Please start checkout again.';
        default: return error.detail ?? 'That change conflicts with the current state. Reload and try again.';
      }
    case 503: return 'The service is temporarily unavailable. Please retry shortly.';
    default:
      return error.status >= 500 ? 'The server had a problem. Please retry.' : (error.detail ?? 'The request failed.');
  }
}
