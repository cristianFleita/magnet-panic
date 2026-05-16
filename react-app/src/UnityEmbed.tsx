import { useEffect, useRef, useState } from "react";

const BUILD_PATH = "/unity-build/Build";
const LOADER_FILE = `${BUILD_PATH}/unity-build.loader.js`;
const DATA_FILE = `${BUILD_PATH}/unity-build.data.unityweb`;
const FRAMEWORK_FILE = `${BUILD_PATH}/unity-build.framework.js.unityweb`;
const WASM_FILE = `${BUILD_PATH}/unity-build.wasm.unityweb`;
const LOGO_FILE = "/brand/magnet-panic-scrapstorm-logo.png";
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
    const swallow = (event: Event) => {
      event.stopImmediatePropagation();
    };

    window.addEventListener("blur", swallow, true);
    window.addEventListener("focusout", swallow, true);
    document.addEventListener("visibilitychange", swallow, true);

    const originalHasFocus = document.hasFocus.bind(document);
    document.hasFocus = () => true;

    return () => {
      window.removeEventListener("blur", swallow, true);
      window.removeEventListener("focusout", swallow, true);
      document.removeEventListener("visibilitychange", swallow, true);
      document.hasFocus = originalHasFocus;
    };
  }, []);

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
        background: "#020813",
        ...style,
      }}
    >
      {/* Loading overlay */}
      {!loaded && !error && (
        <div className="mp-loader" aria-live="polite">
          <div className="mp-loader__arena" />
          <div className="mp-loader__danger mp-loader__danger--left" />
          <div className="mp-loader__danger mp-loader__danger--right" />

          <section className="mp-loader__panel" aria-label="Game loading">
            <span className="mp-loader__corner mp-loader__corner--tl" />
            <span className="mp-loader__corner mp-loader__corner--tr" />
            <span className="mp-loader__corner mp-loader__corner--bl" />
            <span className="mp-loader__corner mp-loader__corner--br" />

            <img
              className="mp-loader__logo"
              src={LOGO_FILE}
              alt="Magnet Panic: Scrapstorm"
              draggable={false}
            />

            <div className="mp-loader__readout">
              <div>
                <span className="mp-loader__eyebrow">MAGNETIC FIELD</span>
                <strong>Charging arena systems</strong>
              </div>
              <span className="mp-loader__percent">{progress}%</span>
            </div>

            <div
              className="mp-loader__track"
              role="progressbar"
              aria-valuemin={0}
              aria-valuemax={100}
              aria-valuenow={progress}
              aria-label="Loading Magnet Panic: Scrapstorm"
            >
              <div className="mp-loader__bar" style={{ width: `${progress}%` }} />
              <div
                className="mp-loader__spark"
                style={{ left: `${Math.max(2, Math.min(progress, 98))}%` }}
              />
            </div>

            <div className="mp-loader__status">
              <span>SCRAPSTORM INIT</span>
              <span>OVERLOAD CHECK</span>
              <span>TOP 5 SYNC</span>
            </div>
          </section>
        </div>
      )}

      {/* Error overlay */}
      {error && (
        <div className="mp-loader mp-loader--error" role="alert">
          <div className="mp-loader__arena" />
          <section className="mp-loader__panel mp-loader__panel--error">
            <span className="mp-loader__corner mp-loader__corner--tl" />
            <span className="mp-loader__corner mp-loader__corner--tr" />
            <span className="mp-loader__corner mp-loader__corner--bl" />
            <span className="mp-loader__corner mp-loader__corner--br" />
            <img
              className="mp-loader__logo mp-loader__logo--error"
              src={LOGO_FILE}
              alt="Magnet Panic: Scrapstorm"
              draggable={false}
            />
            <div className="mp-loader__error-title">Unity link failed</div>
            <div className="mp-loader__error-message">{error}</div>
            <div className="mp-loader__error-code">
              Expected build: unity-build.loader.js / .data / .framework.js / .wasm
            </div>
          </section>
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
