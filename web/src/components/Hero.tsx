import { FEATURED } from '../lib/theme';
import { PageHeading } from './PageHeading';

/** Landing banner with a "shop by team" grid. Picking a team filters the collection below and scrolls to it. */
export function Hero({ onPickTeam }: { onPickTeam: (franchiseId: string) => void }) {
  return (
    <section className="hero" aria-labelledby="hero-title">
      <div className="hero-copy">
        <p className="pill">Cricket merchandise for IPL fans</p>
        <div id="hero-title"><PageHeading title="The PowerPlay collection" className="hero-title" /></div>
        <p className="hero-lede">Jerseys, caps, flags and limited-edition signed photographs, for every team you back.</p>
        <div className="hero-actions">
          <a className="cta" href="#collection">Shop the collection</a>
          <a className="cta cta-ghost" href="#teams">Shop by team</a>
        </div>
      </div>
      <div id="teams" className="team-grid" role="group" aria-label="Shop by team">
        {FEATURED.map((t) => (
          <button
            key={t.id} type="button" className="team-tile"
            style={{ ['--tint' as string]: t.palette.primary, ['--glow' as string]: t.palette.glow }}
            onClick={() => onPickTeam(t.id)}
          >
            <span className="team-tile-code" aria-hidden="true">{t.short}</span>
            <span className="team-tile-name">{t.name}</span>
            <span className="team-tile-cta" aria-hidden="true">Shop now <span className="arrow">→</span></span>
          </button>
        ))}
      </div>
    </section>
  );
}
