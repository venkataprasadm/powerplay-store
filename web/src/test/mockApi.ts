import { vi } from 'vitest';

/** One shared mock of the api object. Tests mock the client module boundary: vi.mock('../api/client', clientMock). */
export const apiMock = {
  devToken: vi.fn(),
  franchises: vi.fn(),
  products: vi.fn(),
  product: vi.fn(),
  updateProduct: vi.fn(),
  audit: vi.fn(),
  cart: vi.fn(),
  addToCart: vi.fn(),
  setQuantity: vi.fn(),
  removeFromCart: vi.fn(),
  acceptChanges: vi.fn(),
  placeOrder: vi.fn(),
  orders: vi.fn(),
  order: vi.fn(),
};

export const clientMock = () => ({ api: apiMock, setAuthToken: vi.fn() });

export function resetApiMock(): void {
  for (const fn of Object.values(apiMock)) fn.mockReset();
}
