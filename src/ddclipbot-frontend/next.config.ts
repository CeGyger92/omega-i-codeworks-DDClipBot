import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  output: 'standalone', // Enable standalone build for Docker
  async rewrites() {
    const backendUrl = process.env.BACKEND_URL || 'http://localhost:5000';
    return [
      {
        source: '/api/:path*',
        destination: `${backendUrl}/api/:path*`,
      },
    ];
  },
  // Increase body size limit for large video uploads
  experimental: {
    serverActions: {
      bodySizeLimit: '2gb',
    },
  },
};

export default nextConfig;
