import type { Cart, CartLine, Order, Product } from '../api/types';

export const product = (over: Partial<Product> = {}): Product => ({
  id: 'mi-jersey', name: 'MI Jersey', type: 'Jersey', franchiseId: 'mi', franchiseName: 'Mumbai Indians',
  description: 'Official replica jersey', price: 1999, stock: 50, inStock: true, stockStatus: 'InStock', etag: '"e1"', ...over,
});

export const line = (over: Partial<CartLine> = {}): CartLine => ({
  productId: 'mi-jersey', productName: 'MI Jersey', quantity: 2, acceptedUnitPrice: 1999, currentUnitPrice: 1999,
  lineTotal: 3998, priceChanged: false, availableStock: 50, stockStatus: 'InStock', insufficientStock: false, ...over,
});

export const cartOf = (lines: CartLine[], over: Partial<Cart> = {}): Cart => {
  const total = lines.reduce((s, l) => s + l.lineTotal, 0);
  const changed = lines.some((l) => l.priceChanged);
  return {
    lines, itemCount: lines.reduce((s, l) => s + l.quantity, 0), total,
    acceptedTotal: lines.reduce((s, l) => s + l.quantity * l.acceptedUnitPrice, 0),
    requiresAcceptance: changed, canCheckout: lines.length > 0 && !changed && !lines.some((l) => l.insufficientStock),
    shipping: 0, grandTotal: total, ...over,
  };
};

export const emptyCart = (): Cart => cartOf([]);

export const order = (over: Partial<Order> = {}): Order => ({
  id: '11111111-2222-3333-4444-555555555555', userId: 'asha', status: 'Confirmed', createdUtc: '2026-11-04T08:28:00Z',
  lines: [{ productId: 'csk-cap', productName: 'CSK Cap', quantity: 3, unitPrice: 499, lineTotal: 1497 }],
  subtotal: 1497, shipping: 0, total: 1497, ...over,
});
