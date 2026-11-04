import { Link, useParams } from 'react-router-dom';
import { api } from '../api/client';
import { AddToCart } from '../components/AddToCart';
import { AsyncView } from '../components/AsyncView';
import { PageHeading } from '../components/PageHeading';
import { ProductVisual } from '../components/ProductVisual';
import { StockBadge } from '../components/StockBadge';
import { useAsync } from '../hooks/useAsync';
import { formatRupees } from '../lib/money';

export function ProductDetailPage() {
  const { id = '' } = useParams();
  const state = useAsync(() => api.product(id), [id]);
  return (
    <section>
      <p className="back-link"><Link to="/">← Back to all products</Link></p>
      <AsyncView state={state} what="product">
        {(p) => (
          <article className="detail">
            <ProductVisual className="detail-visual" type={p.type} franchiseId={p.franchiseId} />
            <div className="detail-info">
              <p className="label">{p.franchiseName}</p>
              <PageHeading title={p.name} />
              <p className="detail-desc">{p.description}</p>
              <p className="price">{formatRupees(p.price)} <span className="muted">inclusive of tax</span></p>
              <p><StockBadge product={p} /></p>
              <AddToCart product={p} showQuantity />
            </div>
          </article>
        )}
      </AsyncView>
    </section>
  );
}
