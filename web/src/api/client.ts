import { ApiError, type ProblemDetails } from './errors';
import { newId } from '../lib/ids';
import type {
  AuditEntry, Cart, DevToken, Franchise, Order, OrderQuery, Page, PlacedOrder, Product, ProductPage,
  ProductQuery, Role, UpdateProductRequest,
} from './types';

const BASE = (import.meta.env?.VITE_API_BASE as string | undefined) ?? '/api/v1';
const TIMEOUT_MS = 15_000;

let authToken: string | null = null;
/** Token lives in memory here; AuthContext also mirrors it to sessionStorage for reloads. */
export function setAuthToken(token: string | null): void { authToken = token; }

interface Options {
  method?: string;
  body?: unknown;
  headers?: Record<string, string>;
  idempotencyKey?: string;
}
interface Raw<T> { data: T; status: number; headers: Headers }

function query(params: Record<string, string | number | undefined | null>): string {
  const search = new URLSearchParams();
  for (const [k, v] of Object.entries(params)) if (v !== undefined && v !== null && v !== '') search.set(k, String(v));
  const s = search.toString();
  return s ? `?${s}` : '';
}

async function send<T>(path: string, opts: Options = {}): Promise<Raw<T>> {
  const correlationId = newId();
  const headers: Record<string, string> = {
    Accept: 'application/json, application/problem+json',
    'X-Correlation-Id': correlationId,
    ...opts.headers,
  };
  if (authToken) headers.Authorization = `Bearer ${authToken}`;
  if (opts.idempotencyKey) headers['Idempotency-Key'] = opts.idempotencyKey;
  if (opts.body !== undefined) headers['Content-Type'] = 'application/json';

  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), TIMEOUT_MS);
  try {
    let res: Response;
    let text: string;
    try {
      res = await fetch(`${BASE}${path}`, {
        method: opts.method ?? 'GET',
        headers,
        body: opts.body === undefined ? undefined : JSON.stringify(opts.body),
        signal: controller.signal,
      });
      text = await res.text();
    } catch {
      const timedOut = controller.signal.aborted;
      throw new ApiError({
        status: 0,
        code: timedOut ? 'timeout' : 'network',
        detail: timedOut ? 'The request timed out.' : 'The network request failed.',
        correlationId,
      });
    }

    let json: unknown;
    if (text) {
      try { json = JSON.parse(text); } catch { json = undefined; }
    }

    if (!res.ok) {
      const problem = (json ?? {}) as ProblemDetails;
      const retryAfter = Number.parseInt(res.headers.get('Retry-After') ?? '', 10);
      throw new ApiError({
        status: res.status,
        code: problem.code,
        title: problem.title,
        detail: problem.detail,
        errors: problem.errors,
        failedLines: problem.failedLines,
        retryAfterSeconds: Number.isFinite(retryAfter) ? retryAfter : null,
        correlationId: problem.correlationId ?? res.headers.get('X-Correlation-Id') ?? correlationId,
      });
    }
    return { data: json as T, status: res.status, headers: res.headers };
  } finally {
    clearTimeout(timer);
  }
}

const get = async <T>(path: string) => (await send<T>(path)).data;

/** The only module that knows URLs, headers and wire formats. Components import `api` and nothing else. */
export const api = {
  devToken: async (userId: string, role: Role) =>
    (await send<DevToken>('/dev/token', { method: 'POST', body: { userId, role } })).data,

  franchises: () => get<Franchise[]>('/franchises'),
  products: (q: ProductQuery = {}) =>
    get<ProductPage>(`/products${query({ q: q.q, type: q.type, franchise: q.franchise, pageSize: q.pageSize, pageToken: q.pageToken })}`),
  product: (id: string) => get<Product>(`/products/${encodeURIComponent(id)}`),

  updateProduct: async (id: string, etag: string, body: UpdateProductRequest) =>
    (await send<Product>(`/admin/products/${encodeURIComponent(id)}`, { method: 'PATCH', body, headers: { 'If-Match': etag } })).data,
  audit: (id: string, pageSize?: number, pageToken?: string) =>
    get<Page<AuditEntry>>(`/admin/products/${encodeURIComponent(id)}/audit${query({ pageSize, pageToken })}`),

  cart: () => get<Cart>('/cart'),
  addToCart: async (key: string, productId: string, quantity: number) =>
    (await send<Cart>('/cart/items', { method: 'POST', body: { productId, quantity }, idempotencyKey: key })).data,
  setQuantity: async (key: string, productId: string, quantity: number) =>
    (await send<Cart>(`/cart/items/${encodeURIComponent(productId)}`, { method: 'PUT', body: { quantity }, idempotencyKey: key })).data,
  removeFromCart: async (key: string, productId: string) =>
    (await send<Cart>(`/cart/items/${encodeURIComponent(productId)}`, { method: 'DELETE', idempotencyKey: key })).data,
  acceptChanges: async (key: string) =>
    (await send<Cart>('/cart/accept-changes', { method: 'POST', idempotencyKey: key })).data,

  placeOrder: async (orderId: string, expectedTotal: number): Promise<PlacedOrder> => {
    const res = await send<Order>(`/orders/${encodeURIComponent(orderId)}`, { method: 'PUT', body: { expectedTotal } });
    return { order: res.data, replayed: res.status === 200 || res.headers.get('Idempotent-Replayed') === 'true' };
  },
  orders: (q: OrderQuery = {}) =>
    get<Page<Order>>(`/orders${query({ from: q.from, to: q.to, pageSize: q.pageSize, pageToken: q.pageToken })}`),
  order: (id: string) => get<Order>(`/orders/${encodeURIComponent(id)}`),
};

export type Api = typeof api;
