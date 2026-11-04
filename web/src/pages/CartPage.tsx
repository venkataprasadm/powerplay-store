import { useNavigate, Link } from 'react-router-dom';
import type { CartLine } from '../api/types';
import { describeError } from '../api/errors';
import { ErrorBanner } from '../components/ErrorBanner';
import { CartTotals } from '../components/CartTotals';
import { EmptyState } from '../components/EmptyState';
import { PageHeading } from '../components/PageHeading';
import { ProductVisual } from '../components/ProductVisual';
import { StockBadge } from '../components/StockBadge';
import { useIdempotentAction } from '../hooks/useIdempotentAction';
import { formatRupees } from '../lib/money';
import { useAuth } from '../state/AuthContext';
import { useCart } from '../state/CartContext';

function CartLineRow({ line }: { line: CartLine }) {
  const { setQuantity, remove } = useCart();
  const change = useIdempotentAction(setQuantity);
  const drop = useIdempotentAction(remove);
  const busy = change.pending || drop.pending;
  const error = change.error ?? drop.error;
  return (
    <tr>
      <th scope="row">
        <div className="line-product">
          {line.productType && line.franchiseId
            ? <ProductVisual className="line-thumb" type={line.productType} franchiseId={line.franchiseId} />
            : <span className="line-thumb" aria-hidden="true" />}
          <div>
            <Link to={`/products/${line.productId}`}>{line.productName}</Link>
            <div><StockBadge product={{ stock: line.availableStock, inStock: line.availableStock > 0, stockStatus: line.stockStatus }} /></div>
        {line.priceChanged && (
          <p className="warn">Price changed from {formatRupees(line.acceptedUnitPrice)} to {formatRupees(line.currentUnitPrice)}.</p>
        )}
        {line.insufficientStock && (
          <p className="field-error">Only {line.availableStock} available. Lower the quantity to continue.</p>
        )}
            {Boolean(error) && <p role="alert" className="field-error">{describeError(error)}</p>}
          </div>
        </div>
      </th>
      <td>
        <label>
          <span className="visually-hidden">Quantity for {line.productName}</span>
          <select value={line.quantity} disabled={busy} onChange={(e) => void change.run(line.productId, Number(e.target.value))}>
            {Array.from({ length: 10 }, (_, i) => i + 1).map((n) => <option key={n} value={n}>{n}</option>)}
          </select>
        </label>
      </td>
      <td className="num">{formatRupees(line.currentUnitPrice)}</td>
      <td className="num">{formatRupees(line.lineTotal)}</td>
      <td className="actions">
        <button type="button" disabled={busy} onClick={() => void drop.run(line.productId)}>
          Remove<span className="visually-hidden"> {line.productName}</span>
        </button>
      </td>
    </tr>
  );
}

export function CartPage() {
  const { user } = useAuth();
  const { cart, loading, error, reload, accept } = useCart();
  const navigate = useNavigate();
  const acceptAction = useIdempotentAction(accept);

  return (
    <section>
      <PageHeading title="Your cart" />
      {!user && <EmptyState>Sign in (top right) to see your cart.</EmptyState>}
      {user && error != null && <ErrorBanner error={error} onRetry={() => void reload()} />}
      {user && !cart && !error && <p role="status">Loading your cart…</p>}
      {user && cart && (
        cart.lines.length === 0 ? (
          <EmptyState>Your cart is empty. <Link to="/">Browse merchandise</Link>.</EmptyState>
        ) : (
          <div aria-busy={loading} className="cart-layout">
            <div className="cart-main">
            {cart.requiresAcceptance && (
              <div role="alert" className="banner banner-warn">
                <p>
                  Some prices changed. Your new total is {formatRupees(cart.total)} (it was {formatRupees(cart.acceptedTotal)}).
                  Accept the new total to continue to checkout.
                </p>
                <button type="button" disabled={acceptAction.pending} onClick={() => void acceptAction.run()}>Accept new total</button>
                {Boolean(acceptAction.error) && <p className="field-error">{describeError(acceptAction.error)}</p>}
              </div>
            )}
            <table>
              <caption>Cart items</caption>
              <thead>
                <tr><th scope="col">Product</th><th scope="col">Quantity</th><th scope="col" className="num">Unit price</th><th scope="col" className="num">Line total</th><th scope="col">Actions</th></tr>
              </thead>
              <tbody>{cart.lines.map((l) => <CartLineRow key={l.productId} line={l} />)}</tbody>
              <tfoot>
                <tr><th scope="row" colSpan={3}>Cart total</th><td className="num">{formatRupees(cart.total)}</td><td /></tr>
              </tfoot>
            </table>
            </div>
            <aside className="summary-card" aria-labelledby="cart-summary-h">
              <h2 id="cart-summary-h">Summary</h2>
              <CartTotals cart={cart} />
              <button type="button" className="summary-cta" disabled={!cart.canCheckout} aria-describedby="checkout-hint" onClick={() => navigate('/checkout')}>
                Proceed to checkout
              </button>
              {!cart.canCheckout && (
                <p id="checkout-hint" className="muted">
                  {cart.requiresAcceptance ? 'Accept the new total first.' : 'Fix the highlighted items to continue.'}
                </p>
              )}
            </aside>
          </div>
        )
      )}
    </section>
  );
}

