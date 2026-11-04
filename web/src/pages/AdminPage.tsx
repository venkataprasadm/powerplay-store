import { useId, useState, type FormEvent } from 'react';
import { api } from '../api/client';
import { ApiError, describeError, fieldErrors } from '../api/errors';
import type { Product } from '../api/types';
import { AsyncView } from '../components/AsyncView';
import { PageHeading } from '../components/PageHeading';
import { useAsync } from '../hooks/useAsync';
import { useTokenPager } from '../hooks/useTokenPager';
import { formatRupees, hasAtMostTwoDecimals } from '../lib/money';
import { formatDateTime } from '../lib/time';
import { useAuth } from '../state/AuthContext';

function AuditTrail({ productId, refreshKey }: { productId: string; refreshKey: string }) {
  const pager = useTokenPager();
  const state = useAsync(() => api.audit(productId, 10, pager.token), [productId, pager.token, refreshKey]);
  return (
    <AsyncView state={state} what="audit trail">
      {(page) => (
        <>
          <table>
            <caption>Audit trail (newest first)</caption>
            <thead><tr><th scope="col">When (IST)</th><th scope="col">Field</th><th scope="col">Old</th><th scope="col">New</th><th scope="col">Reason</th><th scope="col">By</th></tr></thead>
            <tbody>
              {page.items.length === 0 && <tr><td colSpan={6}>No changes recorded.</td></tr>}
              {page.items.map((a) => (
                <tr key={a.id}>
                  <td>{formatDateTime(a.atUtc)}</td><td>{a.field}</td>
                  <td>{a.field === 'Price' ? formatRupees(a.oldValue) : a.oldValue}</td>
                  <td>{a.field === 'Price' ? formatRupees(a.newValue) : a.newValue}</td>
                  <td>{a.reason}</td><td>{a.actor}</td>
                </tr>
              ))}
            </tbody>
          </table>
          <nav aria-label="Audit pagination" className="pager">
            <button type="button" onClick={pager.previous} disabled={!pager.hasPrevious}>Newer</button>
            <button type="button" onClick={() => page.nextPageToken && pager.next(page.nextPageToken)} disabled={!page.nextPageToken}>Older</button>
          </nav>
        </>
      )}
    </AsyncView>
  );
}

export function AdminProductEditor({ initial }: { initial: Product }) {
  const [product, setProduct] = useState(initial);
  const [price, setPrice] = useState(String(initial.price));
  const [stock, setStock] = useState(String(initial.stock));
  const [reason, setReason] = useState('');
  const [clientErrors, setClientErrors] = useState<Record<string, string>>({});
  const [error, setError] = useState<unknown>(null);
  const [saved, setSaved] = useState(false);
  const [pending, setPending] = useState(false);
  const [showAudit, setShowAudit] = useState(false);
  const id = useId();

  const conflict = error instanceof ApiError && (error.status === 412 || error.status === 428);

  function load(p: Product) {
    setProduct(p);
    setPrice(String(p.price));
    setStock(String(p.stock));
    setReason('');
  }

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setSaved(false);
    setError(null);
    const errs: Record<string, string> = {};
    const priceNum = Number(price);
    const stockNum = Number(stock);
    if (price.trim() === '' || !Number.isFinite(priceNum) || priceNum <= 0) errs.price = 'Enter a price greater than zero.';
    else if (!hasAtMostTwoDecimals(priceNum)) errs.price = 'Use at most two decimal places.';
    if (stock.trim() === '' || !Number.isInteger(stockNum) || stockNum < 0) errs.stock = 'Enter a whole number of zero or more.';
    if (!reason.trim()) errs.reason = 'A reason is required for the audit trail.';
    const changedPrice = priceNum !== product.price;
    const changedStock = stockNum !== product.stock;
    if (!errs.price && !errs.stock && !changedPrice && !changedStock) errs.price = 'Change the price or the stock first.';
    setClientErrors(errs);
    if (Object.keys(errs).length > 0) return;

    setPending(true);
    try {
      const updated = await api.updateProduct(product.id, product.etag, {
        ...(changedPrice ? { price: priceNum } : {}),
        ...(changedStock ? { stock: stockNum } : {}),
        reason: reason.trim(),
      });
      load(updated);
      setSaved(true);
    } catch (err) {
      setError(err);
    } finally {
      setPending(false);
    }
  }

  async function reloadLatest() {
    setError(null);
    try { load(await api.product(product.id)); } catch (err) { setError(err); }
  }

  const msg = (field: string) => clientErrors[field] ?? fieldErrors(error, field)[0];
  const field = (name: 'price' | 'stock' | 'reason') => ({
    'aria-invalid': msg(name) ? true : undefined,
    'aria-describedby': msg(name) ? `${id}-${name}-err` : undefined,
  });

  return (
    <article className="card" aria-labelledby={`${id}-h`}>
      <h2 id={`${id}-h`}>{product.name}</h2>
      <p className="muted">{product.franchiseName} - currently {formatRupees(product.price)}, {product.stock} in stock</p>
      <form onSubmit={onSubmit} noValidate aria-label={`Edit ${product.name}`}>
        <div className="field">
          <label htmlFor={`${id}-price`}>Price (₹)</label>
          <input id={`${id}-price`} inputMode="decimal" value={price} onChange={(e) => setPrice(e.target.value)} {...field('price')} />
          {msg('price') && <p id={`${id}-price-err`} className="field-error">{msg('price')}</p>}
        </div>
        <div className="field">
          <label htmlFor={`${id}-stock`}>Stock quantity</label>
          <input id={`${id}-stock`} inputMode="numeric" value={stock} onChange={(e) => setStock(e.target.value)} {...field('stock')} />
          {msg('stock') && <p id={`${id}-stock-err`} className="field-error">{msg('stock')}</p>}
        </div>
        <div className="field">
          <label htmlFor={`${id}-reason`}>Reason</label>
          <input id={`${id}-reason`} value={reason} onChange={(e) => setReason(e.target.value)} {...field('reason')} />
          {msg('reason') && <p id={`${id}-reason-err`} className="field-error">{msg('reason')}</p>}
        </div>
        <button type="submit" disabled={pending}>{pending ? 'Saving…' : 'Save changes'}</button>
      </form>
      {Boolean(error) && (
        <div role="alert" className="banner banner-error">
          <p>{describeError(error)}</p>
          {conflict && <button type="button" onClick={() => void reloadLatest()}>Reload latest</button>}
        </div>
      )}
      <p role="status">{saved ? 'Saved. The change is in the audit trail.' : ''}</p>
      <button type="button" aria-expanded={showAudit} onClick={() => setShowAudit((s) => !s)}>
        {showAudit ? 'Hide audit trail' : 'Show audit trail'}
      </button>
      {showAudit && <AuditTrail productId={product.id} refreshKey={product.etag} />}
    </article>
  );
}

function AdminProducts() {
  const pager = useTokenPager();
  const state = useAsync(() => api.products({ pageSize: 20, pageToken: pager.token }), [pager.token]);
  return (
    <AsyncView state={state} what="products">
      {(page) => (
        <>
          {page.items.map((p) => <AdminProductEditor key={`${p.id}-${pager.pageNumber}`} initial={p} />)}
          <nav aria-label="Pagination" className="pager">
            <button type="button" onClick={pager.previous} disabled={!pager.hasPrevious}>Previous</button>
            <button type="button" onClick={() => page.nextPageToken && pager.next(page.nextPageToken)} disabled={!page.nextPageToken}>Next</button>
          </nav>
        </>
      )}
    </AsyncView>
  );
}

export function AdminPage() {
  const { user } = useAuth();
  return (
    <section>
      <PageHeading title="Admin: prices and stock" />
      {user?.role === 'admin' ? <AdminProducts /> : <p>Admin access required. Sign in as Admin (top right).</p>}
    </section>
  );
}
