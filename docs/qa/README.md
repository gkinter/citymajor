# QA screenshots

Reference captures from `/play` smoke runs.

## Playwright black-canvas limitation

Headless Chromium (Playwright) cannot read WebGL canvas pixels: `readPixels`, `canvas2d` `drawImage`, and `toDataURL` return all-zero even when the city renders correctly in a normal browser. Screenshots in this folder may therefore show a **black canvas** while the HUD still reports healthy building/chunk counts.

Smoke tests treat the diagnostics HUD as authoritative for render health; see `web/scripts/smoke-play-checks.mjs` (`assertCanvasRenderHealth`).

| File | Notes |
|------|-------|
| `citymajor-play-default-e4d707c.png` | Default camera after load (`e4d707c` build) |
| `citymajor-play-citizen-dots-zoomed.png` | Citizen-dots overlay, zoomed view |
