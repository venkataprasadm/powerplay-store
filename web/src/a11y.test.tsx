import { screen } from '@testing-library/react';
import { axe } from 'vitest-axe';
import { MemoryRouter } from 'react-router-dom';
import { render } from '@testing-library/react';
import { AdminProductEditor } from './pages/AdminPage';
import { CartPage } from './pages/CartPage';
import { CheckoutPage } from './pages/CheckoutPage';
import { ProductListPage } from './pages/ProductListPage';
import { Layout } from './components/Layout';
import { AuthProvider } from './state/AuthContext';
import { CartProvider } from './state/CartContext';
import { apiMock, resetApiMock } from './test/mockApi';
import { cartOf, line, product } from './test/fixtures';
import { renderApp } from './test/render';

vi.mock('./api/client', async () => (await import('./test/mockApi')).clientMock());

// region: pages are rendered without the page shell here. color-contrast: jsdom has no layout/canvas, so it cannot be evaluated.
const options = { rules: { region: { enabled: false }, 'color-contrast': { enabled: false } } };

beforeEach(() => {
  resetApiMock();
  apiMock.franchises.mockResolvedValue([{ id: 'csk', name: 'Chennai Super Kings' }]);
  apiMock.products.mockResolvedValue({ items: [product()], nextPageToken: 'n', totalCount: 3 });
  apiMock.cart.mockResolvedValue(cartOf([line({ priceChanged: true, currentUnitPrice: 2099, lineTotal: 4198 })]));
});

describe('accessibility (axe)', () => {
  it('product list has no violations', async () => {
    const { container } = renderApp(<ProductListPage />);
    await screen.findByText('₹1,999.00');
    expect(await axe(container, options)).toHaveNoViolations();
  });

  it('cart with a price warning has no violations', async () => {
    const { container } = renderApp(<CartPage />);
    await screen.findByRole('button', { name: 'Accept new total' });
    expect(await axe(container, options)).toHaveNoViolations();
  });

  it('checkout has no violations', async () => {
    apiMock.cart.mockResolvedValue(cartOf([line()]));
    const { container } = renderApp(<CheckoutPage />);
    await screen.findByRole('button', { name: 'Place order' });
    expect(await axe(container, options)).toHaveNoViolations();
  });

  it('admin editor has no violations', async () => {
    const { container } = renderApp(<AdminProductEditor initial={product()} />, { user: 'admin' });
    await screen.findByLabelText('Price (₹)');
    expect(await axe(container, options)).toHaveNoViolations();
  });

  it('the app shell exposes a skip link, labelled navigation and a labelled user switcher', async () => {
    sessionStorage.clear();
    const { container } = render(
      <AuthProvider><CartProvider><MemoryRouter><Layout /></MemoryRouter></CartProvider></AuthProvider>,
    );
    expect(screen.getByRole('link', { name: 'Skip to main content' })).toHaveAttribute('href', '#main');
    expect(screen.getByRole('navigation', { name: 'Main' })).toBeInTheDocument();
    expect(screen.getByLabelText('Sign in as')).toBeInTheDocument();
    expect(await axe(container, options)).toHaveNoViolations();
  });
});
