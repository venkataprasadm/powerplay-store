// Strongly typed models mirroring docs/API.md (v1). Keep in sync with the backend contract.
export type ProductType = 'Jersey' | 'Cap' | 'Flag' | 'SignedPhotograph' | 'Other';
export const PRODUCT_TYPES: ReadonlyArray<{ value: ProductType; label: string }> = [
  { value: 'Jersey', label: 'Jersey' },
  { value: 'Cap', label: 'Cap' },
  { value: 'Flag', label: 'Flag' },
  { value: 'SignedPhotograph', label: 'Signed photograph' },
  { value: 'Other', label: 'Other' },
];

export type StockStatus = 'InStock' | 'LowStock' | 'OutOfStock';
export type Role = 'shopper' | 'admin';

export interface Franchise { id: string; name: string }

export interface Product {
  id: string;
  name: string;
  type: ProductType;
  franchiseId: string;
  franchiseName: string;
  description: string;
  price: number;
  stock: number;
  inStock: boolean;
  /** Added after v1 shipped as an optional additive field; treat as possibly absent. */
  stockStatus?: StockStatus;
  etag: string;
}

export interface Page<T> { items: T[]; nextPageToken: string | null }
export interface ProductPage extends Page<Product> { totalCount: number }

export interface ProductQuery {
  q?: string;
  type?: ProductType | '';
  franchise?: string;
  pageSize?: number;
  pageToken?: string;
}

export interface UpdateProductRequest { price?: number; stock?: number; reason: string }

export type AuditField = 'Price' | 'Stock';
export interface AuditEntry {
  id: string;
  productId: string;
  field: AuditField;
  oldValue: number;
  newValue: number;
  reason: string;
  actor: string;
  atUtc: string;
}

export interface CartLine {
  productId: string;
  productName: string;
  quantity: number;
  acceptedUnitPrice: number;
  currentUnitPrice: number;
  lineTotal: number;
  priceChanged: boolean;
  availableStock: number;
  stockStatus: StockStatus;
  insufficientStock: boolean;
  /** Added after v1 (additive). Used for the line thumbnail; absent from older servers. */
  productType?: ProductType;
  franchiseId?: string;
}

export interface Cart {
  lines: CartLine[];
  itemCount: number;
  /** Item total at current prices (v1 meaning). */
  total: number;
  acceptedTotal: number;
  requiresAcceptance: boolean;
  canCheckout: boolean;
  /** Added after v1 (additive). Absent from older servers, in which case shipping is unknown and `total` is what is charged. */
  shipping?: number;
  /** total + shipping: what checkout charges and what is sent as expectedTotal. */
  grandTotal?: number;
}

/** The amount checkout will charge for this cart. Falls back to `total` for servers that predate shipping. */
export const payableTotal = (cart: Cart): number => cart.grandTotal ?? cart.total;

export interface OrderLine {
  productId: string;
  productName: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
}

export interface Order {
  id: string;
  userId: string;
  status: 'Confirmed';
  createdUtc: string;
  lines: OrderLine[];
  subtotal: number;
  shipping: number;
  total: number;
}

export interface OrderQuery { from?: string; to?: string; pageSize?: number; pageToken?: string }
export interface PlacedOrder { order: Order; replayed: boolean }

export interface DevToken { accessToken: string; userId: string; role: Role; expiresAtUtc: string }
