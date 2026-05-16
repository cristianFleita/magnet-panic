import { useEffect, useRef, useState } from "react";

const BUILD_PATH = "/unity-build/Build";
const LOADER_FILE = `${BUILD_PATH}/unity-build.loader.js`;
const DATA_FILE = `${BUILD_PATH}/unity-build.data.unityweb`;
const FRAMEWORK_FILE = `${BUILD_PATH}/unity-build.framework.js.unityweb`;
const WASM_FILE = `${BUILD_PATH}/unity-build.wasm.unityweb`;
const LOCAL_BACKEND_URL = "http://localhost:3000";
const LEADERBOARD_PATH = "/leaderboard";

interface MagnetPanicConfig {
  leaderboardUrl?: string;
}

declare global {
  interface Window {
    unityInstance: unknown;
    onUnityReady?: () => void;
    BACKEND_URL?: string;
    MAGNET_PANIC_CONFIG?: MagnetPanicConfig;
    createUnityInstance: (
      canvas: HTMLCanvasElement,
      config: object,
      onProgress?: (progress: number) => void
    ) => Promise<unknown>;
  }
}

function waitForCreateUnityInstance(timeoutMs = 15000): Promise<void> {
  return new Promise((resolve, reject) => {
    const start = Date.now();
    const check = () => {
      if (typeof window.createUnityInstance === "function") {
        resolve();
        return;
      }
      if (Date.now() - start > timeoutMs) {
        reject(new Error("Timed out waiting for window.createUnityInstance"));
        return;
      }
      setTimeout(check, 50);
    };
    check();
  });
}

function trimToValue(value: string | undefined): string | undefined {
  const trimmed = value?.trim();
  return trimmed ? trimmed : undefined;
}

function withLeaderboardPath(url: string): string {
  const trimmed = url.trim();
  if (trimmed.toLowerCase().endsWith(LEADERBOARD_PATH)) return trimmed;
  if (trimmed.toLowerCase().endsWith(`${LEADERBOARD_PATH}/`)) return trimmed.slice(0, -1);
  return `${trimmed.replace(/\/+$/, "")}${LEADERBOARD_PATH}`;
}

function configureUnityRuntime() {
  const backendUrl = trimToValue(import.meta.env.VITE_BACKEND_URL) ?? LOCAL_BACKEND_URL;
  const leaderboardUrl = withLeaderboardPath(
    trimToValue(import.meta.env.VITE_LEADERBOARD_URL) ?? backendUrl
  );

  window.BACKEND_URL = backendUrl;
  window.MAGNET_PANIC_CONFIG = {
    ...window.MAGNET_PANIC_CONFIG,
    leaderboardUrl,
  };
}

function errorMessage(error: unknown, fallback: string): string {
  return error instanceof Error ? error.message : fallback;
}

interface UnityEmbedProps {
  width?: string | number;
  height?: string | number;
  className?: string;
  style?: React.CSSProperties;
}

export default function UnityEmbed({
  width = "100%",
  height = "100%",
  className,
  style,
}: UnityEmbedProps) {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const [progress, setProgress] = useState(0);
  const [loaded, setLoaded] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const handleVisibilityChange = () => {

      if (document.visibilityState === "visible" && window.unityInstance) {
        console.log("[UnityEmbed] Tab focused: Signaling Unity to resume.");
      }
    };

    document.addEventListener("visibilitychange", handleVisibilityChange);

    return () => {
      document.removeEventListener("visibilitychange", handleVisibilityChange);
    };
  }, [loaded]);

  useEffect(() => {
    let cancelled = false;

    async function loadUnity() {
      if (!canvasRef.current) return;

      configureUnityRuntime();

      if (!document.querySelector(`script[src="${LOADER_FILE}"]`)) {
        await new Promise<void>((resolve, reject) => {
          const s = document.createElement("script");
          s.src = LOADER_FILE;
          s.onload = () => resolve();
          s.onerror = () => reject(new Error(`Failed to load: ${LOADER_FILE}`));
          document.body.appendChild(s);
        });
      }

      if (cancelled) return;

      try {
        await waitForCreateUnityInstance();
      } catch (error: unknown) {
        if (!cancelled) setError(errorMessage(error, "Timed out waiting for Unity loader"));
        return;
      }

      if (cancelled) return;

      try {
        const instance = await window.createUnityInstance(
          canvasRef.current,
          {
            dataUrl: DATA_FILE,
            frameworkUrl: FRAMEWORK_FILE,
            codeUrl: WASM_FILE,
            companyName: "DefaultCompany",
            productName: "MagnetPanic",
            productVersion: "0.1",
          },
          (p: number) => {
            if (!cancelled) setProgress(Math.round(p * 100));
          }
        );

        if (!cancelled) {
          window.unityInstance = instance;
          setLoaded(true);
          console.log("[UnityEmbed] Unity loaded ✓");
          if (typeof window.onUnityReady === "function") window.onUnityReady();
        }
      } catch (err: unknown) {
        if (!cancelled) {
          setError(errorMessage(err, "Unity failed to load"));
          console.error("[UnityEmbed] error:", err);
        }
      }
    }

    loadUnity();
    return () => {
      cancelled = true;
    };
  }, []);

  return (
    <div
      className={className}
      style={{
        position: "relative",
        width,
        height,
        background: "#1a2e1a",
        ...style,
      }}
    >
      {/* Loading overlay */}
      {!loaded && !error && (
        <div
          style={{
            position: "absolute",
            inset: 0,
            display: "flex",
            flexDirection: "column",
            alignItems: "center",
            justifyContent: "center",
            color: "#f3e7ca",
            fontFamily: "'Bebas Neue', Impact, sans-serif",
            background: "radial-gradient(ellipse at 70% 30%, #312e22 85%, #171613 100%)",
            gap: 24,
            zIndex: 10,
            letterSpacing: 1,
            boxShadow: "inset 0 0 180px #13130e",
          }}
        >
          <div
            style={{
              fontSize: 54,
              userSelect: "none",
              filter: "drop-shadow(0 2px 10px #13130e88)",
              color: "#e1b758",
            }}
          >
            🗝️
          </div>
          <div
            style={{
              fontSize: 34,
              fontWeight: 900,
              textTransform: "uppercase",
              letterSpacing: 2,
              color: "#e7effa",
              textShadow: "2px 4px 0 #000, 0 1px 8px #000000ad",
            }}
          >
            Magent Panic
          </div>
          <div
            style={{
              fontSize: 17,
              color: "#e1b758",
              fontWeight: 600,
              letterSpacing: 1,
              textShadow: "0 1px 10px #222",
              marginBottom: 6,
            }}
          >
            Grabing scrap
          </div>
          {/* Prison bars as visual loader */}
          <div
            style={{
              display: "flex",
              alignItems: "center",
              gap: 3,
              width: 210,
              height: 28,
              margin: "6px 0",
              background: "#23231e",
              borderRadius: 7,
              position: "relative",
              boxShadow: "0 2px 16px #0e0e0b9b",
              overflow: "hidden",
            }}
          >
            {[...Array(7)].map((_, i) => (
              <div
                key={i}
                style={{
                  width: 15,
                  height: "100%",
                  background:
                    i < Math.ceil(progress / (100 / 7))
                      ? "linear-gradient(180deg, #faa733 0%, #33331d 100%)"
                      : "#595641",
                  borderRadius: 3,
                  boxShadow:
                    i < Math.ceil(progress / (100 / 7))
                      ? "0 1px 4px #ffecbc"
                      : "none",
                  transition: "background 0.3s, box-shadow 0.3s",
                }}
              />
            ))}
            {/* Animated escapee icon */}
            <div
              style={{
                position: "absolute",
                left: `${(progress / 100) * 180 + 5}px`,
                top: 1,
                transition: "left 0.4s cubic-bezier(.36,1.01,.81,.97)",
                fontSize: 18,
                userSelect: "none",
                textShadow: "0 1px 2px #000",
              }}
            >
              🏃‍♂️
            </div>
          </div>
          <div style={{ fontSize: 14, color: "#bbb", textShadow: "0 1px 7px #1d170a85" }}>
            {progress}%
          </div>
        </div>
      )}

      {/* Error overlay */}
      {error && (
        <div
          style={{
            position: "absolute",
            inset: 0,
            display: "flex",
            flexDirection: "column",
            alignItems: "center",
            justifyContent: "center",
            color: "#ff6b6b",
            fontFamily: "sans-serif",
            gap: 12,
            padding: 24,
            textAlign: "center",
            zIndex: 10,
          }}
        >
          <div style={{ fontSize: 40 }}>⚠️</div>
          <div style={{ fontSize: 18, fontWeight: 600 }}>
            Failed to load game
          </div>
          <div style={{ fontSize: 13, color: "#aaa", maxWidth: 440 }}>
            {error}
          </div>
          <div style={{ fontSize: 12, color: "#666" }}>
            Place your Unity build in <code>public/unity/Build/</code>
            <br />
            Expected:{" "}
            <code>
              unity-build.loader.js / .data / .framework.js / .wasm
            </code>
          </div>
        </div>
      )}

      {/* Unity canvas */}
      <canvas
        ref={canvasRef}
        id="unity-canvas"
        style={{
          width: "100%",
          height: "100%",
          display: "block",
          opacity: loaded ? 1 : 0,
          transition: "opacity 0.5s ease",
        }}
      />
    </div>
  );
}
