import { render } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import type { ReactElement } from 'react';
import { AuthProvider } from '../state/AuthContext';
import { CartProvider } from '../state/CartContext';

/** Renders under the real providers. Pre-seeds sessionStorage so the user is signed in without a network call. */
export function renderApp(ui: ReactElement, opts: { route?: string; user?: 'asha' | 'admin' | null } = {}) {
  const { route = '/', user = 'asha' } = opts;
  if (user) {
    sessionStorage.setItem('ipl.auth', JSON.stringify({
      userId: user, role: user === 'admin' ? 'admin' : 'shopper', accessToken: 'test-token',
      expiresAtUtc: new Date(Date.now() + 3_600_000).toISOString(),
    }));
  }
  return render(
    <AuthProvider>
      <CartProvider>
        <MemoryRouter initialEntries={[route]}>{ui}</MemoryRouter>
      </CartProvider>
    </AuthProvider>,
  );
}

/** A promise you resolve or reject by hand, to hold a request "in flight". */
export function deferred<T>() {
  let resolve!: (v: T) => void;
  let reject!: (e: unknown) => void;
  const promise = new Promise<T>((res, rej) => { resolve = res; reject = rej; });
  return { promise, resolve, reject };
}
