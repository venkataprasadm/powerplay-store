import type { Product } from '../api/types';

/** Stock state is conveyed with text, never colour alone. Falls back to `inStock` if the additive field is absent. */
export function StockBadge({ product }: { product: Pick<Product, 'stock' | 'inStock' | 'stockStatus'> }) {
  const status = product.stockStatus ?? (product.inStock ? 'InStock' : 'OutOfStock');
  if (status === 'OutOfStock') return <span className="badge badge-out">Out of stock</span>;
  if (status === 'LowStock') return <span className="badge badge-low">Low stock: only {product.stock} left</span>;
  return <span className="badge badge-ok">In stock</span>;
}
