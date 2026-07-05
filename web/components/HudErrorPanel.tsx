"use client";

import Link from "next/link";

type HudErrorPanelProps = {
  code: string;
  title: string;
  body: string;
  onRetry?: () => void;
};

export function HudErrorPanel({ code, title, body, onRetry }: HudErrorPanelProps) {
  return (
    <div className="hud-not-found">
      <div className="hud-not-found__glow hud-not-found__glow--error" aria-hidden />
      <main className="hud-not-found__panel">
        <p className="hud-not-found__eyebrow">CityMajor Web</p>
        <h1 className="hud-not-found__code hud-not-found__code--error">{code}</h1>
        <p className="hud-not-found__title">{title}</p>
        <p className="hud-not-found__body">{body}</p>
        <div className="hud-not-found__actions">
          {onRetry ? (
            <button type="button" onClick={onRetry} className="hud-not-found__cta">
              Try again
            </button>
          ) : null}
          <Link href="/play" className="hud-not-found__cta hud-not-found__cta--secondary">
            Back to /play
          </Link>
        </div>
      </main>
    </div>
  );
}
