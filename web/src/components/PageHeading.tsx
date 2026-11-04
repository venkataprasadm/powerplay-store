import { useEffect, useRef } from 'react';

/** Moves focus to the page title on route change (announces the new page) and sets the tab title. */
export function PageHeading({ title, className }: { title: string; className?: string }) {
  const ref = useRef<HTMLHeadingElement>(null);
  useEffect(() => {
    ref.current?.focus({ preventScroll: true });
    document.title = `${title} - PowerPlay Store`;
  }, [title]);
  return <h1 ref={ref} tabIndex={-1} className={className}>{title}</h1>;
}
