"use client";

import { useCallback, useEffect, useState, type CSSProperties } from "react";
import type { Entitlements } from "@/lib/entitlements";

const FOUNDER_PASS_PRICE = "$24.99";

const FOUNDER_BENEFITS = [
  "Unlimited LLM narrative events",
  "20 cloud save slots",
  "Founder monument skin",
  "3 cosmetic building packs",
  "Early era-2 access",
  "Credits name listing",
] as const;

type ShopClientProps = {
  stripeCheckoutEnabled?: boolean;
};

export function ShopClient({ stripeCheckoutEnabled = false }: ShopClientProps) {
  const [entitlements, setEntitlements] = useState<Entitlements | null>(null);
  const [loading, setLoading] = useState(true);
  const [checkingOut, setCheckingOut] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState(false);

  const loadEntitlements = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await fetch("/api/me/entitlements", { credentials: "include" });
      if (!res.ok) throw new Error(`Failed to load entitlements (${res.status})`);
      const data = (await res.json()) as Entitlements;
      setEntitlements(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load entitlements");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadEntitlements();
  }, [loadEntitlements]);

  useEffect(() => {
    const params = new URLSearchParams(window.location.search);
    if (params.get("checkout") === "success") {
      setSuccess(true);
      void loadEntitlements();
    }
  }, [loadEntitlements]);

  async function handleCheckout() {
    setCheckingOut(true);
    setError(null);
    setSuccess(false);
    try {
      if (stripeCheckoutEnabled) {
        const res = await fetch("/api/checkout/founder-pass", {
          method: "POST",
          credentials: "include",
        });
        const body = (await res.json().catch(() => null)) as { url?: string; error?: string } | null;
        if (!res.ok) {
          throw new Error(body?.error ?? `Checkout failed (${res.status})`);
        }
        if (!body?.url) {
          throw new Error("Stripe checkout URL missing");
        }
        window.location.href = body.url;
        return;
      }

      const res = await fetch("/api/me/entitlements", {
        method: "POST",
        credentials: "include",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ tier: "founder_pass" }),
      });
      if (!res.ok) {
        const mockBody = (await res.json().catch(() => null)) as { error?: string } | null;
        throw new Error(mockBody?.error ?? `Checkout failed (${res.status})`);
      }
      const data = (await res.json()) as Entitlements;
      setEntitlements(data);
      setSuccess(true);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Checkout failed");
    } finally {
      setCheckingOut(false);
    }
  }

  const hasFounderPass = entitlements?.tier === "founder_pass";
  const checkoutLabel = stripeCheckoutEnabled
    ? "Buy Founder Pass"
    : "Buy Founder Pass (mock checkout)";

  return (
    <div style={styles.page}>
      <header style={styles.header}>
        <h1 style={styles.title}>CityMajor Shop</h1>
        <p style={styles.subtitle}>Cosmetics and Founder Pass — sim depth is never paywalled.</p>
      </header>

      <main style={styles.main}>
        <article style={styles.card}>
          <div style={styles.badge}>Founder Pass</div>
          <p style={styles.price}>{FOUNDER_PASS_PRICE}</p>
          <p style={styles.priceNote}>One-time purchase</p>

          <ul style={styles.benefits}>
            {FOUNDER_BENEFITS.map((benefit) => (
              <li key={benefit}>{benefit}</li>
            ))}
          </ul>

          {loading ? (
            <p style={styles.status}>Checking your account…</p>
          ) : hasFounderPass ? (
            <p style={styles.owned}>You already own Founder Pass.</p>
          ) : (
            <button
              type="button"
              style={styles.button}
              disabled={checkingOut}
              onClick={() => void handleCheckout()}
            >
              {checkingOut ? "Processing…" : checkoutLabel}
            </button>
          )}

          {success ? (
            <p style={styles.success}>Founder Pass activated — enjoy unlimited narrative events.</p>
          ) : null}
          {error ? <p style={styles.error}>{error}</p> : null}
        </article>

        <aside style={styles.aside}>
          <h2 style={styles.asideTitle}>Your tier</h2>
          {loading ? (
            <p>Loading…</p>
          ) : entitlements ? (
            <dl style={styles.dl}>
              <div style={styles.dlRow}>
                <dt>Tier</dt>
                <dd>{entitlements.tier}</dd>
              </div>
              <div style={styles.dlRow}>
                <dt>Save slots</dt>
                <dd>{entitlements.maxSaveSlots}</dd>
              </div>
              <div style={styles.dlRow}>
                <dt>LLM events / day</dt>
                <dd>
                  {entitlements.maxNarrativeEventsPerDay === Number.MAX_SAFE_INTEGER
                    ? "Unlimited"
                    : entitlements.maxNarrativeEventsPerDay}
                </dd>
              </div>
            </dl>
          ) : (
            <p>Unable to load entitlements.</p>
          )}
          <a href="/play" style={styles.link}>
            Back to play
          </a>
        </aside>
      </main>
    </div>
  );
}

const styles: Record<string, CSSProperties> = {
  page: {
    minHeight: "100%",
    overflow: "auto",
    padding: "2rem 1.5rem 3rem",
    background: "linear-gradient(180deg, #0b1020 0%, #121a2e 100%)",
  },
  header: {
    maxWidth: 960,
    margin: "0 auto 2rem",
    textAlign: "center",
  },
  title: {
    margin: 0,
    fontSize: "2rem",
    fontWeight: 700,
    letterSpacing: "-0.02em",
  },
  subtitle: {
    margin: "0.5rem 0 0",
    color: "#9aa8c4",
    fontSize: "1rem",
  },
  main: {
    maxWidth: 960,
    margin: "0 auto",
    display: "grid",
    gridTemplateColumns: "1fr minmax(220px, 280px)",
    gap: "2rem",
    alignItems: "start",
  },
  card: {
    background: "rgba(255,255,255,0.04)",
    border: "1px solid rgba(255,255,255,0.1)",
    borderRadius: 16,
    padding: "2rem 2rem 2.25rem",
    display: "flex",
    flexDirection: "column",
    gap: "0.25rem",
  },
  badge: {
    display: "inline-block",
    alignSelf: "flex-start",
    padding: "0.35rem 0.85rem",
    borderRadius: 999,
    background: "rgba(99, 179, 237, 0.15)",
    color: "#7ec8ff",
    fontSize: "0.8rem",
    fontWeight: 600,
    textTransform: "uppercase",
    letterSpacing: "0.06em",
    marginBottom: "0.75rem",
  },
  price: {
    margin: 0,
    fontSize: "2.5rem",
    fontWeight: 700,
    lineHeight: 1.1,
  },
  priceNote: {
    margin: "0.5rem 0 1.5rem",
    color: "#9aa8c4",
    fontSize: "0.9rem",
  },
  benefits: {
    margin: "0 0 1.75rem",
    paddingLeft: "1.25rem",
    color: "#c8d4ea",
    lineHeight: 1.6,
    display: "flex",
    flexDirection: "column",
    gap: "0.625rem",
  },
  button: {
    width: "100%",
    marginTop: "0.25rem",
    padding: "0.85rem 1.25rem",
    border: "none",
    borderRadius: 10,
    background: "linear-gradient(135deg, #3b82f6 0%, #2563eb 100%)",
    color: "#fff",
    fontSize: "1rem",
    fontWeight: 600,
    cursor: "pointer",
  },
  owned: {
    margin: "0.25rem 0 0",
    padding: "0.85rem 1rem",
    borderRadius: 10,
    background: "rgba(34, 197, 94, 0.12)",
    color: "#86efac",
    textAlign: "center",
  },
  status: {
    margin: 0,
    color: "#9aa8c4",
  },
  success: {
    margin: "1.25rem 0 0",
    color: "#86efac",
    fontSize: "0.9rem",
  },
  error: {
    margin: "1.25rem 0 0",
    color: "#fca5a5",
    fontSize: "0.9rem",
  },
  aside: {
    background: "rgba(255,255,255,0.03)",
    border: "1px solid rgba(255,255,255,0.08)",
    borderRadius: 12,
    padding: "1.25rem",
  },
  asideTitle: {
    margin: "0 0 1rem",
    fontSize: "1rem",
    fontWeight: 600,
  },
  dl: {
    margin: 0,
  },
  dlRow: {
    display: "flex",
    justifyContent: "space-between",
    gap: "1rem",
    marginBottom: "0.5rem",
    fontSize: "0.9rem",
  },
  link: {
    display: "inline-block",
    marginTop: "1.25rem",
    color: "#7ec8ff",
    textDecoration: "none",
    fontSize: "0.9rem",
  },
};
