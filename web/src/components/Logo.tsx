/** Original brand mark: a bat meeting the ball, with a motion trail. Decorative inside the home link, which carries the accessible name. */
export function Logo() {
  return (
    <svg className="logo-mark" viewBox="0 0 64 64" aria-hidden="true" focusable="false">
      <defs><linearGradient id="pp-logo-g" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stopColor="#f0dfb2"/><stop offset="1" stopColor="#b48a42"/></linearGradient></defs>
  <g transform="rotate(42 32 33)">
    <rect x="30.1" y="1" width="3.8" height="23" rx="1.9" fill="url(#pp-logo-g)"/>
    <path d="M30.1 5.5h3.8M30.1 8.5h3.8M30.1 11.5h3.8" stroke="#0a0a0b" strokeOpacity=".35" strokeWidth=".9"/>
    <path d="M28 23.5h8c1.6 0 2.6 1.6 2.6 3.2v24.2a4.6 4.6 0 0 1-4.6 4.6h-4a4.6 4.6 0 0 1-4.6-4.6V26.7c0-1.6 1-3.2 2.6-3.2z" fill="url(#pp-logo-g)"/>
    <path d="M32 28v24" stroke="#0a0a0b" strokeOpacity=".3" strokeWidth="1.2" strokeLinecap="round"/>
  </g>
  <circle cx="11.5" cy="13" r="6.2" fill="currentColor"/>
  <path d="M6.6 10.6c2.4 1.2 3.7 3.8 3.6 6.6M11.6 7.1c2.8 1.5 4.3 4.5 4.1 7.7" fill="none" stroke="#b48a42" strokeWidth="1.1" strokeLinecap="round"/>
  <path d="M25 30.5C20 28.5 15.5 24.5 14.8 19.5" fill="none" stroke="url(#pp-logo-g)" strokeWidth="1.8" strokeLinecap="round" strokeDasharray=".4 3.7"/>
    </svg>
  );
}
