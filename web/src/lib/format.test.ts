import { addDays, formatDateTime, localDateRangeToUtc, startOfLocalDayUtc } from './time';
import { formatRupees, hasAtMostTwoDecimals } from './money';

describe('formatRupees', () => {
  it('always shows the rupee symbol and two decimals', () => {
    expect(formatRupees(1999)).toBe('₹1,999.00');
    expect(formatRupees(499)).toBe('₹499.00');
    expect(formatRupees(10494)).toBe('₹10,494.00');
  });
  it('uses Indian digit grouping', () => {
    expect(formatRupees(1234567.5)).toBe('₹12,34,567.50');
  });
});

describe('hasAtMostTwoDecimals', () => {
  it('accepts 2dp and rejects 3dp', () => {
    expect(hasAtMostTwoDecimals(549)).toBe(true);
    expect(hasAtMostTwoDecimals(549.05)).toBe(true);
    expect(hasAtMostTwoDecimals(549.055)).toBe(false);
  });
});

describe('time', () => {
  it('shows a UTC instant in IST by default', () => {
    expect(formatDateTime('2026-11-04T08:28:00Z')).toMatch(/1:58\s?pm/i);
  });
  it('can show another zone', () => {
    expect(formatDateTime('2026-11-04T08:28:00Z', 'UTC')).toMatch(/8:28\s?am/i);
  });
  it('converts an IST calendar day start to UTC', () => {
    expect(startOfLocalDayUtc('2026-11-04')).toBe('2026-11-03T18:30:00.000Z');
  });
  it('builds an inclusive-from, exclusive-to UTC range from IST dates', () => {
    expect(localDateRangeToUtc('2026-11-04', '2026-11-04')).toEqual({
      from: '2026-11-03T18:30:00.000Z',
      to: '2026-11-04T18:30:00.000Z',
    });
    expect(localDateRangeToUtc()).toEqual({ from: undefined, to: undefined });
  });
  it('adds days across month ends', () => {
    expect(addDays('2026-11-30', 1)).toBe('2026-12-01');
  });
});
