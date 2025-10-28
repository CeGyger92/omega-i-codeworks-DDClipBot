"use client";

import Image from "next/image";
import { useEffect, useState, Suspense } from "react";
import { useSearchParams, useRouter } from "next/navigation";
import { useAuth } from "../context/AuthContext";

function LoginContent() {
    const [isLoading, setIsLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const searchParams = useSearchParams();
    const router = useRouter();
    const { checkAuth } = useAuth();

    useEffect(() => {
        const code = searchParams.get('code');
        if (code) {
            exchangeCodeForToken(code);
        }
    }, [searchParams]);

    const exchangeCodeForToken = async (code: string) => {
        setIsLoading(true);
        setError(null);

        try {
            // Call backend API - this will be proxied to ASP.NET backend
            const response = await fetch('/api/auth/discord/callback', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify({ code }),
                credentials: 'include', // Important: allows cookies to be set
            });

            if (!response.ok) {
                throw new Error('Failed to authenticate with Discord');
            }

            const data = await response.json();
            
            if (data.success) {
                // Backend has set HttpOnly session cookie
                // Refresh auth state
                await checkAuth();
                // Redirect to home page
                router.push('/');
            } else {
                throw new Error('Authentication failed');
            }
        } catch (err) {
            setError(err instanceof Error ? err.message : 'An error occurred during authentication');
            setIsLoading(false);
        }
    };

    const handleDiscordLogin = () => {
        window.location.href = "https://discord.com/oauth2/authorize?client_id=1430631789776601199&response_type=code&redirect_uri=http%3A%2F%2Flocalhost%3A3000%2Flogin&scope=guilds.members.read+identify+email+openid";
    };

    // Show loading state if processing OAuth callback
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
                        Authenticating...
                    </p>
                </div>
            </div>
        );
    }

    return (
        <div className="flex min-h-screen items-center justify-center bg-[#0C0C0C] py-8 px-4">
            <main className="w-full max-w-md">
                {/* Header with Emblem */}
                <div className="flex flex-col items-center mb-8">
                    <Image
                        src="/assets/DDEmblem.png"
                        alt="Dough Doctor Emblem"
                        width={256}
                        height={256}
                        priority
                        className="mb-6 rounded-xl"
                    />
                    <h1 className="text-4xl font-bold uppercase tracking-wider text-[#00FFFF] text-center font-[family-name:var(--font-orbitron)]">
                        Clip Share
                    </h1>
                    <p className="text-[#CCCCCC] text-sm uppercase tracking-wide mt-2">
                        Authentication Required
                    </p>
                </div>

                {/* Login Container */}
                <div className="bg-[#0C0C0C] border-2 border-[#00FFFF] rounded-lg p-8 space-y-6">
                    {error && (
                        <div className="bg-[#FF4500]/10 border border-[#FF4500] rounded-md p-4 mb-4">
                            <p className="text-[#FF4500] text-sm text-center">
                                {error}
                            </p>
                        </div>
                    )}
                    
                    <div className="text-center space-y-2 mb-8">
                        <h2 className="text-2xl font-bold uppercase tracking-wide text-[#39FF14]">
                            Welcome Back
                        </h2>
                        <p className="text-[#CCCCCC] text-sm">
                            Sign in with your Discord account to continue
                        </p>
                    </div>

                    {/* Discord Login Button */}
                    <button
                        onClick={handleDiscordLogin}
                        className="w-full bg-[#0C0C0C] border-2 border-[#FF4500] rounded-lg text-[#FF4500] py-4 px-8 uppercase tracking-wider font-bold text-lg hover:bg-[#FF4500] hover:text-[#0C0C0C] hover:shadow-[0_0_20px_rgba(255,69,0,0.6)] transition-all active:bg-[#00FFFF] active:border-[#00FFFF] flex items-center justify-center space-x-3"
                    >
                        <svg
                            xmlns="http://www.w3.org/2000/svg"
                            viewBox="0 0 24 24"
                            fill="currentColor"
                            className="w-6 h-6"
                        >
                            <path d="M20.317 4.37a19.791 19.791 0 0 0-4.885-1.515a.074.074 0 0 0-.079.037c-.21.375-.444.864-.608 1.25a18.27 18.27 0 0 0-5.487 0a12.64 12.64 0 0 0-.617-1.25a.077.077 0 0 0-.079-.037A19.736 19.736 0 0 0 3.677 4.37a.07.07 0 0 0-.032.027C.533 9.046-.32 13.58.099 18.057a.082.082 0 0 0 .031.057a19.9 19.9 0 0 0 5.993 3.03a.078.078 0 0 0 .084-.028a14.09 14.09 0 0 0 1.226-1.994a.076.076 0 0 0-.041-.106a13.107 13.107 0 0 1-1.872-.892a.077.077 0 0 1-.008-.128a10.2 10.2 0 0 0 .372-.292a.074.074 0 0 1 .077-.01c3.928 1.793 8.18 1.793 12.062 0a.074.074 0 0 1 .078.01c.12.098.246.198.373.292a.077.077 0 0 1-.006.127a12.299 12.299 0 0 1-1.873.892a.077.077 0 0 0-.041.107c.36.698.772 1.362 1.225 1.993a.076.076 0 0 0 .084.028a19.839 19.839 0 0 0 6.002-3.03a.077.077 0 0 0 .032-.054c.5-5.177-.838-9.674-3.549-13.66a.061.061 0 0 0-.031-.03zM8.02 15.33c-1.183 0-2.157-1.085-2.157-2.419c0-1.333.956-2.419 2.157-2.419c1.21 0 2.176 1.096 2.157 2.42c0 1.333-.956 2.418-2.157 2.418zm7.975 0c-1.183 0-2.157-1.085-2.157-2.419c0-1.333.955-2.419 2.157-2.419c1.21 0 2.176 1.096 2.157 2.42c0 1.333-.946 2.418-2.157 2.418z" />
                        </svg>
                        <span>Log in with Discord</span>
                    </button>

                    {/* Info Text */}
                    <div className="text-center text-[#666666] text-xs mt-6">
                        By logging in, you agree to share your Discord username and profile information
                    </div>
                </div>
            </main>
        </div>
    );
}

export default function Login() {
    return (
        <Suspense fallback={
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
        }>
            <LoginContent />
        </Suspense>
    );
}

export function DiscordCallback() {
  const router = useRouter();

  useEffect(() => {
    const handleCallback = async () => {
      const urlParams = new URLSearchParams(window.location.search);
      const code = urlParams.get('code');

      if (!code) {
        router.push('/login?error=no_code');
        return;
      }

      try {
        // Send code to YOUR backend - never handle tokens client-side
        const response = await fetch('/api/auth/discord/callback', {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
          },
          body: JSON.stringify({ code }),
        });

        if (!response.ok) {
          throw new Error('Authentication failed');
        }

        // Backend sets HttpOnly cookies - client never touches tokens
        router.push('/');
      } catch (error) {
        console.error('Auth error:', error);
        router.push('/login?error=auth_failed');
      }
    };

    handleCallback();
  }, [router]);

  return <div>Authenticating...</div>;
}