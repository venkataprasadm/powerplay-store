import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { OrdersPage } from './OrdersPage';
import { apiMock, resetApiMock } from '../test/mockApi';
import { emptyCart, order } from '../test/fixtures';
import { renderApp } from '../test/render';

vi.mock('../api/client', async () => (await import('../test/mockApi')).clientMock());

beforeEach(() => {
  resetApiMock();
  apiMock.cart.mockResolvedValue(emptyCart());
  apiMock.orders.mockResolvedValue({ items: [order()], nextPageToken: 'older' });
});

describe('OrdersPage', () => {
  it('shows the price paid at purchase time and the time in IST', async () => {
    renderApp(<OrdersPage />);
    expect(await screen.findByText('₹499.00')).toBeInTheDocument();
    expect(screen.getByText(/1:58\s?pm/i)).toBeInTheDocument();
  });

  it('sends the IST day range as UTC instants and pages with the token', async () => {
    const user = userEvent.setup();
    renderApp(<OrdersPage />);
    await screen.findByText('₹499.00');
    await user.type(screen.getByLabelText('From date'), '2026-11-04');
    await user.type(screen.getByLabelText('To date'), '2026-11-04');
    await user.click(screen.getByRole('button', { name: 'Apply' }));
    await waitFor(() => expect(apiMock.orders).toHaveBeenLastCalledWith(expect.objectContaining({
      from: '2026-11-03T18:30:00.000Z', to: '2026-11-04T18:30:00.000Z', pageToken: undefined })));
    await user.click(await screen.findByRole('button', { name: 'Older' }));
    await waitFor(() => expect(apiMock.orders).toHaveBeenLastCalledWith(expect.objectContaining({ pageToken: 'older' })));
  });

  it('rejects an end date before the start date', async () => {
    const user = userEvent.setup();
    renderApp(<OrdersPage />);
    await screen.findByText('₹499.00');
    await user.type(screen.getByLabelText('From date'), '2026-11-04');
    await user.type(screen.getByLabelText('To date'), '2026-11-01');
    await user.click(screen.getByRole('button', { name: 'Apply' }));
    expect(await screen.findByRole('alert')).toHaveTextContent(/on or after/i);
  });
});
