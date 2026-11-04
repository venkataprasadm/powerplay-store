import { useId, useState, type FormEvent } from 'react';
import { useLocation } from 'react-router-dom';
import { api } from '../api/client';
import { AsyncView } from '../components/AsyncView';
import { EmptyState } from '../components/EmptyState';
import { PageHeading } from '../components/PageHeading';
import { useAsync } from '../hooks/useAsync';
import { useTokenPager } from '../hooks/useTokenPager';
import { formatRupees } from '../lib/money';
import { formatDateTime, localDateRangeToUtc } from '../lib/time';
import { useAuth } from '../state/AuthContext';

export const ORDER_PAGE_SIZE = 5;

function OrderHistory() {
  const location = useLocation();
  const placed = (location.state as { placedOrderId?: string; replayed?: boolean } | null) ?? null;
  const [draft, setDraft] = useState({ from: '', to: '' });
  const [range, setRange] = useState<{ from?: string; to?: string }>({});
  const [rangeError, setRangeError] = useState<string | null>(null);
  const pager = useTokenPager();
  const ids = { from: useId(), to: useId() };

  const state = useAsync(
    () => api.orders({ ...range, pageSize: ORDER_PAGE_SIZE, pageToken: pager.token }),
    [range, pager.token],
  );

  function apply(e: FormEvent) {
    e.preventDefault();
    if (draft.from && draft.to && draft.to < draft.from) {
      setRangeError('The end date must be on or after the start date.');
      return;
    }
    setRangeError(null);
    pager.reset();
    setRange(localDateRangeToUtc(draft.from || undefined, draft.to || undefined));
  }

  return (
    <>
      {placed?.placedOrderId && (
        <p role="status" className="banner banner-ok">
          {placed.replayed ? 'This order was already placed. Showing the original.' : 'Order placed.'} Reference {placed.placedOrderId.slice(0, 8)}.
        </p>
      )}
      <form className="filters" onSubmit={apply} aria-label="Filter orders by date">
        <div className="field">
          <label htmlFor={ids.from}>From date</label>
          <input id={ids.from} type="date" value={draft.from} onChange={(e) => setDraft({ ...draft, from: e.target.value })} />
        </div>
        <div className="field">
          <label htmlFor={ids.to}>To date</label>
          <input id={ids.to} type="date" value={draft.to} aria-invalid={rangeError ? true : undefined}
            aria-describedby={rangeError ? `${ids.to}-err` : undefined}
            onChange={(e) => setDraft({ ...draft, to: e.target.value })} />
          {rangeError && <p id={`${ids.to}-err`} role="alert" className="field-error">{rangeError}</p>}
        </div>
        <button type="submit">Apply</button>
      </form>
      <AsyncView state={state} what="orders">
        {(page) => (
          <>
            {page.items.length === 0 && <div role="status"><EmptyState>No orders found.</EmptyState></div>}
            {page.items.map((o) => (
              <article key={o.id} className="card" aria-labelledby={`o-${o.id}`}>
                <header className="order-head">
                  <div>
                    <h2 id={`o-${o.id}`}>Order {o.id.slice(0, 8)}</h2>
                    <p className="muted">Placed {formatDateTime(o.createdUtc)} (IST)</p>
                  </div>
                  <span className={`status-pill status-${o.status.toLowerCase()}`}>{o.status}</span>
                  <p className="order-total"><span className="muted">Total</span> {formatRupees(o.total)}</p>
                </header>
                <table>
                  <caption className="visually-hidden">Items in order {o.id.slice(0, 8)}</caption>
                  <thead><tr><th scope="col">Product</th><th scope="col">Quantity</th><th scope="col" className="num">Unit price</th><th scope="col" className="num">Line total</th></tr></thead>
                  <tbody>
                    {o.lines.map((l) => (
                      <tr key={l.productId}>
                        <th scope="row">{l.productName}</th><td>{l.quantity}</td>
                        <td className="num">{formatRupees(l.unitPrice)}</td><td className="num">{formatRupees(l.lineTotal)}</td>
                      </tr>
                    ))}
                  </tbody>
                  <tfoot>
                    <tr><th scope="row" colSpan={3}>Shipping</th><td className="num">{formatRupees(o.shipping)}</td></tr>
                    <tr><th scope="row" colSpan={3}>Total</th><td className="num">{formatRupees(o.total)}</td></tr>
                  </tfoot>
                </table>
              </article>
            ))}
            <nav aria-label="Pagination" className="pager">
              <button type="button" onClick={pager.previous} disabled={!pager.hasPrevious}>Newer</button>
              <button type="button" onClick={() => page.nextPageToken && pager.next(page.nextPageToken)} disabled={!page.nextPageToken}>Older</button>
            </nav>
          </>
        )}
      </AsyncView>
    </>
  );
}

export function OrdersPage() {
  const { user } = useAuth();
  return (
    <section>
      <PageHeading title="Order history" />
      {user ? <OrderHistory key={user.userId} /> : <EmptyState>Sign in (top right) to see your orders.</EmptyState>}
    </section>
  );
}
