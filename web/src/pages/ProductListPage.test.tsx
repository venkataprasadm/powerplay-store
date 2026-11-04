import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ProductListPage } from './ProductListPage';
import { apiMock, resetApiMock } from '../test/mockApi';
import { emptyCart, product } from '../test/fixtures';
import { renderApp } from '../test/render';
import { ApiError } from '../api/errors';

vi.mock('../api/client', async () => (await import('../test/mockApi')).clientMock());

const page1 = { items: [product(), product({ id: 'csk-cap', name: 'CSK Cap', price: 499, type: 'Cap', franchiseId: 'csk', franchiseName: 'Chennai Super Kings' })], nextPageToken: 'tok2', totalCount: 4 };
const page2 = { items: [product({ id: 'rcb-flag', name: 'RCB Flag', price: 299 })], nextPageToken: null, totalCount: 4 };

beforeEach(() => {
  resetApiMock();
  apiMock.franchises.mockResolvedValue([{ id: 'csk', name: 'Chennai Super Kings' }, { id: 'mi', name: 'Mumbai Indians' }]);
  apiMock.cart.mockResolvedValue(emptyCart());
  apiMock.products.mockResolvedValue(page1);
});

describe('ProductListPage', () => {
  it('shows products with rupee prices and the total count', async () => {
    renderApp(<ProductListPage />);
    expect(await screen.findByText('₹1,999.00')).toBeInTheDocument();
    expect(screen.getByText('₹499.00')).toBeInTheDocument();
    expect(await screen.findByText(/showing 2 of 4 products/i)).toBeInTheDocument();
  });

  it('combines name, type and franchise filters in one query and resets to the first page', async () => {
    const user = userEvent.setup();
    renderApp(<ProductListPage />);
    await screen.findByText('₹1,999.00');
    await user.type(screen.getByLabelText('Search by name'), 'cap');
    await user.selectOptions(screen.getByLabelText('Type'), 'Cap');
    await user.selectOptions(screen.getByLabelText('Franchise'), 'csk');
    await user.click(screen.getByRole('button', { name: 'Search' }));
    await waitFor(() => expect(apiMock.products).toHaveBeenLastCalledWith(
      expect.objectContaining({ q: 'cap', type: 'Cap', franchise: 'csk', pageToken: undefined })));
  });

  it('pages forward with the continuation token and back again', async () => {
    const user = userEvent.setup();
    apiMock.products.mockImplementation(async (q: { pageToken?: string }) => (q.pageToken === 'tok2' ? page2 : page1));
    renderApp(<ProductListPage />);
    await screen.findByText('₹1,999.00');
    expect(screen.getByRole('button', { name: 'Previous' })).toBeDisabled();

    await user.click(screen.getByRole('button', { name: 'Next' }));
    expect(await screen.findByRole('link', { name: 'RCB Flag' })).toBeInTheDocument();
    expect(apiMock.products).toHaveBeenLastCalledWith(expect.objectContaining({ pageToken: 'tok2' }));
    expect(screen.getByRole('button', { name: 'Next' })).toBeDisabled();

    await user.click(screen.getByRole('button', { name: 'Previous' }));
    expect(await screen.findByRole('link', { name: 'MI Jersey' })).toBeInTheDocument();
  });

  it('shows a failure with a Retry that reloads', async () => {
    const user = userEvent.setup();
    apiMock.products.mockRejectedValueOnce(new ApiError({ status: 503, code: 'dependency_unavailable' }));
    renderApp(<ProductListPage />);
    expect(await screen.findByRole('alert')).toHaveTextContent(/temporarily unavailable/i);
    await user.click(screen.getByRole('button', { name: 'Retry' }));
    expect(await screen.findByText('₹1,999.00')).toBeInTheDocument();
  });

  it('changes the page size and starts again from the first page', async () => {
    const user = userEvent.setup();
    renderApp(<ProductListPage />);
    await screen.findByText('₹1,999.00');
    expect(apiMock.products).toHaveBeenLastCalledWith(expect.objectContaining({ pageSize: 6 }));
    await user.selectOptions(screen.getByLabelText('Per page'), '2');
    await waitFor(() => expect(apiMock.products).toHaveBeenLastCalledWith(expect.objectContaining({ pageSize: 2, pageToken: undefined })));
  });
});
