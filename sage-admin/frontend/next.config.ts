import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  /* config options here */
  cacheComponents: true,
  partialPrefetching: true,
  experimental: {
    // Every admin page sits behind the client-side sign-in check in AdminShell, so no page can render before the
    // browser knows who is signed in. Validating every page implicitly ('warning', the default) therefore reports each
    // one as a "dropped segment" in dev. Only validate segments that opt in with `export const instant = true`.
    instantInsights: { validationLevel: "manual-warning" },
  },
  turbopack: {
    rules: {
      "*.css": {
        loaders: ["@tailwindcss/turbopack"],
        as: "*.css",
      },
    },
  },
};

export default nextConfig;
