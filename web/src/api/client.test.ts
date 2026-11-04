import { api, setAuthToken } from './client';
import { ApiError, describeError, fieldErrors } from './errors';

const json = (status: number, body: unknown, headers: Record<string, string> = {}) =>
  new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/problem+json', ...headers } });

afterEach(() => { vi.restoreAllMocks(); vi.useRealTimers(); setAuthToken(null); });

describe('api client', () => {
  it('sends the idempotency key, bearer token and a correlation id on cart mutations', async () => {
    setAuthToken('tok');
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockResolvedValue(json(200, { lines: [] }));
    await api.addToCart('key-123', 'mi-jersey', 2);
    const [url, init] = fetchMock.mock.calls[0];
    const headers = init!.headers as Record<string, string>;
    expect(url).toBe('/api/v1/cart/items');
    expect(init!.method).toBe('POST');
    expect(headers['Idempotency-Key']).toBe('key-123');
    expect(headers.Authorization).toBe('Bearer tok');
    expect(headers['X-Correlation-Id']).toBeTruthy();
    expect(JSON.parse(init!.body as string)).toEqual({ productId: 'mi-jersey', quantity: 2 });
  });

  it('maps problem+json to a typed ApiError with failed lines', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(json(409, {
      code: 'out_of_stock', detail: 'x', failedLines: [{ productId: 'kkr', productName: 'KKR Signed Photo', requested: 1, available: 0 }],
    }));
    const err = await api.placeOrder('o1', 10).catch((e: unknown) => e);
    expect(err).toBeInstanceOf(ApiError);
    const e = err as ApiError;
    expect(e.status).toBe(409);
    expect(e.code).toBe('out_of_stock');
    expect(e.failedLines[0].productName).toBe('KKR Signed Photo');
    expect(describeError(e)).toMatch(/cart was kept/i);
  });

  it('reads Retry-After on 429', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(json(429, { code: 'rate_limited' }, { 'Retry-After': '7' }));
    const e = (await api.products().catch((x: unknown) => x)) as ApiError;
    expect(e.retryAfterSeconds).toBe(7);
    expect(e.retryable).toBe(true);
    expect(describeError(e)).toContain('7 seconds');
  });

  it('exposes validation errors per field, case-insensitively', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(json(400, { code: 'validation_failed', errors: { Price: ['Too many decimals'] } }));
    const e = await api.updateProduct('p', '"e"', { price: 1.234, reason: 'r' }).catch((x: unknown) => x);
    expect(fieldErrors(e, 'price')).toEqual(['Too many decimals']);
  });

  it('sends If-Match on admin updates', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockResolvedValue(json(200, {}));
    await api.updateProduct('csk-cap', '"abc"', { price: 549, reason: 'r' });
    const init = fetchMock.mock.calls[0][1]!;
    expect((init.headers as Record<string, string>)['If-Match']).toBe('"abc"');
    expect(init.method).toBe('PATCH');
  });

  it('flags a replayed order (200) versus a new one (201)', async () => {
    const o = { id: 'o1' };
    vi.spyOn(globalThis, 'fetch').mockResolvedValueOnce(json(201, o)).mockResolvedValueOnce(json(200, o));
    expect((await api.placeOrder('o1', 1)).replayed).toBe(false);
    expect((await api.placeOrder('o1', 1)).replayed).toBe(true);
  });

  it('turns a network failure into status 0', async () => {
    vi.spyOn(globalThis, 'fetch').mockRejectedValue(new TypeError('offline'));
    const e = (await api.cart().catch((x: unknown) => x)) as ApiError;
    expect(e.status).toBe(0);
    expect(e.code).toBe('network');
  });

  it('aborts slow requests with a timeout error', async () => {
    vi.useFakeTimers();
    vi.spyOn(globalThis, 'fetch').mockImplementation((_u, init) =>
      new Promise((_res, rej) => init!.signal!.addEventListener('abort', () => rej(new DOMException('Aborted', 'AbortError')))));
    const pending = api.cart().catch((x: unknown) => x);
    await vi.advanceTimersByTimeAsync(15_001);
    const e = (await pending) as ApiError;
    expect(e.code).toBe('timeout');
  });
});
