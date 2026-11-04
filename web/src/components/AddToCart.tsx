import { useEffect, useId, useState } from 'react';
import type { Product } from '../api/types';
import { useAuth } from '../state/AuthContext';
import { useCart } from '../state/CartContext';
import { useIdempotentAction } from '../hooks/useIdempotentAction';
import { describeError } from '../api/errors';
import { CheckIcon } from './Icons';

const ADDED_MS = 1800;

export function AddToCart({ product, showQuantity = false }: { product: Product; showQuantity?: boolean }) {
  const { user } = useAuth();
  const { add, cart } = useCart();
  const [quantity, setQuantity] = useState(1);
  const [added, setAdded] = useState(false);
  const [flash, setFlash] = useState(0);
  const [localError, setLocalError] = useState<string | null>(null);
  const action = useIdempotentAction(add);
  const qtyId = useId();
  const hintId = useId();

  // The "Added" state on the button is a short visual confirmation; the status line below stays for screen readers.
  useEffect(() => {
    if (!flash) return;
    const t = window.setTimeout(() => setFlash(0), ADDED_MS);
    return () => window.clearTimeout(t);
  }, [flash]);

  const outOfStock = (product.stockStatus ?? (product.inStock ? 'InStock' : 'OutOfStock')) === 'OutOfStock';
  const disabled = !user || outOfStock || action.pending;
  const inCart = cart?.lines.find((l) => l.productId === product.id)?.quantity ?? 0;

  async function onAdd() {
    setAdded(false);
    if (!Number.isInteger(quantity) || quantity < 1 || quantity > 10) {
      setLocalError('Quantity must be a whole number from 1 to 10.');
      return;
    }
    setLocalError(null);
    const result = await action.run(product.id, quantity);
    if (result) { setAdded(true); setFlash((n) => n + 1); }
  }

  return (
    <div className="add-to-cart">
      {showQuantity && (
        <div className="field">
          <label htmlFor={qtyId}>Quantity</label>
          <input
            id={qtyId} type="number" min={1} max={10} value={Number.isNaN(quantity) ? '' : quantity}
            aria-invalid={localError ? true : undefined}
            onChange={(e) => setQuantity(e.target.valueAsNumber)}
          />
        </div>
      )}
      <button
        type="button" onClick={onAdd} disabled={disabled} aria-describedby={!user || outOfStock ? hintId : undefined}
        className={`add-button${flash ? ' is-added' : ''}${action.pending ? ' is-pending' : ''}`}
      >
        {action.pending ? 'Adding…' : flash ? (
          <>
            <span key={flash} className="added-label" aria-hidden="true"><CheckIcon /> Added</span>
            <span className="visually-hidden">Add to cart</span>
          </>
        ) : 'Add to cart'}
        <span className="visually-hidden"> {product.name}</span>
      </button>
      {user && inCart > 0 && <span className="in-cart"><CheckIcon /> {inCart} in cart</span>}
      {!user && <span id={hintId} className="muted"> Sign in to add items.</span>}
      {user && outOfStock && <span id={hintId} className="muted"> Out of stock.</span>}
      {localError && <p role="alert" className="field-error">{localError}</p>}
      {Boolean(action.error) && <p role="alert" className="field-error">{describeError(action.error)}</p>}
      <p role="status" className="visually-hidden">{added ? `Added ${product.name} to your cart.` : ''}</p>
    </div>
  );
}
