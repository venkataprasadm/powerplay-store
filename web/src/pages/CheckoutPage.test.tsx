import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Route, Routes } from 'react-router-dom';
import { CheckoutPage } from './CheckoutPage';
import { apiMock, resetApiMock } from '../test/mockApi';
import { cartOf, emptyCart, line, order } from '../test/fixtures';
import { renderApp } from '../test/render';
import { ApiError } from '../api/errors';

vi.mock('../api/client', async () => (await import('../test/mockApi')).clientMock());

const photoCart = () => cartOf([line({ productId: 'kkr-photo', productName: 'KKR Signed Photo', quantity: 1, currentUnitPrice: 4999, acceptedUnitPrice: 4999, lineTotal: 4999, availableStock: 1 })]);

function renderCheckout() {
  return renderApp(
    <Routes>
      <Route path="/checkout" element={<CheckoutPage />} />
      <Route path="/orders" element={<p>Orders page</p>} />
    </Routes>,
    { route: '/checkout' },
  );
}

beforeEach(() => { resetApiMock(); apiMock.cart.mockResolvedValue(photoCart()); });

describe('CheckoutPage', () => {
  it('lists failing lines on an out-of-stock rejection and keeps the cart', async () => {
    const user = userEvent.setup();
    apiMock.placeOrder.mockRejectedValue(new ApiError({
      status: 409, code: 'out_of_stock',
      failedLines: [{ productId: 'kkr-photo', productName: 'KKR Signed Photo', requested: 1, available: 0 }],
    }));
    renderCheckout();
    await user.click(await screen.findByRole('button', { name: 'Place order' }));

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent(/out of stock/i);
    expect(alert).toHaveTextContent(/cart was kept/i);
    expect(alert).toHaveTextContent('KKR Signed Photo: you asked for 1, none available.');
    expect(screen.queryByText('Orders page')).not.toBeInTheDocument();
    expect(screen.getByRole('table', { name: 'Order summary' })).toBeInTheDocument(); // cart still shown
    expect(screen.queryByRole('button', { name: 'Retry' })).not.toBeInTheDocument(); // not retryable
  });

  it('retries with the same order id after a timeout, and uses it only once on success', async () => {
    const user = userEvent.setup();
    apiMock.placeOrder
      .mockRejectedValueOnce(new ApiError({ status: 0, code: 'timeout' }))
      .mockResolvedValueOnce({ order: order(), replayed: true });
    renderCheckout();
    await user.click(await screen.findByRole('button', { name: 'Place order' }));
    await user.click(await screen.findByRole('button', { name: 'Retry' }));

    await waitFor(() => expect(apiMock.placeOrder).toHaveBeenCalledTimes(2));
    expect(apiMock.placeOrder.mock.calls[0][0]).toBe(apiMock.placeOrder.mock.calls[1][0]);
    expect(apiMock.placeOrder.mock.calls[0][1]).toBe(4999); // expectedTotal travels with the request
    expect(await screen.findByText('Orders page')).toBeInTheDocument();
  });

  it('blocks a double click from sending two requests', async () => {
    const user = userEvent.setup();
    apiMock.placeOrder.mockImplementation(() => new Promise(() => {}));
    renderCheckout();
    await user.dblClick(await screen.findByRole('button', { name: 'Place order' }));
    expect(apiMock.placeOrder).toHaveBeenCalledTimes(1);
  });

  it('shows a message when the cart is empty', async () => {
    apiMock.cart.mockResolvedValue(emptyCart());
    renderCheckout();
    expect(await screen.findByText(/your cart is empty/i)).toBeInTheDocument();
  });

  it('sends the grand total including shipping as the expected total', async () => {
    const user = userEvent.setup();
    apiMock.cart.mockResolvedValue(cartOf([line({ productId: 'csk-cap', productName: 'CSK Cap', quantity: 1, currentUnitPrice: 499, acceptedUnitPrice: 499, lineTotal: 499 })], { shipping: 99, grandTotal: 598 }));
    apiMock.placeOrder.mockImplementation(() => new Promise(() => {}));
    renderCheckout();
    await user.click(await screen.findByRole('button', { name: 'Place order' }));
    expect(apiMock.placeOrder.mock.calls[0][1]).toBe(598);
  });
});
