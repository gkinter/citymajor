import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "CityMajor — Build your city across 200 years",
  description:
    "Deep city simulation in your browser. Zone districts, balance the economy, and guide your metropolis from frontier town to megacity with Herald AI.",
  applicationName: "CityMajor",
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
