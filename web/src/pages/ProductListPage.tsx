import { useId, useState, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../api/client';
import { PRODUCT_TYPES, type ProductQuery, type ProductType } from '../api/types';
import { AddToCart } from '../components/AddToCart';
import { AsyncView } from '../components/AsyncView';
import { Hero } from '../components/Hero';
import { ProductVisual } from '../components/ProductVisual';
import { StockBadge } from '../components/StockBadge';
import { useAsync } from '../hooks/useAsync';
import { useTokenPager } from '../hooks/useTokenPager';
import { formatRupees } from '../lib/money';

export const PRODUCT_PAGE_SIZE = 6;
const PAGE_SIZES = [2, 6, 12, 24];

interface Filters { q: string; type: ProductType | ''; franchise: string }
const EMPTY: Filters = { q: '', type: '', franchise: '' };

export function ProductListPage() {
  const [draft, setDraft] = useState<Filters>(EMPTY);
  const [applied, setApplied] = useState<Filters>(EMPTY);
  const [pageSize, setPageSize] = useState(PRODUCT_PAGE_SIZE);
  const pager = useTokenPager();
  const ids = { q: useId(), type: useId(), franchise: useId(), size: useId() };

  const franchises = useAsync(() => api.franchises(), []);
  const products = useAsync(() => {
    const query: ProductQuery = {
      q: applied.q.trim() || undefined,
      type: applied.type || undefined,
      franchise: applied.franchise || undefined,
      pageSize,
      pageToken: pager.token,
    };
    return api.products(query);
  }, [applied, pageSize, pager.token]);

  function pickTeam(franchise: string) {
    const next = { ...EMPTY, franchise };
    pager.reset();
    setDraft(next);
    setApplied(next);
    document.getElementById('collection')?.scrollIntoView?.({ behavior: 'smooth' });
  }

  function onSubmit(e: FormEvent) {
    e.preventDefault();
    pager.reset();
    setApplied(draft);
  }

  return (
    <section>
      <Hero onPickTeam={pickTeam} />
      <div id="collection" className="collection">
      <div className="collection-head">
        <h2>The collection</h2>
        <p className="muted">Jerseys, caps, flags and collectibles. Prices in rupees, inclusive of tax.</p>
      </div>
      <form role="search" aria-label="Search products" className="filters" onSubmit={onSubmit}>
        <div className="field">
          <label htmlFor={ids.q}>Search by name</label>
          <input id={ids.q} type="search" value={draft.q} onChange={(e) => setDraft({ ...draft, q: e.target.value })} />
        </div>
        <div className="field">
          <label htmlFor={ids.type}>Type</label>
          <select id={ids.type} value={draft.type} onChange={(e) => setDraft({ ...draft, type: e.target.value as ProductType | '' })}>
            <option value="">All types</option>
            {PRODUCT_TYPES.map((t) => <option key={t.value} value={t.value}>{t.label}</option>)}
          </select>
        </div>
        <div className="field">
          <label htmlFor={ids.franchise}>Franchise</label>
          <select id={ids.franchise} value={draft.franchise} onChange={(e) => setDraft({ ...draft, franchise: e.target.value })}>
            <option value="">All franchises</option>
            {(franchises.data ?? []).map((f) => <option key={f.id} value={f.id}>{f.name}</option>)}
          </select>
        </div>
        <button type="submit">Search</button>
      </form>

      <div className="results-bar">
        {products.data && !products.error && (
          <p role="status" className="result-count">
            {products.data.items.length === 0
              ? 'No products match your search.'
              : `Showing ${products.data.items.length} of ${products.data.totalCount} products (page ${pager.pageNumber}).`}
          </p>
        )}
        <div className="field field-inline">
          <label htmlFor={ids.size}>Per page</label>
          <select id={ids.size} value={pageSize} onChange={(e) => { pager.reset(); setPageSize(Number(e.target.value)); }}>
            {PAGE_SIZES.map((n) => <option key={n} value={n}>{n}</option>)}
          </select>
        </div>
      </div>
      <AsyncView state={products} what="products" placeholder={<SkeletonGrid count={Math.min(pageSize, 6)} />}>
        {(page) => (
          <>
            {page.items.length === 0 && (
              <div className="empty-state">
                <p>Try a different name, or clear the filters.</p>
                <button type="button" className="cta-ghost" onClick={() => { pager.reset(); setDraft(EMPTY); setApplied(EMPTY); }}>Clear filters</button>
              </div>
            )}
            <ul className="product-grid">
              {page.items.map((p, i) => (
                <li key={p.id} style={{ ['--i' as string]: i }}>
                  <article aria-labelledby={`p-${p.id}`} className="product-card">
                    <Link to={`/products/${p.id}`} className="product-media" tabIndex={-1} aria-hidden="true">
                      <ProductVisual type={p.type} franchiseId={p.franchiseId} />
                    </Link>
                    <div className="product-body">
                      <p className="label">{p.franchiseName}</p>
                      <h2 id={`p-${p.id}`}><Link to={`/products/${p.id}`}>{p.name}</Link></h2>
                      <div className="product-meta">
                        <p className="price">{formatRupees(p.price)}</p>
                        <StockBadge product={p} />
                      </div>
                      <AddToCart product={p} />
                    </div>
                  </article>
                </li>
              ))}
            </ul>
            {(pager.hasPrevious || page.nextPageToken) && (
              <nav aria-label="Pagination" className="pager">
                <button type="button" onClick={pager.previous} disabled={!pager.hasPrevious}>Previous</button>
                <span className="pager-page" aria-hidden="true">Page {pager.pageNumber}</span>
                <button type="button" onClick={() => page.nextPageToken && pager.next(page.nextPageToken)} disabled={!page.nextPageToken}>Next</button>
              </nav>
            )}
          </>
        )}
      </AsyncView>
      </div>
    </section>
  );
}

function SkeletonGrid({ count }: { count: number }) {
  return (
    <ul className="product-grid">
      {Array.from({ length: count }, (_, i) => (
        <li key={i}><div className="product-card skeleton-card"><div className="skeleton skeleton-media" /><div className="skeleton skeleton-line" /><div className="skeleton skeleton-line short" /><div className="skeleton skeleton-button" /></div></li>
      ))}
    </ul>
  );
}
