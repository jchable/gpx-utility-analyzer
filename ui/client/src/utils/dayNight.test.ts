import { describe, it, expect } from 'vitest';
import { hhmmToElapsedSeconds, elapsedSecondsToDayOffset } from './dayNight';

describe('hhmmToElapsedSeconds', () => {
  it('handles a same-day cutoff', () => {
    // Start 06:00, cutoff 14:30 -> 8 h 30 min
    expect(hhmmToElapsedSeconds('06:00', '14:30', 0)).toBe(8.5 * 3600);
  });

  it('handles an overnight cutoff on the first night', () => {
    // Start 18:00 Friday, cutoff 04:00 Saturday -> 10 h
    expect(hhmmToElapsedSeconds('18:00', '04:00', 0)).toBe(10 * 3600);
  });

  it('represents a day-2 cutoff instead of wrapping it into day 1', () => {
    // The reported case: start Friday 18:00, official cutoff Saturday 20:00.
    // 26 h elapsed = 93,600 s. Minute-of-day arithmetic alone yields 7,200 s.
    expect(hhmmToElapsedSeconds('18:00', '20:00', 1)).toBe(26 * 3600);
  });

  it('represents a 46 h cutoff (a normal UTMB-scale finish limit)', () => {
    // Start 18:00, cutoff 16:00 two days later.
    expect(hhmmToElapsedSeconds('18:00', '16:00', 1)).toBe(46 * 3600);
  });

  it('returns null for empty or malformed input', () => {
    expect(hhmmToElapsedSeconds('18:00', '')).toBeNull();
    expect(hhmmToElapsedSeconds('18:00', 'not-a-time')).toBeNull();
  });
});

describe('elapsedSecondsToDayOffset', () => {
  it('round-trips a day-2 cutoff', () => {
    const seconds = 26 * 3600;
    const offset = elapsedSecondsToDayOffset('18:00', seconds);
    expect(offset).toBe(1);
    expect(hhmmToElapsedSeconds('18:00', '20:00', offset)).toBe(seconds);
  });

  it('reports 0 for a same-day cutoff', () => {
    expect(elapsedSecondsToDayOffset('06:00', 8.5 * 3600)).toBe(0);
  });
});
