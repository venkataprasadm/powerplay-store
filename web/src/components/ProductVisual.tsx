import type { ReactNode } from 'react';
import type { ProductType } from '../api/types';
import { paletteFor } from '../lib/theme';

/** Flat product illustrations on a 120 x 120 grid. `fill` parts take the team colour, `trim` parts the second colour. */
const ART: Record<ProductType, ReactNode> = {
  Jersey: (
    <>
      <path className="fill" d="M43 17 30 21 11 36l9 19 12-6v51a5 5 0 0 0 5 5h46a5 5 0 0 0 5-5V49l12 6 9-19-19-15-13-4c-3 9-9 14-17 14s-14-5-17-14Z" />
      <path className="trim" d="M43 17c3 9 9 14 17 14s14-5 17-14" fill="none" strokeWidth="4" strokeLinecap="round" />
      <path className="shade" d="M32 49v51a5 5 0 0 0 5 5h8V44Z" />
    </>
  ),
  Cap: (
    <>
      <path className="fill" d="M22 76c0-27 16-46 38-46s38 19 38 46Z" />
      <path className="shade" d="M22 76c0-27 16-46 38-46-12 6-19 24-19 46Z" />
      <path className="trim" d="M14 78h92c5 0 6 6 1 9-10 6-27 9-47 9s-37-3-47-9c-5-3-4-9 1-9Z" />
      <circle className="trim" cx="60" cy="30" r="4" />
    </>
  ),
  Flag: (
    <>
      <rect className="trim" x="22" y="12" width="5" height="98" rx="2.5" />
      <path className="fill" d="M27 20c12-7 24-7 36 0s24 7 36 0v44c-12 7-24 7-36 0s-24-7-36 0Z" />
      <path className="shade" d="M63 20c12 7 24 7 36 0v44c-12 7-24 7-36 0Z" />
    </>
  ),
  SignedPhotograph: (
    <>
      <rect className="trim" x="22" y="12" width="76" height="96" rx="5" />
      <rect className="fill" x="30" y="20" width="60" height="64" rx="2" />
      <circle className="shade" cx="60" cy="44" r="10" />
      <path className="shade" d="M40 84c0-14 9-22 20-22s20 8 20 22Z" />
      <path className="ink" d="M36 99c5-6 8-6 9-1 1 4 4 4 8-2 3-4 5-3 6 1 1 3 4 3 8 0 4-2 9-3 17-2" fill="none" strokeWidth="2.2" strokeLinecap="round" />
    </>
  ),
  Other: (
    <>
      <path className="fill" d="M36 16h48v24c0 16-10 27-24 29-14-2-24-13-24-29Z" />
      <path className="trim" d="M36 24H24c0 14 6 22 14 24M84 24h12c0 14-6 22-14 24" fill="none" strokeWidth="5" strokeLinecap="round" />
      <path className="trim" d="M54 68h12v14H54Z" />
      <rect className="fill" x="40" y="82" width="40" height="12" rx="3" />
      <path className="shade" d="M60 16h24v24c0 16-10 27-24 29Z" />
    </>
  ),
};

interface Props { type: ProductType; franchiseId: string; className?: string }

/** Decorative product image: a flat illustration in the team's colours. The product name is always given as text nearby. */
export function ProductVisual({ type, franchiseId, className = '' }: Props) {
  const { primary, secondary, glow } = paletteFor(franchiseId);
  return (
    <div
      className={`product-visual ${className}`.trim()} aria-hidden="true"
      style={{ ['--tint' as string]: primary, ['--trim' as string]: secondary, ['--glow' as string]: glow }}
    >
      <svg viewBox="0 0 120 120" focusable="false">{ART[type]}</svg>
      <span className="product-visual-tag">{franchiseId.toUpperCase()}</span>
    </div>
  );
}
