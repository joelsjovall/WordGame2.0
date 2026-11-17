// Empty uses the frontend origin until a backend URL is configured.
export const apiBaseUrl = (import.meta.env.VITE_API_BASE_URL ?? '')
  .trim()
  .replace(/\/+$/, '')
