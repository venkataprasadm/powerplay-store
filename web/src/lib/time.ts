/** Timestamps are stored and transferred in UTC; this is only the display zone. */
export const DISPLAY_TIME_ZONE = 'Asia/Kolkata';

export function formatDateTime(iso: string, timeZone: string = DISPLAY_TIME_ZONE): string {
  return new Intl.DateTimeFormat('en-IN', { dateStyle: 'medium', timeStyle: 'short', timeZone }).format(new Date(iso));
}

function offsetMs(timeZone: string, utcMs: number): number {
  const parts = new Intl.DateTimeFormat('en-US', {
    timeZone, hourCycle: 'h23', year: 'numeric', month: '2-digit', day: '2-digit',
    hour: '2-digit', minute: '2-digit', second: '2-digit',
  }).formatToParts(new Date(utcMs));
  const get = (type: string) => Number(parts.find((p) => p.type === type)?.value);
  const asUtc = Date.UTC(get('year'), get('month') - 1, get('day'), get('hour'), get('minute'), get('second'));
  return asUtc - Math.floor(utcMs / 1000) * 1000;
}

/** UTC instant of 00:00 local time on `date` (YYYY-MM-DD) in the given zone. Second pass handles DST shifts. */
export function startOfLocalDayUtc(date: string, timeZone: string = DISPLAY_TIME_ZONE): string {
  const [y, m, d] = date.split('-').map(Number);
  const guess = Date.UTC(y, m - 1, d);
  const first = guess - offsetMs(timeZone, guess);
  return new Date(guess - offsetMs(timeZone, first)).toISOString();
}

export function addDays(date: string, days: number): string {
  const [y, m, d] = date.split('-').map(Number);
  return new Date(Date.UTC(y, m - 1, d + days)).toISOString().slice(0, 10);
}

/** Inclusive local dates -> API range: `from` inclusive, `to` exclusive (start of the day after). */
export function localDateRangeToUtc(from?: string, to?: string, timeZone: string = DISPLAY_TIME_ZONE) {
  return {
    from: from ? startOfLocalDayUtc(from, timeZone) : undefined,
    to: to ? startOfLocalDayUtc(addDays(to, 1), timeZone) : undefined,
  };
}
