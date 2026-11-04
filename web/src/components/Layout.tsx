import { useEffect, useId } from 'react';
import { Link, NavLink, Outlet, useLocation } from 'react-router-dom';
import { describeError } from '../api/errors';
import { DEV_USERS, useAuth } from '../state/AuthContext';
import { useCart } from '../state/CartContext';
import { AddedToCartToast } from './AddedToCartToast';
import { CartIcon } from './Icons';
import { Logo } from './Logo';

export function Layout() {
  const { user, signIn, signOut, busy, error } = useAuth();
  const { cart, lastAdded } = useCart();
  const selectId = useId();
  const { pathname } = useLocation();
  // Each route starts at the top. 'instant' overrides the page's smooth scrolling, which is meant for in-page anchors only.
  useEffect(() => { window.scrollTo({ top: 0, behavior: 'instant' as ScrollBehavior }); }, [pathname]);
  const count = cart?.itemCount ?? 0;
  const bump = lastAdded?.seq ?? 0;

  return (
    <>
      <a className="skip-link" href="#main">Skip to main content</a>
      <header className="site-header">
        <Link to="/" className="brand" aria-label="PowerPlay Store, home"><Logo /><span className="brand-text">PowerPlay <span>Store</span></span></Link>
        <nav aria-label="Main">
          <NavLink to="/" end>Products</NavLink>
          <NavLink to="/orders">Orders</NavLink>
          {user?.role === 'admin' && <NavLink to="/admin">Admin</NavLink>}
        </nav>
        <div className="header-end">
          <div className="user-switch">
            <label htmlFor={selectId}>Sign in as</label>
            <select
              id={selectId} value={user?.userId ?? ''} disabled={busy}
              onChange={(e) => (e.target.value ? void signIn(e.target.value) : signOut())}
            >
              <option value="">Not signed in</option>
              {DEV_USERS.map((u) => <option key={u.userId} value={u.userId}>{u.label}</option>)}
            </select>
          </div>
          <NavLink to="/cart" className="cart-link">
            <span className="cart-icon-wrap">
              <CartIcon />
              {user && count > 0 && <span key={`c${bump}`} className={`cart-count${bump ? ' bump' : ''}`} aria-hidden="true">{count > 99 ? '99+' : count}</span>}
              {lastAdded && <span key={`p${bump}`} className="cart-plus" aria-hidden="true">+{lastAdded.quantity}</span>}
            </span>
            <span className="cart-label">Cart</span>
            {user && <span className="visually-hidden"> ({count} {count === 1 ? 'item' : 'items'})</span>}
          </NavLink>
        </div>
      </header>
      {Boolean(error) && <p role="alert" className="banner banner-error">{describeError(error)}</p>}
      <main id="main" tabIndex={-1}><div key={pathname} className="page-enter"><Outlet /></div></main>
      <AddedToCartToast />
      <footer className="site-footer">
        <span>&copy; 2026 PowerPlay Store</span>
        <span>All prices in Indian rupees and inclusive of tax.</span>
        <p className="disclaimer">PowerPlay Store is an independent store. It is not affiliated with, endorsed by or sponsored by the IPL, the BCCI or any franchise. Team names are used only to describe products.</p>
      </footer>
    </>
  );
}
