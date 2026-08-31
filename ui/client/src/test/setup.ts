import '@testing-library/jest-dom/vitest';

// jsdom has no localStorage quota and no navigation; reset between tests.
afterEach(() => {
  localStorage.clear();
  sessionStorage.clear();
});
