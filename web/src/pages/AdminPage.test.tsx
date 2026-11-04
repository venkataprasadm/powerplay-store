import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AdminPage } from './AdminPage';
import { apiMock, resetApiMock } from '../test/mockApi';
import { emptyCart, product } from '../test/fixtures';
import { renderApp } from '../test/render';
import { ApiError } from '../api/errors';

vi.mock('../api/client', async () => (await import('../test/mockApi')).clientMock());

const cap = () => product({ id: 'csk-cap', name: 'CSK Cap', price: 499, stock: 100, etag: '"v1"' });

beforeEach(() => {
  resetApiMock();
  apiMock.cart.mockResolvedValue(emptyCart());
  apiMock.products.mockResolvedValue({ items: [cap()], nextPageToken: null, totalCount: 1 });
});

async function editPrice(user: ReturnType<typeof userEvent.setup>, value: string) {
  const price = await screen.findByLabelText('Price (₹)');
  await user.clear(price);
  await user.type(price, value);
  await user.type(screen.getByLabelText('Reason'), 'Demand');
  await user.click(screen.getByRole('button', { name: 'Save changes' }));
}

describe('AdminPage', () => {
  it('saves with the current ETag and sends only changed fields', async () => {
    const user = userEvent.setup();
    apiMock.updateProduct.mockResolvedValue({ ...cap(), price: 549, etag: '"v2"' });
    renderApp(<AdminPage />, { user: 'admin' });
    await editPrice(user, '549');
    expect(apiMock.updateProduct).toHaveBeenCalledWith('csk-cap', '"v1"', { price: 549, reason: 'Demand' });
    expect(await screen.findByText(/saved/i)).toBeInTheDocument();
  });

  it('shows a conflict message on 412 and reloads the latest version on request', async () => {
    const user = userEvent.setup();
    apiMock.updateProduct.mockRejectedValue(new ApiError({ status: 412, code: 'precondition_failed' }));
    apiMock.product.mockResolvedValue({ ...cap(), price: 519, etag: '"v9"' });
    renderApp(<AdminPage />, { user: 'admin' });
    await editPrice(user, '549');

    expect(await screen.findByRole('alert')).toHaveTextContent(/changed by someone else/i);
    await user.click(screen.getByRole('button', { name: 'Reload latest' }));
    await waitFor(() => expect(screen.getByLabelText('Price (₹)')).toHaveValue('519'));
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('validates fields before calling the API', async () => {
    const user = userEvent.setup();
    renderApp(<AdminPage />, { user: 'admin' });
    await editPrice(user, '549.999');
    expect(await screen.findByText(/at most two decimal places/i)).toBeInTheDocument();
    expect(screen.getByLabelText('Price (₹)')).toHaveAttribute('aria-invalid', 'true');
    expect(apiMock.updateProduct).not.toHaveBeenCalled();
  });

  it('renders server side field errors next to the field', async () => {
    const user = userEvent.setup();
    apiMock.updateProduct.mockRejectedValue(new ApiError({ status: 400, code: 'validation_failed', errors: { stock: ['Stock cannot exceed 100000.'] } }));
    renderApp(<AdminPage />, { user: 'admin' });
    const stock = await screen.findByLabelText('Stock quantity');
    await user.clear(stock);
    await user.type(stock, '5');
    await user.type(screen.getByLabelText('Reason'), 'Count');
    await user.click(screen.getByRole('button', { name: 'Save changes' }));
    expect(await screen.findByText('Stock cannot exceed 100000.')).toBeInTheDocument();
  });

  it('refuses non-admins', async () => {
    renderApp(<AdminPage />, { user: 'asha' });
    expect(await screen.findByText(/admin access required/i)).toBeInTheDocument();
    expect(apiMock.products).not.toHaveBeenCalled();
  });
});
