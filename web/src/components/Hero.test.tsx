import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Hero } from './Hero';
import { renderApp } from '../test/render';

describe('Hero', () => {
  it('offers one tile per team and reports the picked team', async () => {
    const user = userEvent.setup();
    const onPickTeam = vi.fn();
    renderApp(<Hero onPickTeam={onPickTeam} />, { user: null });
    const group = screen.getByRole('group', { name: /shop by team/i });
    expect(group.querySelectorAll('button')).toHaveLength(4);

    await user.click(screen.getByRole('button', { name: /Royal Challengers Bengaluru/ }));
    expect(onPickTeam).toHaveBeenCalledWith('rcb');
  });

  it('names the store and does not claim any affiliation', () => {
    renderApp(<Hero onPickTeam={() => {}} />, { user: null });
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('The PowerPlay collection');
    expect(screen.queryByText(/official/i)).not.toBeInTheDocument();
  });
});
