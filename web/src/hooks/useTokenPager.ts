import { useState } from 'react';

/**
 * Continuation-token paging. The API only moves forward, so "Previous" is a stack of the tokens
 * we already used. Nothing is cached: going back re-fetches (cheap, and always fresh).
 */
export function useTokenPager() {
  const [tokens, setTokens] = useState<Array<string | undefined>>([undefined]);
  return {
    token: tokens[tokens.length - 1],
    pageNumber: tokens.length,
    hasPrevious: tokens.length > 1,
    next: (nextToken: string) => setTokens((t) => [...t, nextToken]),
    previous: () => setTokens((t) => (t.length > 1 ? t.slice(0, -1) : t)),
    reset: () => setTokens([undefined]),
  };
}
