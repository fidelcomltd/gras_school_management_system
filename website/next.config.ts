import path from "node:path"
import type { NextConfig } from "next"

const securityHeaders = [
  { key: "X-Content-Type-Options", value: "nosniff" },
  { key: "X-Frame-Options", value: "DENY" },
  { key: "Referrer-Policy", value: "strict-origin-when-cross-origin" },
  { key: "Permissions-Policy", value: "camera=(), microphone=(), geolocation=(), browsing-topics=()" },
  // This host only: no includeSubDomains/preload, which would bind every goldenroyalark.com subdomain
  // (mail, staging, the portal) to HTTPS for years. Decide that domain-wide, deliberately, if ever.
  { key: "Strict-Transport-Security", value: "max-age=31536000" },
]

const nextConfig: NextConfig = {
  // The repo root holds other packages (backend/, frontend/); pin tracing to this one.
  outputFileTracingRoot: path.join(import.meta.dirname),
  turbopack: { root: path.join(import.meta.dirname) },
  poweredByHeader: false,
  devIndicators: false,
  reactStrictMode: true,
  images: {
    formats: ["image/avif", "image/webp"],
    // Next 16 only serves listed qualities. 75 is the default; 85 is for full-bleed hero photos.
    qualities: [75, 85],
    deviceSizes: [390, 640, 768, 1024, 1280, 1440, 1920, 2560],
    minimumCacheTTL: 60 * 60 * 24 * 365,
  },
  async headers() {
    return [{ source: "/:path*", headers: securityHeaders }]
  },
}

export default nextConfig
