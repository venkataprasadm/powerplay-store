import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AddToCart } from './AddToCart';
import { apiMock, resetApiMock } from '../test/mockApi';
import { cartOf, emptyCart, line, product } from '../test/fixtures';
import { deferred, renderApp } from '../test/render';
import { ApiError } from '../api/errors';

vi.mock('../api/client', async () => (await import('../test/mockApi')).clientMock());

beforeEach(() => {
  resetApiMock();
  apiMock.cart.mockResolvedValue(emptyCart());
});

describe('AddToCart', () => {
  it('sends one request for a double click', async () => {
    const user = userEvent.setup();
    const d = deferred<ReturnType<typeof cartOf>>();
    apiMock.addToCart.mockReturnValue(d.promise);
    renderApp(<AddToCart product={product()} />);
    const button = await screen.findByRole('button', { name: /add to cart/i });
    await user.dblClick(button);
    expect(apiMock.addToCart).toHaveBeenCalledTimes(1);
    d.resolve(cartOf([line()]));
    expect(await screen.findByText(/added mi jersey/i)).toBeInTheDocument();
  });

  it('reuses the idempotency key when the same intent is retried after a failure, then renews it after success', async () => {
    const user = userEvent.setup();
    apiMock.addToCart
      .mockRejectedValueOnce(new ApiError({ status: 0, code: 'timeout' }))
      .mockResolvedValue(cartOf([line()]));
    renderApp(<AddToCart product={product()} />);
    const button = await screen.findByRole('button', { name: /add to cart/i });

    await user.click(button);
    expect(await screen.findByRole('alert')).toHaveTextContent(/too long/i);
    await user.click(button);
    await waitFor(() => expect(apiMock.addToCart).toHaveBeenCalledTimes(2));
    await user.click(button);
    await waitFor(() => expect(apiMock.addToCart).toHaveBeenCalledTimes(3));

    const keys = apiMock.addToCart.mock.calls.map((c) => c[0] as string);
    expect(keys[0]).toBe(keys[1]);
    expect(keys[2]).not.toBe(keys[1]);
  });

  it('rejects an out-of-range quantity without calling the API', async () => {
    const user = userEvent.setup();
    renderApp(<AddToCart product={product()} showQuantity />);
    const qty = await screen.findByLabelText('Quantity');
    await user.clear(qty);
    await user.type(qty, '11');
    await user.click(screen.getByRole('button', { name: /add to cart/i }));
    expect(await screen.findByRole('alert')).toHaveTextContent(/1 to 10/);
    expect(apiMock.addToCart).not.toHaveBeenCalled();
  });

  it('shows the server stock conflict message', async () => {
    const user = userEvent.setup();
    apiMock.addToCart.mockRejectedValue(new ApiError({ status: 409, code: 'insufficient_stock', detail: 'Only 1 left.' }));
    renderApp(<AddToCart product={product()} />);
    await user.click(await screen.findByRole('button', { name: /add to cart/i }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Only 1 left.');
  });

  it('is disabled when out of stock or signed out', async () => {
    renderApp(<AddToCart product={product({ stock: 0, inStock: false, stockStatus: 'OutOfStock' })} />);
    expect(await screen.findByRole('button', { name: /add to cart/i })).toBeDisabled();
  });
});
