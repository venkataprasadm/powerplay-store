import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { CartPage } from './CartPage';
import { apiMock, resetApiMock } from '../test/mockApi';
import { cartOf, line } from '../test/fixtures';
import { renderApp } from '../test/render';

vi.mock('../api/client', async () => (await import('../test/mockApi')).clientMock());

const changed = () => cartOf([line({ productId: 'csk-cap', productName: 'CSK Cap', quantity: 3, acceptedUnitPrice: 499, currentUnitPrice: 549, lineTotal: 1647, priceChanged: true })]);
const accepted = () => cartOf([line({ productId: 'csk-cap', productName: 'CSK Cap', quantity: 3, acceptedUnitPrice: 549, currentUnitPrice: 549, lineTotal: 1647 })]);

beforeEach(() => resetApiMock());

describe('CartPage', () => {
  it('warns about a price change and blocks checkout until the new total is accepted', async () => {
    const user = userEvent.setup();
    apiMock.cart.mockResolvedValue(changed());
    apiMock.acceptChanges.mockResolvedValue(accepted());
    renderApp(<CartPage />);

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('₹1,647.00');
    expect(alert).toHaveTextContent('₹1,497.00');
    expect(screen.getByText(/price changed from ₹499.00 to ₹549.00/i)).toBeInTheDocument();
    const checkout = screen.getByRole('button', { name: 'Proceed to checkout' });
    expect(checkout).toBeDisabled();

    await user.click(screen.getByRole('button', { name: 'Accept new total' }));
    expect(apiMock.acceptChanges).toHaveBeenCalledTimes(1);
    expect(typeof apiMock.acceptChanges.mock.calls[0][0]).toBe('string'); // idempotency key
    expect(await screen.findByRole('button', { name: 'Proceed to checkout' })).toBeEnabled();
    expect(screen.queryByRole('button', { name: 'Accept new total' })).not.toBeInTheDocument();
  });

  it('changes a quantity with an idempotency key', async () => {
    const user = userEvent.setup();
    apiMock.cart.mockResolvedValue(cartOf([line()]));
    apiMock.setQuantity.mockResolvedValue(cartOf([line({ quantity: 3, lineTotal: 5997 })]));
    renderApp(<CartPage />);
    await user.selectOptions(await screen.findByLabelText('Quantity for MI Jersey'), '3');
    expect(apiMock.setQuantity).toHaveBeenCalledWith(expect.any(String), 'mi-jersey', 3);
    expect(await screen.findAllByText('₹5,997.00')).not.toHaveLength(0);
  });

  it('shows a stock problem on the line and disables checkout', async () => {
    apiMock.cart.mockResolvedValue(cartOf([line({ availableStock: 1, insufficientStock: true, stockStatus: 'LowStock' })]));
    renderApp(<CartPage />);
    expect(await screen.findByText(/only 1 available/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Proceed to checkout' })).toBeDisabled();
  });

  it('shows shipping and the total that will be charged', async () => {
    apiMock.cart.mockResolvedValue(cartOf([line({ productId: 'csk-cap', productName: 'CSK Cap', quantity: 1, currentUnitPrice: 499, acceptedUnitPrice: 499, lineTotal: 499 })], { shipping: 99, grandTotal: 598 }));
    renderApp(<CartPage />);
    const summary = await screen.findByRole('complementary', { name: 'Summary' });
    expect(summary).toHaveTextContent('Shipping₹99.00');
    expect(summary).toHaveTextContent('₹598.00');
  });
});
