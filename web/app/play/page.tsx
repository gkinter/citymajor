"use client";

import dynamic from "next/dynamic";

const PlayClient = dynamic(
  () => import("@/components/city/PlayClient").then((m) => m.PlayClient),
  { ssr: false },
);

export default function PlayPage() {
  return <PlayClient />;
}
