import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { api } from '../api/client';
import { ApiError } from '../api/errors';
import { ErrorBanner } from '../components/ErrorBanner';
import { CartTotals } from '../components/CartTotals';
import { EmptyState } from '../components/EmptyState';
import { PageHeading } from '../components/PageHeading';
import { usePersistentOrderId } from '../hooks/usePersistentOrderId';
import { payableTotal } from '../api/types';
import { formatRupees } from '../lib/money';
import { useAuth } from '../state/AuthContext';
import { useCart } from '../state/CartContext';

function CheckoutForm({ userId }: { userId: string }) {
  const { cart, reload } = useCart();
  const navigate = useNavigate();
  const { orderId, renew } = usePersistentOrderId(userId);
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<unknown>(null);

  if (!cart) return <p role="status">Loading your cart…</p>;
  if (cart.lines.length === 0) return <EmptyState>Your cart is empty. <Link to="/">Browse merchandise</Link>.</EmptyState>;

  async function place() {
    if (pending || !cart) return; // double-click guard; the stable orderId is the server-side guard
    setPending(true);
    setError(null);
    try {
      const { order, replayed } = await api.placeOrder(orderId, payableTotal(cart));
      renew();
      await reload();
      navigate('/orders', { state: { placedOrderId: order.id, replayed } });
    } catch (e) {
      setError(e);
      // Server state may have moved (stock, prices): refresh so the cart page shows the truth.
      if (e instanceof ApiError && e.status === 409) await reload();
    } finally {
      setPending(false);
    }
  }

  const retryable = error instanceof ApiError && error.retryable;
  const cartChanged = error instanceof ApiError && error.code === 'cart_changed';

  return (
    <div className="cart-layout">
      <div className="cart-main">
      <table>
        <caption>Order summary</caption>
        <thead><tr><th scope="col">Product</th><th scope="col">Quantity</th><th scope="col" className="num">Unit price</th><th scope="col" className="num">Line total</th></tr></thead>
        <tbody>
          {cart.lines.map((l) => (
            <tr key={l.productId}>
              <th scope="row">{l.productName}</th><td>{l.quantity}</td>
              <td className="num">{formatRupees(l.currentUnitPrice)}</td><td className="num">{formatRupees(l.lineTotal)}</td>
            </tr>
          ))}
        </tbody>
        <tfoot><tr><th scope="row" colSpan={3}>Items total</th><td className="num">{formatRupees(cart.total)}</td></tr></tfoot>
      </table>
      </div>
      <aside className="summary-card" aria-labelledby="checkout-summary-h">
      <h2 id="checkout-summary-h">Payment summary</h2>
      <CartTotals cart={cart} />
      {Boolean(error) && (
        <ErrorBanner error={error} onRetry={retryable ? () => void place() : undefined}>
          <p><Link to="/cart">Review your cart</Link></p>
          {cartChanged && <p>Your cart has been refreshed with the latest prices.</p>}
        </ErrorBanner>
      )}
      <button type="button" className="summary-cta" onClick={() => void place()} disabled={pending || !cart.canCheckout}>
        {pending ? 'Placing order…' : 'Place order'}
      </button>
      {!cart.canCheckout && <p className="muted">Your cart needs attention before you can check out. <Link to="/cart">Go to cart</Link>.</p>}
      <p className="fine-print muted">You are charged the total shown. If a price changes before you place the order, nothing is charged and you are asked to review it.</p>
      </aside>
    </div>
  );
}

export function CheckoutPage() {
  const { user } = useAuth();
  return (
    <section>
      <PageHeading title="Checkout" />
      {user ? <CheckoutForm userId={user.userId} /> : <EmptyState>Sign in (top right) to check out.</EmptyState>}
    </section>
  );
}
