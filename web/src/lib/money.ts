const formatter = new Intl.NumberFormat('en-IN', {
  style: 'currency',
  currency: 'INR',
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
});

/** The single place rupee amounts are formatted, e.g. 1999 -> "₹1,999.00". */
export function formatRupees(amount: number): string {
  return formatter.format(amount);
}

/** True when the value has at most two decimal places (price rule from the brief). */
export function hasAtMostTwoDecimals(value: number): boolean {
  return Math.abs(Math.round(value * 100) - value * 100) < 1e-7;
}
