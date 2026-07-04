import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "CityMajor Web",
  description: "CityMajor Web v1 Phase 0 — R3F technical spike",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en">
      <body>{children}</body>
    </html>
  );
}
