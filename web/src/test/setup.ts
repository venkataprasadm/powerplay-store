import '@testing-library/jest-dom/vitest';
import * as axeMatchers from 'vitest-axe/matchers';
import { afterEach, expect } from 'vitest';
import { cleanup } from '@testing-library/react';

expect.extend(axeMatchers);
afterEach(() => {
  cleanup();
  sessionStorage.clear();
});

// jsdom does not implement these; stub them so test output only shows real problems.
window.scrollTo = (() => {}) as typeof window.scrollTo;
HTMLCanvasElement.prototype.getContext = (() => null) as unknown as typeof HTMLCanvasElement.prototype.getContext;
