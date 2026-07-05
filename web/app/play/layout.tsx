import type { Metadata } from "next";

export const metadata: Metadata = {
  title: "Play — CityMajor",
  description:
    "Launch the game in your browser. Zone a living 256×256 city, manage traffic and services, and watch your metropolis evolve from frontier town to modern megacity.",
  openGraph: {
    title: "Play — CityMajor",
    description:
      "Launch the game in your browser. Zone a living 256×256 city, manage traffic and services, and watch your metropolis evolve from frontier town to modern megacity.",
    url: "/play",
    siteName: "CityMajor",
    type: "website",
    images: [
      {
        url: "/assets/og/citymajor-og.png",
        width: 1200,
        height: 630,
        alt: "CityMajor — Build your city across 200 years",
      },
    ],
  },
  twitter: {
    card: "summary",
    title: "Play — CityMajor",
    description:
      "Launch the game in your browser. Zone a living 256×256 city, manage traffic and services, and watch your metropolis evolve from frontier town to modern megacity.",
  },
};

export default function PlayLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return children;
}
