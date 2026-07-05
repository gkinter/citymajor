import Link from "next/link";
import type { CSSProperties } from "react";

import type { Metadata } from "next";

export const metadata: Metadata = {
  title: "CityMajor — Build your city across 200 years",
  description:
    "Deep city simulation in your browser. Zone districts, balance the economy, and guide your metropolis from frontier town to megacity with Herald AI.",
};

const FEATURES = [
  {
    title: "Deep simulation",
    description:
      "Economy, zoning, traffic, and era progression — a full city sim running in WebAssembly, not a toy sketch.",
    accent: "#7ec8ff",
  },
  {
    title: "Herald AI",
    description:
      "Your city's chronicler. Herald narrates pivotal moments, reacts to crises, and shapes the story of your metropolis.",
    accent: "#c4a0ff",
  },
  {
    title: "256×256 browser city",
    description:
      "A living 3D city rendered in WebGL — pan, zone, and watch thousands of buildings rise without installing anything.",
    accent: "#86efac",
  },
] as const;

export default function HomePage() {
  return (
    <div style={styles.page}>
      <div style={styles.glow} aria-hidden />

      <header style={styles.header}>
        <p style={styles.eyebrow}>CityMajor Web v1</p>
        <h1 style={styles.hero}>Build your city across 200 years</h1>
        <p style={styles.lead}>
          From frontier outpost to modern megacity — zone districts, balance resources, and let Herald
          chronicle every turning point. All in your browser.
        </p>

        <div style={styles.ctaRow}>
          <Link href="/play" style={styles.ctaPrimary}>
            Play Free
          </Link>
          <Link href="/shop" style={styles.ctaSecondary}>
            Shop
          </Link>
        </div>
      </header>

      <section style={styles.features} aria-labelledby="features-heading">
        <h2 id="features-heading" style={styles.featuresHeading}>
          What you get
        </h2>
        <div style={styles.featureGrid}>
          {FEATURES.map((feature) => (
            <article key={feature.title} style={styles.featureCard}>
              <div style={{ ...styles.featureAccent, color: feature.accent }}>
                {feature.title}
              </div>
              <p style={styles.featureBody}>{feature.description}</p>
            </article>
          ))}
        </div>
      </section>

      <footer style={styles.footer}>
        <p style={styles.footerNote}>Sim depth is never paywalled. Founder Pass unlocks cosmetics &amp; narrative quota.</p>
        <div style={styles.footerLinks}>
          <Link href="/play" style={styles.footerLink}>
            Launch game →
          </Link>
          <Link href="/shop" style={styles.footerLink}>
            Founder Pass →
          </Link>
        </div>
      </footer>
    </div>
  );
}

const styles: Record<string, CSSProperties> = {
  page: {
    position: "relative",
    minHeight: "100%",
    overflow: "auto",
    padding: "3rem 1.5rem 4rem",
    background: "linear-gradient(180deg, #0b1020 0%, #121a2e 55%, #0b1020 100%)",
    color: "#e8eef8",
  },
  glow: {
    position: "absolute",
    top: "-20%",
    left: "50%",
    transform: "translateX(-50%)",
    width: "min(900px, 120vw)",
    height: "500px",
    background:
      "radial-gradient(ellipse at center, rgba(59, 130, 246, 0.18) 0%, rgba(11, 16, 32, 0) 70%)",
    pointerEvents: "none",
  },
  header: {
    position: "relative",
    maxWidth: 720,
    margin: "0 auto 4rem",
    textAlign: "center",
  },
  eyebrow: {
    margin: "0 0 1rem",
    fontFamily: "ui-monospace, SFMono-Regular, Menlo, monospace",
    fontSize: "0.75rem",
    fontWeight: 600,
    letterSpacing: "0.12em",
    textTransform: "uppercase",
    color: "#9aa8c4",
  },
  hero: {
    margin: 0,
    fontSize: "clamp(2.25rem, 5vw, 3.25rem)",
    fontWeight: 700,
    lineHeight: 1.1,
    letterSpacing: "-0.03em",
  },
  lead: {
    margin: "1.25rem auto 0",
    maxWidth: 540,
    fontSize: "1.1rem",
    lineHeight: 1.65,
    color: "#9aa8c4",
  },
  ctaRow: {
    display: "flex",
    flexWrap: "wrap",
    justifyContent: "center",
    gap: "0.75rem",
    marginTop: "2rem",
  },
  ctaPrimary: {
    display: "inline-block",
    padding: "0.9rem 1.75rem",
    borderRadius: 10,
    background: "linear-gradient(135deg, #3b82f6 0%, #2563eb 100%)",
    color: "#fff",
    fontSize: "1rem",
    fontWeight: 600,
    textDecoration: "none",
    border: "1px solid rgba(99, 179, 237, 0.35)",
    boxShadow: "0 4px 24px rgba(37, 99, 235, 0.35)",
  },
  ctaSecondary: {
    display: "inline-block",
    padding: "0.9rem 1.75rem",
    borderRadius: 10,
    background: "rgba(8, 12, 24, 0.82)",
    color: "#e8eef8",
    fontSize: "1rem",
    fontWeight: 600,
    textDecoration: "none",
    border: "1px solid rgba(120, 160, 220, 0.25)",
  },
  features: {
    position: "relative",
    maxWidth: 960,
    margin: "0 auto",
  },
  featuresHeading: {
    margin: "0 0 1.5rem",
    fontSize: "0.8rem",
    fontWeight: 600,
    letterSpacing: "0.1em",
    textTransform: "uppercase",
    color: "#9aa8c4",
    textAlign: "center",
  },
  featureGrid: {
    display: "grid",
    gridTemplateColumns: "repeat(auto-fit, minmax(260px, 1fr))",
    gap: "1rem",
  },
  featureCard: {
    background: "rgba(255, 255, 255, 0.04)",
    border: "1px solid rgba(120, 160, 220, 0.2)",
    borderRadius: 14,
    padding: "1.5rem",
  },
  featureAccent: {
    margin: "0 0 0.75rem",
    fontFamily: "ui-monospace, SFMono-Regular, Menlo, monospace",
    fontSize: "0.85rem",
    fontWeight: 600,
    letterSpacing: "0.04em",
    textTransform: "uppercase",
  },
  featureBody: {
    margin: 0,
    fontSize: "0.95rem",
    lineHeight: 1.65,
    color: "#c8d4ea",
  },
  footer: {
    maxWidth: 960,
    margin: "3.5rem auto 0",
    paddingTop: "2rem",
    borderTop: "1px solid rgba(120, 160, 220, 0.15)",
    textAlign: "center",
  },
  footerNote: {
    margin: "0 0 1rem",
    fontSize: "0.9rem",
    color: "#9aa8c4",
  },
  footerLinks: {
    display: "flex",
    flexWrap: "wrap",
    justifyContent: "center",
    gap: "1.5rem",
  },
  footerLink: {
    color: "#7ec8ff",
    fontSize: "0.9rem",
    fontWeight: 500,
    textDecoration: "none",
  },
};
