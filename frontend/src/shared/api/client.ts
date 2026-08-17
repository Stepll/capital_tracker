import axios from "axios";

export const TOKEN_STORAGE_KEY = "capital_tracker_token";

export const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_URL ?? "http://localhost:5000/api",
});

apiClient.interceptors.request.use((config) => {
  const token = localStorage.getItem(TOKEN_STORAGE_KEY);
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

/**
 * Drop the session and bounce to login. Exported because the SSE stream is a manual
 * fetch() and so never passes through the interceptor below — without sharing this,
 * an expired token would log you out on every screen except the analysis modal.
 */
export function handleUnauthorized() {
  localStorage.removeItem(TOKEN_STORAGE_KEY);
  if (location.pathname !== "/login") {
    location.href = "/login";
  }
}

apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      handleUnauthorized();
    }
    return Promise.reject(error);
  },
);
