/** Small decorative line icons. Always aria-hidden: the surrounding control carries the accessible name. */
const base = { viewBox: '0 0 24 24', fill: 'none', stroke: 'currentColor', strokeWidth: 1.8, strokeLinecap: 'round', strokeLinejoin: 'round', 'aria-hidden': true, focusable: false } as const;

export const CartIcon = () => (
  <svg {...base} className="icon"><path d="M3 4h2l2.4 11.2a1.5 1.5 0 0 0 1.5 1.2h8.6a1.5 1.5 0 0 0 1.5-1.1L21 8H6.2" /><circle cx="9.5" cy="20" r="1.2" /><circle cx="17.5" cy="20" r="1.2" /></svg>
);
export const CheckIcon = () => (
  <svg {...base} className="icon"><path d="m5 12.5 4.2 4.2L19 7" /></svg>
);
export const CloseIcon = () => (
  <svg {...base} className="icon"><path d="M6 6l12 12M18 6 6 18" /></svg>
);
export const BagIcon = () => (
  <svg {...base} className="icon"><path d="M5 8h14l-1 12H6Z" /><path d="M9 8V6a3 3 0 0 1 6 0v2" /></svg>
);
