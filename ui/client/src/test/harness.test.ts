import { describe, it, expect } from 'vitest';

describe('vitest harness', () => {
  it('runs in a jsdom environment with localStorage', () => {
    localStorage.setItem('probe', 'ok');
    expect(localStorage.getItem('probe')).toBe('ok');
    expect(typeof window).toBe('object');
  });
});
