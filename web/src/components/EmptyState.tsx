import type { ReactNode } from 'react';
import { BagIcon } from './Icons';

export function EmptyState({ children }: { children: ReactNode }) {
  return <div className="empty-state"><span className="empty-icon"><BagIcon /></span><p>{children}</p></div>;
}
