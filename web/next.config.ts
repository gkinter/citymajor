import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  output: "standalone",
  reactStrictMode: true,
  transpilePackages: ["@citymajor/sim-types"],
  async headers() {
    const coopCoep = [
      {
        key: "Cross-Origin-Opener-Policy",
        value: "same-origin",
      },
      {
        key: "Cross-Origin-Embedder-Policy",
        value: "require-corp",
      },
    ];
    const corp = [
      {
        key: "Cross-Origin-Resource-Policy",
        value: "same-origin",
      },
    ];
    return [
      {
        source: "/:path*",
        headers: coopCoep,
      },
      {
        source: "/assets/:path*",
        headers: [...coopCoep, ...corp],
      },
      {
        source: "/dotnet/:path*",
        headers: [...coopCoep, ...corp],
      },
      {
        source: "/_next/static/:path*",
        headers: corp,
      },
    ];
  },
};

export default nextConfig;
