"use client";

import Image from "next/image";
import { useState, useCallback } from "react";
import ProtectedRoute from "./Components/ProtectedRoute";

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

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    // Handle form submission
    console.log("Form submitted:", { formData, videoFile });
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
            className="mb-6"
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
              className="w-full bg-[#0C0C0C] border border-[#00FFFF] rounded-md text-[#CCCCCC] px-4 py-3 focus:outline-none focus:border-[#FF4500] focus:shadow-[0_0_10px_rgba(255,69,0,0.5)] transition-all cursor-pointer"
            >
              <option value="">Select a channel</option>
              <option value="general">general</option>
              <option value="flapjack-times">flapjack-times</option>
              <option value="pure-chaos">pure-chaos</option>
              <option value="plunder-speak">plunder-speak</option>
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

          {/* Submit Button */}
          <div className="pt-4">
            <button
              type="submit"
              className="w-full bg-[#0C0C0C] border-2 border-[#FF4500] rounded-lg text-[#FF4500] py-4 px-8 uppercase tracking-wider font-bold text-lg hover:bg-[#FF4500] hover:text-[#0C0C0C] hover:shadow-[0_0_20px_rgba(255,69,0,0.6)] transition-all active:bg-[#00FFFF] active:border-[#00FFFF]"
            >
              Upload Clip
            </button>
          </div>
        </form>
      </main>
    </div>
    </ProtectedRoute>
  );
}
