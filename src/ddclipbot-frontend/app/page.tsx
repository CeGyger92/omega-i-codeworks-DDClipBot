"use client";

import Image from "next/image";
import { useState, useCallback, useEffect } from "react";
import ProtectedRoute from "./Components/ProtectedRoute";

interface DiscordChannel {
  id: string;
  name: string;
}

export default function Home() {
  const [formData, setFormData] = useState({
    title: "",
    description: "",
    publishMessage: "",
    targetChannel: "",
    pingChannel: false,
  });
  const [videoFile, setVideoFile] = useState<File | null>(null);
  const [isDragging, setIsDragging] = useState(false);
  const [showTooltip, setShowTooltip] = useState(false);
  const [channels, setChannels] = useState<DiscordChannel[]>([]);
  const [loadingChannels, setLoadingChannels] = useState(true);
  const [isUploading, setIsUploading] = useState(false);
  const [uploadSuccess, setUploadSuccess] = useState(false);
  const [uploadError, setUploadError] = useState<string | null>(null);

  // Fetch Discord channels on mount
  useEffect(() => {
    const fetchChannels = async () => {
      try {
        const response = await fetch("/api/discord/channels", {
          credentials: "include",
        });

        if (response.ok) {
          const data = await response.json();
          setChannels(data.channels || []);
        } else {
          console.error("Failed to fetch channels:", response.statusText);
        }
      } catch (error) {
        console.error("Error fetching channels:", error);
      } finally {
        setLoadingChannels(false);
      }
    };

    fetchChannels();
  }, []);

  const handleInputChange = (
    e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement>
  ) => {
    const { name, value, type } = e.target;
    setFormData((prev) => ({
      ...prev,
      [name]: type === "checkbox" ? (e.target as HTMLInputElement).checked : value,
    }));
  };

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.files && e.target.files[0]) {
      setVideoFile(e.target.files[0]);
    }
  };

  const handleDragOver = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    setIsDragging(true);
  }, []);

  const handleDragLeave = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    setIsDragging(false);
  }, []);

  const handleDrop = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    setIsDragging(false);
    if (e.dataTransfer.files && e.dataTransfer.files[0]) {
      setVideoFile(e.dataTransfer.files[0]);
    }
  }, []);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    
    if (!videoFile) {
      setUploadError("Please select a video file");
      return;
    }

    setIsUploading(true);
    setUploadError(null);
    setUploadSuccess(false);

    try {
      // Create FormData for multipart upload
      const formDataToSend = new FormData();
      formDataToSend.append("title", formData.title);
      formDataToSend.append("description", formData.description);
      formDataToSend.append("publishMessage", formData.publishMessage);
      formDataToSend.append("targetChannel", formData.targetChannel);
      formDataToSend.append("pingChannel", formData.pingChannel.toString());
      formDataToSend.append("video", videoFile);

      console.log("Starting upload:", {
        fileName: videoFile.name,
        fileSize: videoFile.size,
        fileSizeMB: (videoFile.size / 1024 / 1024).toFixed(2) + " MB"
      });

      // Create an AbortController for timeout handling
      const controller = new AbortController();
      const timeoutId = setTimeout(() => controller.abort(), 30 * 60 * 1000); // 30 minute timeout

      try {
        // Call backend API directly to avoid Next.js proxy limitations with large files
        const apiUrl = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000';
        const response = await fetch(`${apiUrl}/api/videos/upload`, {
          method: "POST",
          credentials: "include",
          body: formDataToSend,
          signal: controller.signal,
        });

        clearTimeout(timeoutId);

        if (!response.ok) {
          const errorData = await response.json().catch(() => ({ error: "Upload failed" }));
          throw new Error(errorData.error || "Upload failed");
        }

        const result = await response.json();
        console.log("Upload successful:", result);

        // Show success message
        setUploadSuccess(true);
      } catch (fetchError: unknown) {
        clearTimeout(timeoutId);
        
        if (fetchError instanceof Error && fetchError.name === 'AbortError') {
          throw new Error("Upload timed out. Please try again with a smaller file or check your connection.");
        }
        throw fetchError;
      }

      // Reset form after 3 seconds
      setTimeout(() => {
        setFormData({
          title: "",
          description: "",
          publishMessage: "",
          targetChannel: "",
          pingChannel: false,
        });
        setVideoFile(null);
        setUploadSuccess(false);
      }, 3000);

    } catch (error) {
      console.error("Upload error:", error);
      setUploadError(error instanceof Error ? error.message : "Upload failed");
    } finally {
      setIsUploading(false);
    }
  };

  return (
    <ProtectedRoute>
      <div className="flex min-h-screen items-center justify-center bg-[#0C0C0C] py-8 px-4">
        <main className="w-full max-w-4xl">
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
        </div>

        {/* Form Container */}
        <form
          onSubmit={handleSubmit}
          className="bg-[#0C0C0C] border-2 border-[#00FFFF] rounded-lg p-8 space-y-6"
        >
          {/* Title Field */}
          <div className="space-y-2">
            <label
              htmlFor="title"
              className="block text-sm uppercase tracking-wide text-[#CCCCCC] font-semibold"
            >
              Title <span className="text-[#FF4500]">*</span>
            </label>
            <input
              type="text"
              id="title"
              name="title"
              required
              value={formData.title}
              onChange={handleInputChange}
              className="w-full bg-[#0C0C0C] border border-[#00FFFF] rounded-md text-[#CCCCCC] px-4 py-3 focus:outline-none focus:border-[#FF4500] focus:shadow-[0_0_10px_rgba(255,69,0,0.5)] transition-all"
              placeholder="Enter clip title"
            />
          </div>

          {/* Description Field */}
          <div className="space-y-2">
            <label
              htmlFor="description"
              className="block text-sm uppercase tracking-wide text-[#CCCCCC] font-semibold"
            >
              Description
            </label>
            <textarea
              id="description"
              name="description"
              value={formData.description}
              onChange={handleInputChange}
              rows={4}
              className="w-full bg-[#0C0C0C] border border-[#00FFFF] rounded-md text-[#CCCCCC] px-4 py-3 focus:outline-none focus:border-[#FF4500] focus:shadow-[0_0_10px_rgba(255,69,0,0.5)] transition-all resize-none"
              placeholder="Describe your clip"
            />
          </div>

          {/* Publish Message Field */}
          <div className="space-y-2">
            <label
              htmlFor="publishMessage"
              className="block text-sm uppercase tracking-wide text-[#CCCCCC] font-semibold"
            >
              Publish Message
            </label>
            <textarea
              id="publishMessage"
              name="publishMessage"
              value={formData.publishMessage}
              onChange={handleInputChange}
              rows={3}
              className="w-full bg-[#0C0C0C] border border-[#00FFFF] rounded-md text-[#CCCCCC] px-4 py-3 focus:outline-none focus:border-[#FF4500] focus:shadow-[0_0_10px_rgba(255,69,0,0.5)] transition-all resize-none"
              placeholder="Optional message to accompany the clip when its posted in Discord"
            />
          </div>

          {/* Target Channel Dropdown */}
          <div className="space-y-2">
            <label
              htmlFor="targetChannel"
              className="block text-sm uppercase tracking-wide text-[#CCCCCC] font-semibold"
            >
              Target Channel <span className="text-[#FF4500]">*</span>
            </label>
            <select
              id="targetChannel"
              name="targetChannel"
              required
              value={formData.targetChannel}
              onChange={handleInputChange}
              disabled={loadingChannels}
              className="w-full bg-[#0C0C0C] border border-[#00FFFF] rounded-md text-[#CCCCCC] px-4 py-3 focus:outline-none focus:border-[#FF4500] focus:shadow-[0_0_10px_rgba(255,69,0,0.5)] transition-all cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed"
            >
              <option value="">
                {loadingChannels ? "Loading channels..." : "Select a channel"}
              </option>
              {channels.map((channel) => (
                <option key={channel.id} value={channel.id}>
                  {channel.name}
                </option>
              ))}
            </select>
          </div>

          {/* Ping Channel Checkbox */}
          <div className="flex items-center space-x-3">
            <input
              type="checkbox"
              id="pingChannel"
              name="pingChannel"
              checked={formData.pingChannel}
              onChange={handleInputChange}
              className="w-5 h-5 bg-[#0C0C0C] border-2 border-[#00FFFF] rounded checked:bg-[#39FF14] focus:ring-2 focus:ring-[#FF4500] cursor-pointer"
            />
            <label
              htmlFor="pingChannel"
              className="text-sm uppercase tracking-wide text-[#CCCCCC] font-semibold cursor-pointer"
            >
              Ping Channel
            </label>
            <div className="relative">
              <button
                type="button"
                onMouseEnter={() => setShowTooltip(true)}
                onMouseLeave={() => setShowTooltip(false)}
                className="text-[#00FFFF] hover:text-[#FF4500] transition-colors"
              >
                <svg
                  xmlns="http://www.w3.org/2000/svg"
                  viewBox="0 0 24 24"
                  fill="currentColor"
                  className="w-5 h-5"
                >
                  <path
                    fillRule="evenodd"
                    d="M2.25 12c0-5.385 4.365-9.75 9.75-9.75s9.75 4.365 9.75 9.75-4.365 9.75-9.75 9.75S2.25 17.385 2.25 12zm8.706-1.442c1.146-.573 2.437.463 2.126 1.706l-.709 2.836.042-.02a.75.75 0 01.67 1.34l-.04.022c-1.147.573-2.438-.463-2.127-1.706l.71-2.836-.042.02a.75.75 0 11-.671-1.34l.041-.022zM12 9a.75.75 0 100-1.5.75.75 0 000 1.5z"
                    clipRule="evenodd"
                  />
                </svg>
              </button>
              {showTooltip && (
                <div className="absolute left-8 top-1/2 -translate-y-1/2 bg-[#0C0C0C] border border-[#00FFFF] rounded-md px-3 py-2 text-sm text-[#CCCCCC] whitespace-nowrap z-10 shadow-[0_0_15px_rgba(0,255,255,0.3)]">
                  Use the @here tag to notify people in the channel when the video posts
                  <div className="absolute left-0 top-1/2 -translate-x-1 -translate-y-1/2 w-2 h-2 bg-[#0C0C0C] border-l border-b border-[#00FFFF] rotate-45"></div>
                </div>
              )}
            </div>
          </div>

          {/* Video File Upload */}
          <div className="space-y-2">
            <label
              htmlFor="video"
              className="block text-sm uppercase tracking-wide text-[#CCCCCC] font-semibold"
            >
              Video File <span className="text-[#FF4500]">*</span>
            </label>
            <div
              onDragOver={handleDragOver}
              onDragLeave={handleDragLeave}
              onDrop={handleDrop}
              className={`relative border-2 border-dashed rounded-lg transition-all ${
                isDragging
                  ? "border-[#FF4500] bg-[#FF4500]/10 shadow-[0_0_20px_rgba(255,69,0,0.3)]"
                  : "border-[#00FFFF]"
              } p-8 text-center`}
            >
              <input
                type="file"
                id="video"
                name="video"
                required
                accept="video/mp4,video/x-m4v,video/*,.mkv,.avi,.mov,.wmv,.flv,.webm"
                onChange={handleFileChange}
                className="absolute inset-0 w-full h-full opacity-0 cursor-pointer"
              />
              {videoFile ? (
                <div className="space-y-2">
                  <div className="text-[#39FF14] text-lg font-semibold">
                    ✓ {videoFile.name}
                  </div>
                  <div className="text-[#CCCCCC] text-sm">
                    {(videoFile.size / 1024 / 1024).toFixed(2)} MB
                  </div>
                  <button
                    type="button"
                    onClick={(e) => {
                      e.stopPropagation();
                      setVideoFile(null);
                    }}
                    className="text-[#FF4500] text-sm hover:underline"
                  >
                    Click to replace
                  </button>
                </div>
              ) : (
                <div className="space-y-2">
                  <div className="text-[#00FFFF] text-6xl mb-4">↑</div>
                  <div className="text-[#CCCCCC]">
                    <span className="text-[#00FFFF] font-semibold">
                      Drag and drop
                    </span>{" "}
                    your video here
                  </div>
                  <div className="text-[#CCCCCC] text-sm">
                    or click to browse
                  </div>
                  <div className="text-[#666666] text-xs mt-2">
                    Supports YouTube compatible formats: MP4, AVI, MOV, MKV, FLV, WMV, WebM
                  </div>
                </div>
              )}
            </div>
          </div>

          {/* Error Message */}
          {uploadError && (
            <div className="bg-[#FF4500]/10 border border-[#FF4500] rounded-lg p-4 text-[#FF4500]">
              ❌ {uploadError}
            </div>
          )}

          {/* Success Message */}
          {uploadSuccess && (
            <div className="bg-[#39FF14]/10 border border-[#39FF14] rounded-lg p-4 text-[#39FF14]">
              ✅ Upload queued successfully! You'll receive a DM when processing starts.
            </div>
          )}

          {/* Submit Button */}
          <div className="pt-4">
            <button
              type="submit"
              disabled={isUploading}
              className={`w-full bg-[#0C0C0C] border-2 border-[#FF4500] rounded-lg text-[#FF4500] py-4 px-8 uppercase tracking-wider font-bold text-lg transition-all ${
                isUploading
                  ? "opacity-50 cursor-not-allowed"
                  : "hover:bg-[#FF4500] hover:text-[#0C0C0C] hover:shadow-[0_0_20px_rgba(255,69,0,0.6)] active:bg-[#00FFFF] active:border-[#00FFFF]"
              }`}
            >
              {isUploading ? (
                <span className="flex items-center justify-center gap-3">
                  <svg
                    className="animate-spin h-5 w-5"
                    xmlns="http://www.w3.org/2000/svg"
                    fill="none"
                    viewBox="0 0 24 24"
                  >
                    <circle
                      className="opacity-25"
                      cx="12"
                      cy="12"
                      r="10"
                      stroke="currentColor"
                      strokeWidth="4"
                    ></circle>
                    <path
                      className="opacity-75"
                      fill="currentColor"
                      d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
                    ></path>
                  </svg>
                  Uploading...
                </span>
              ) : (
                "Upload Clip"
              )}
            </button>
          </div>
        </form>
      </main>
    </div>
    </ProtectedRoute>
  );
}
