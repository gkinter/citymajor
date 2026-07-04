# SB-3693 — Auth, accounts & Founder Pass entitlements (v1 gap)

> **Linear:** [SB-3693](https://linear.app/softblaze/issue/SB-3693/auth-accounts-and-founder-pass-entitlements)  
> **Branch:** `feat/wasm-r3f-integration-2026-07-04` (`citymajor-web-r3f-spike` worktree)  
> **Key commit:** `f0875a0` — production-hardened Stripe checkout + webhook → tier-store path

Minimal v1 assessment: what ships today vs what stays deferred until Supabase OAuth (v1.5).

---

## Done in v1 (anonymous identity + paid tier plumbing)

### HMAC-signed user cookie (`web/lib/user-identity.ts`)

| Piece | Status |
|-------|--------|
| Cookie `citymajor_uid` = `{uuid}.{hmac-sha256-base64url}` | ✅ |
| HTTP-only, `SameSite=Lax`, `Secure` in production | ✅ |
| `CITYMAJOR_SESSION_SECRET` required ≥32 chars in production | ✅ |
| `ensureUserId()` mints UUID on first visit; routes attach cookie | ✅ |
| **No** trust of client headers (`x-citymajor-user` removed) | ✅ |

Wired into: `/api/saves`, `/api/saves/:id`, `/api/me/entitlements`, `/api/checkout/founder-pass`, `/api/narrative/event`.

### Tier store (`web/lib/tier-store.ts`)

| Piece | Status |
|-------|--------|
| `getStoredTier(userKey)` / `setStoredTier(userKey, tier)` | ✅ |
| Dev persistence: `.data/tiers.json` (+ in-memory fallback) | ✅ stub |
| Key = HMAC cookie UUID (same as save ownership) | ✅ |

### Tier resolution (`web/lib/resolve-tier.ts`)

| Environment | Resolution order |
|-------------|------------------|
| **Production** | tier-store by signed cookie → `free` |
| **Non-production** | mock `citymajor_tier` cookie → tier-store → `X-CityMajor-Tier` header → `free` |

Production intentionally blocks self-grant of `founder_pass` via mock cookie or dev header.

### Stripe webhook path (`f0875a0`)

| Piece | Status |
|-------|--------|
| `POST /api/webhooks/stripe` — signature verify via `STRIPE_WEBHOOK_SECRET` | ✅ |
| `web/lib/stripe-webhook.ts` — `checkout.session.completed` → `setStoredTier(userKey, "founder_pass")` | ✅ |
| Idempotency ledger (in-memory `processedEventIds`) | ✅ v1 stub |
| Checkout binds identity: `client_reference_id` + `metadata.userKey` from `ensureUserId()` | ✅ |
| Refund / subscription revoke handlers | ⏸ v1.5 (`ignored`) |

### Entitlements API

| Piece | Status |
|-------|--------|
| `GET /api/me/entitlements` — tier from `resolveTierFromRequest` + narrative quota | ✅ |
| `POST /api/me/entitlements` — mock tier cookie | ✅ dev-only (404 in production) |

---

## Deferred — Supabase OAuth / v1.5 (not in scope for v1 spike)

These are **explicitly out of v1** per [LIVE_SERVICES_ARCHITECTURE.md](./LIVE_SERVICES_ARCHITECTURE.md). Do not block M3 gameplay on them.

| Gap | Why deferred | Target |
|-----|--------------|--------|
| **Supabase Auth (OAuth / magic link)** | v1 uses anonymous HMAC cookie; no login UI | v1.5 |
| **`auth.uid()` as user key** | Saves/tiers keyed by random UUID, not account | v1.5 + RLS on `save_slots` |
| **Account ↔ Stripe customer link** | Checkout uses session UUID; no Customer portal / cross-device restore | v1.5 |
| **Durable tier store** | File stub lost on redeploy; no multi-instance consistency | Postgres (Supabase) or Redis |
| **Durable webhook idempotency** | In-memory set resets on restart → duplicate grant risk | Stripe `event.id` table |
| **Refund / chargeback revocation** | Stub returns `ignored` for `charge.refunded` | v1.5 |
| **Cloud saves (R2 + signed URLs)** | Local `.data/saves.json` | v1.5 ([SAVE_FORMAT_WEB.md](./SAVE_FORMAT_WEB.md)) |

### v1.5 migration sketch (when OAuth lands)

1. Add Supabase Auth middleware; on first login, optionally merge anonymous `citymajor_uid` saves into `auth.uid()` (one-time migration endpoint).
2. Replace `tier-store` file with `entitlements` row keyed by `auth.uid()`; backfill from Stripe `metadata.userKey` → `auth.uid()` mapping at checkout.
3. Move webhook idempotency to `stripe_events_processed(event_id)` table.
4. Keep HMAC cookie as **session binding for pre-login** checkout only, or drop once OAuth session JWT is canonical.

---

## Env vars (production checklist)

```bash
# Required for signed identity in production
CITYMAJOR_SESSION_SECRET=   # ≥32 chars

# Required for live Founder Pass checkout
STRIPE_SECRET_KEY=
STRIPE_FOUNDER_PASS_PRICE_ID=
STRIPE_WEBHOOK_SECRET=      # Dashboard → /api/webhooks/stripe
```

---

## Acceptance for v1 “done” (this ticket’s stub bar)

- [x] Anonymous user identity is server-issued and tamper-evident (HMAC cookie)
- [x] Paid tier granted only via verified Stripe webhook → tier-store (production)
- [x] Checkout session carries user key for webhook correlation
- [x] Dev/mock paths documented and gated off in production
- [ ] Supabase OAuth — **deferred v1.5**
- [ ] Persistent cross-deploy entitlement store — **deferred v1.5**
