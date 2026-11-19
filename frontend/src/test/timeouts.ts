// Measured with the full suite under load (mutation runs; 12 busy processes on 8 logical cores): the gallery's
// lazy route took up to 1.4 s to appear, and the first render of the gallery and of the app up to 5.7 s. The
// defaults (1 s for findBy, 5 s per test) leave no margin there, so only the tests that pay those costs wait longer.

/** For a findBy that waits on a lazily imported route or on the app's first render. */
export const SLOW_RENDER_WAIT_MS = 10_000

/** Per test. Above SLOW_RENDER_WAIT_MS, so a real failure reports what findBy did not find, not a bare timeout. */
export const SLOW_TEST_TIMEOUT_MS = 15_000
