import { payableTotal, type Cart } from '../api/types';
import { formatRupees } from '../lib/money';

/** Items, shipping and the amount that will be charged. Shipping is only shown when the server reports it. */
export function CartTotals({ cart }: { cart: Cart }) {
  return (
    <dl className="totals">
      <div><dt>Items ({cart.itemCount})</dt><dd>{formatRupees(cart.total)}</dd></div>
      {cart.shipping !== undefined && (
        <div><dt>Shipping</dt><dd>{cart.shipping === 0 ? 'Free' : formatRupees(cart.shipping)}</dd></div>
      )}
      <div className="totals-grand"><dt>Total <span className="muted">(inclusive of tax)</span></dt><dd>{formatRupees(payableTotal(cart))}</dd></div>
    </dl>
  );
}
