"use client";

import { useEffect } from "react";
import "./globals.css";
import { HudErrorPanel } from "@/components/HudErrorPanel";

export default function GlobalError({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  useEffect(() => {
    console.error("[CityMajor] global error:", error);
  }, [error]);

  return (
    <html lang="en">
      <body>
        <HudErrorPanel
          code="500"
          title="City grid offline"
          body="A critical fault stopped the simulation shell. Retry loading or return to /play."
          onRetry={reset}
        />
      </body>
    </html>
  );
}
