"use client";

import { useEffect } from "react";
import { HudErrorPanel } from "@/components/HudErrorPanel";

export default function Error({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  useEffect(() => {
    console.error("[CityMajor] route error:", error);
  }, [error]);

  return (
    <HudErrorPanel
      code="500"
      title="Simulation fault"
      body="Something went wrong while rendering this sector. Retry the route or return to the mayor&apos;s desk."
      onRetry={reset}
    />
  );
}
