import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Route, Routes } from 'react-router-dom';
import { AddToCart } from './AddToCart';
import { Layout } from './Layout';
import { apiMock, resetApiMock } from '../test/mockApi';
import { cartOf, emptyCart, line, product } from '../test/fixtures';
import { renderApp } from '../test/render';

vi.mock('../api/client', async () => (await import('../test/mockApi')).clientMock());

const jersey = line({ productType: 'Jersey', franchiseId: 'mi', quantity: 1, lineTotal: 1999 });

function renderShop() {
  return renderApp(
    <Routes>
      <Route element={<Layout />}>
        <Route index element={<AddToCart product={product()} />} />
        <Route path="cart" element={<p>Cart page</p>} />
      </Route>
    </Routes>,
  );
}

beforeEach(() => {
  resetApiMock();
  apiMock.cart.mockResolvedValue(emptyCart());
});

describe('add to cart feedback', () => {
  it('bumps the header count with +1, confirms in a card, and marks the product as in the cart', async () => {
    const user = userEvent.setup();
    apiMock.addToCart.mockResolvedValue(cartOf([jersey]));
    renderShop();
    expect(await screen.findByRole('link', { name: /cart \(0 items\)/i })).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /add to cart/i }));

    const cartLink = await screen.findByRole('link', { name: /cart \(1 item\)/i });
    expect(within(cartLink).getByText('+1')).toBeInTheDocument();
    const toast = screen.getByRole('complementary', { name: 'Added to cart' });
    expect(toast).toHaveTextContent('MI Jersey');
    expect(toast).toHaveTextContent('Cart subtotal (1 item): ₹1,999.00');
    expect(screen.getByText(/1 in cart/)).toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveTextContent('Added MI Jersey to your cart.');
  });

  it('lets the shopper go straight to the cart, or dismiss the card', async () => {
    const user = userEvent.setup();
    apiMock.addToCart.mockResolvedValue(cartOf([jersey]));
    renderShop();
    await user.click(await screen.findByRole('button', { name: /add to cart/i }));
    await user.click(await screen.findByRole('button', { name: 'Dismiss' }));
    expect(screen.queryByRole('complementary', { name: 'Added to cart' })).not.toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /add to cart/i }));
    await user.click(await screen.findByRole('link', { name: 'View cart' }));
    expect(await screen.findByText('Cart page')).toBeInTheDocument();
    expect(screen.queryByRole('complementary', { name: 'Added to cart' })).not.toBeInTheDocument();
  });

  it('shows nothing when the add fails', async () => {
    const user = userEvent.setup();
    apiMock.addToCart.mockRejectedValue(new Error('boom'));
    renderShop();
    await user.click(await screen.findByRole('button', { name: /add to cart/i }));
    expect(await screen.findByRole('alert')).toBeInTheDocument();
    expect(screen.queryByRole('complementary', { name: 'Added to cart' })).not.toBeInTheDocument();
    expect(screen.queryByText('+1')).not.toBeInTheDocument();
  });
});
