import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { formatRupees } from '../lib/money';
import { useCart } from '../state/CartContext';
import { CheckIcon, CloseIcon } from './Icons';
import { ProductVisual } from './ProductVisual';

const VISIBLE_MS = 5000;

/**
 * Confirmation card after an add, like a store's "Added to cart" flyout. It is deliberately not a live region:
 * the Add to cart control already announces the result, so screen readers hear it once. Hovering or focusing it pauses the timer.
 */
export function AddedToCartToast() {
  const { lastAdded, dismissAdded } = useCart();
  const [paused, setPaused] = useState(false);
  const timer = useRef<number>();

  useEffect(() => {
    if (!lastAdded || paused) return;
    timer.current = window.setTimeout(dismissAdded, VISIBLE_MS);
    return () => window.clearTimeout(timer.current);
  }, [lastAdded, paused, dismissAdded]);

  if (!lastAdded) return null;
  const { line, quantity, cart, seq } = lastAdded;
  const count = cart.itemCount;

  return (
    <aside
      key={seq} className="toast" aria-label="Added to cart"
      onMouseEnter={() => setPaused(true)} onMouseLeave={() => setPaused(false)}
      onFocus={() => setPaused(true)} onBlur={() => setPaused(false)}
    >
      {line.productType && line.franchiseId
        ? <ProductVisual className="toast-thumb" type={line.productType} franchiseId={line.franchiseId} />
        : <span className="toast-thumb" aria-hidden="true" />}
      <div className="toast-body">
        <p className="toast-title"><span className="toast-check"><CheckIcon /></span>Added to cart</p>
        <p className="toast-item">{line.productName} <span className="muted">× {quantity}</span></p>
        <p className="toast-sub muted">Cart subtotal ({count} {count === 1 ? 'item' : 'items'}): <strong>{formatRupees(cart.total)}</strong></p>
        <div className="toast-actions">
          <Link className="cta cta-sm" to="/cart" onClick={dismissAdded}>View cart</Link>
          <Link className="cta cta-sm cta-ghost" to="/checkout" onClick={dismissAdded}>Checkout</Link>
        </div>
      </div>
      <button type="button" className="toast-close" onClick={dismissAdded} aria-label="Dismiss"><CloseIcon /></button>
      <span className="toast-timer" style={{ animationPlayState: paused ? 'paused' : 'running', animationDuration: `${VISIBLE_MS}ms` }} aria-hidden="true" />
    </aside>
  );
}
