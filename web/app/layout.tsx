import type { Metadata } from "next";
import "./globals.css";

const siteUrl = "https://citymajor.apps.softblaze.net";

export const metadata: Metadata = {
  metadataBase: new URL(siteUrl),
  title: "CityMajor — Build your city across 200 years",
  description:
    "Deep city simulation in your browser. Zone districts, balance the economy, and guide your metropolis from frontier town to megacity with Herald AI.",
  applicationName: "CityMajor",
  openGraph: {
    title: "CityMajor — Build your city across 200 years",
    description:
      "Deep city simulation in your browser. Zone districts, balance the economy, and guide your metropolis from frontier town to megacity with Herald AI.",
    url: siteUrl,
    siteName: "CityMajor",
    locale: "en_US",
    type: "website",
  },
  twitter: {
    card: "summary",
    title: "CityMajor — Build your city across 200 years",
    description:
      "Deep city simulation in your browser. Zone districts, balance the economy, and guide your metropolis from frontier town to megacity with Herald AI.",
  },
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
