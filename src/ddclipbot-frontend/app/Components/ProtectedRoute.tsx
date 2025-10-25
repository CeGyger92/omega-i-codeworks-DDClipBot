"use client";

import { useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { useAuth } from '../context/AuthContext';
import Image from 'next/image';

export default function ProtectedRoute({ children }: { children: React.ReactNode }) {
  const { isAuthenticated, isLoading } = useAuth();
  const router = useRouter();

  useEffect(() => {
    if (!isLoading && !isAuthenticated) {
      router.push('/login');
    }
  }, [isAuthenticated, isLoading, router]);

  // Show loading state while checking authentication
  if (isLoading) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-[#0C0C0C]">
        <div className="flex flex-col items-center space-y-6">
          <Image
            src="/assets/DDEmblem.png"
            alt="Dough Doctor Emblem"
            width={128}
            height={128}
            priority
            className="animate-pulse rounded-lg"
          />
          <div className="relative w-16 h-16">
            <div className="absolute inset-0 border-4 border-[#00FFFF] border-t-transparent rounded-full animate-spin"></div>
          </div>
          <p className="text-[#00FFFF] text-lg uppercase tracking-wider font-semibold">
            Loading...
          </p>
        </div>
      </div>
    );
  }

  // If not authenticated, don't render children (will redirect)
  if (!isAuthenticated) {
    return null;
  }

  // User is authenticated, render the protected content
  return <>{children}</>;
}
