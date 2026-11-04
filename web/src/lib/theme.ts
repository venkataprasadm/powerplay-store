/** Franchise colours tint product imagery and the team tiles. Unknown franchises fall back to neutral grey. */
export interface Palette { primary: string; secondary: string; glow: string }

const PALETTES: Record<string, Palette> = {
  mi: { primary: '#0a3f8f', secondary: '#c9a96a', glow: '#3b78d8' },
  csk: { primary: '#e0ac00', secondary: '#0a3f8f', glow: '#f0c53a' },
  rcb: { primary: '#b0121f', secondary: '#16161a', glow: '#d8505c' },
  kkr: { primary: '#3c2468', secondary: '#c9a96a', glow: '#7d5cc4' },
};
const FALLBACK: Palette = { primary: '#6b7280', secondary: '#e5e7eb', glow: '#9ca3af' };

export const paletteFor = (franchiseId: string): Palette => PALETTES[franchiseId] ?? FALLBACK;
export const ALL_PALETTES: readonly Palette[] = Object.values(PALETTES);

/** Franchises in a fixed order, for the "shop by team" tiles. */
const NAMES: Record<string, string> = { mi: 'Mumbai Indians', csk: 'Chennai Super Kings', rcb: 'Royal Challengers Bengaluru', kkr: 'Kolkata Knight Riders' };
export const FEATURED: ReadonlyArray<{ id: string; short: string; name: string; palette: Palette }> =
  Object.entries(PALETTES).map(([id, palette]) => ({ id, short: id.toUpperCase(), name: NAMES[id] ?? id.toUpperCase(), palette }));
