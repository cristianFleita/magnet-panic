import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

const unityBuildVersion =
  process.env.VERCEL_GIT_COMMIT_SHA ??
  process.env.VITE_UNITY_BUILD_VERSION ??
  String(Date.now())

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  define: {
    __UNITY_BUILD_VERSION__: JSON.stringify(unityBuildVersion),
  },
})
