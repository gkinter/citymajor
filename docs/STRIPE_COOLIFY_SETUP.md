# Stripe on `citymajor-web` (Coolify preview)

Quick ops runbook for enabling **test-mode** Founder Pass checkout on the CityMajor web preview. No secrets in this doc — paste real values from the [Stripe Dashboard](https://dashboard.stripe.com) only in your shell or Coolify UI.

**Full deploy context:** [`DEPLOY_WEB.md`](./DEPLOY_WEB.md) (Docker, WASM, E2E checkout, Stripe CLI local dev, troubleshooting).

| Item | Value |
|------|-------|
| Coolify app | `citymajor-web` |
| App UUID | `w134tsftvj327kp96j45kcsb` |
| Preview FQDN | `https://citymajor.apps.softblaze.net` |
| Webhook path | `https://citymajor.apps.softblaze.net/api/webhooks/stripe` |
| Linear | [SB-3714](https://linear.app/softblaze/issue/SB-3714) |

Omit all Stripe vars to keep **mock cookie checkout** on `/shop` (valid for M0 preview smoke).

## Required environment variables

| Key | Build time | Runtime | Required for | Source (test mode) |
|-----|------------|---------|--------------|-------------------|
| `STRIPE_SECRET_KEY` | No | **Yes** | Checkout + webhooks | Dashboard → Developers → API keys → Secret key (`sk_test_…`) |
| `STRIPE_FOUNDER_PASS_PRICE_ID` | No | **Yes** | Checkout | Dashboard → Product catalog → Founder Pass price ID (`price_…`) |
| `NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY` | **Yes** | No | Browser Stripe.js | Dashboard → API keys → Publishable key (`pk_test_…`) |
| `STRIPE_WEBHOOK_SECRET` | No | **Yes** | Webhook signature verify | Dashboard → Webhooks → endpoint → Signing secret (`whsec_…`) |
| `CITYMAJOR_SESSION_SECRET` | No | **Yes** | `citymajor_uid` cookie (webhook `userKey`) | Generate locally (≥32 chars); never commit |

**Checkout ready** when `STRIPE_SECRET_KEY` + `STRIPE_FOUNDER_PASS_PRICE_ID` are set.  
**Webhook ready** additionally needs `STRIPE_WEBHOOK_SECRET` (register the preview URL in Dashboard first — see [`DEPLOY_WEB.md` §3](./DEPLOY_WEB.md#3-stripe-dashboard--webhook-endpoint-preview)).

`NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY` must be marked **Available at Buildtime** in Coolify (or Docker build arg). Changing it requires **redeploy/rebuild**, not just restart.

## Add vars via `coolify env-set` on Beast

Run from your laptop; secrets stay in your shell — not in git or this doc.

```bash
APP=citymajor-web
FQDN="https://citymajor.apps.softblaze.net"

# Paste test-mode values from Stripe Dashboard (do not commit)
export STRIPE_SECRET_KEY='sk_test_REPLACE_ME'
export STRIPE_FOUNDER_PASS_PRICE_ID='price_REPLACE_ME'
export NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY='pk_test_REPLACE_ME'
export CITYMAJOR_SESSION_SECRET='REPLACE_WITH_32_PLUS_CHAR_RANDOM_STRING'

# Phase A — checkout keys (runtime + publishable)
ssh beast "/home/devops/bin/coolify env-set $APP STRIPE_SECRET_KEY '$STRIPE_SECRET_KEY'"
ssh beast "/home/devops/bin/coolify env-set $APP STRIPE_FOUNDER_PASS_PRICE_ID '$STRIPE_FOUNDER_PASS_PRICE_ID'"
ssh beast "/home/devops/bin/coolify env-set $APP CITYMAJOR_SESSION_SECRET '$CITYMAJOR_SESSION_SECRET'"
ssh beast "/home/devops/bin/coolify env-set $APP NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY '$NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY'"

# NEXT_PUBLIC_* defaults to runtime-only via CLI — enable Buildtime in Coolify UI for that key, then:
ssh beast "/home/devops/bin/coolify deploy $APP --force"
```

**Phase B — webhook** (after Dashboard endpoint `…/api/webhooks/stripe` exists):

```bash
export STRIPE_WEBHOOK_SECRET='whsec_REPLACE_ME'   # Dashboard signing secret, NOT stripe listen

ssh beast "/home/devops/bin/coolify env-set $APP STRIPE_WEBHOOK_SECRET '$STRIPE_WEBHOOK_SECRET'"
ssh beast "/home/devops/bin/coolify deploy $APP --force"
```

Use single-quoted values inside the SSH command so `whsec_…` special characters are preserved. Quote `"$VAR"` when exporting locally.

**Verify keys are registered** (no secrets printed):

```bash
ssh beast "/home/devops/bin/coolify env $APP" | grep -E 'STRIPE_|CITYMAJOR_SESSION'
```

**Alternative:** authenticated Mac wrapper `~/bin/coolify env-set …` hits the same Coolify API; Beast path above is authoritative when Mac returns 401.

## Test via `GET /api/shop`

Primary deploy smoke — safe to run in CI or from any machine; response never includes secret values.

```bash
FQDN="https://citymajor.apps.softblaze.net"

# Full probe
curl -s "$FQDN/api/shop" | jq .

# Minimal gate
curl -s "$FQDN/api/shop" | jq '{stripeCheckoutEnabled, publishableKeyConfigured, missing}'
```

**Expected when Stripe checkout is live:**

```json
{
  "stripeCheckoutEnabled": true,
  "publishableKeyConfigured": true,
  "checkoutEndpoint": "/api/checkout/founder-pass"
}
```

(`missing` key omitted when checkout env is complete.)

**Expected when vars are unset** (mock `/shop` UI):

```json
{
  "stripeCheckoutEnabled": false,
  "publishableKeyConfigured": false,
  "checkoutEndpoint": "/api/checkout/founder-pass",
  "missing": ["STRIPE_SECRET_KEY", "STRIPE_FOUNDER_PASS_PRICE_ID"]
}
```

Related probes (same pattern — list unset keys, no secrets):

```bash
curl -s "$FQDN/api/checkout/founder-pass" | jq '{configured, missing}'
curl -s "$FQDN/api/webhooks/stripe" | jq '{checkoutConfigured, webhookConfigured, missingCheckout, missingWebhook}'
```

`POST /api/checkout/founder-pass` without complete env returns **503** with `missing: ["STRIPE_…"]`.

## Checklist

1. [ ] Stripe Dashboard in **Test mode** — product + one-time price created
2. [ ] Phase A env vars on `citymajor-web` + `NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY` buildtime + redeploy
3. [ ] `curl …/api/shop` → `stripeCheckoutEnabled: true`, `missing` absent
4. [ ] Dashboard webhook → `…/api/webhooks/stripe`, event `checkout.session.completed`
5. [ ] Phase B `STRIPE_WEBHOOK_SECRET` + redeploy
6. [ ] `GET …/api/webhooks/stripe` → `webhookConfigured: true`
7. [ ] E2E: `/shop` → test card `4242…` → entitlements (see [`DEPLOY_WEB.md` §4](./DEPLOY_WEB.md#4-end-to-end-test-on-preview))

## Related

- [`DEPLOY_WEB.md`](./DEPLOY_WEB.md) — Stripe test-mode setup (SB-3714), local Stripe CLI, troubleshooting table
- [`web/.env.example`](../web/.env.example) — local dev placeholders
- [`design/SB-3693_AUTH_ENTITLEMENTS_GAP.md`](./design/SB-3693_AUTH_ENTITLEMENTS_GAP.md) — checkout → webhook → tier flow
