// Base URL of the Durnius.Web API. Override with VITE_API_URL (see .env.example).
export const API_URL = import.meta.env.VITE_API_URL ?? "http://localhost:8080";

export class ApiError extends Error {
  constructor(message, status, body) {
    super(message);
    this.name = "ApiError";
    this.status = status;
    this.body = body;
  }
}

async function toJson(response) {
  const text = await response.text();
  if (!text) return null;
  try {
    return JSON.parse(text);
  } catch {
    return null;
  }
}

async function handle(response) {
  const body = await toJson(response);
  if (!response.ok) {
    const message =
      body?.errors?.join(" ") ??
      body?.message ??
      `Request failed (${response.status})`;
    throw new ApiError(message, response.status, body);
  }
  return body;
}

function authHeaders(token) {
  const headers = { "Content-Type": "application/json" };
  if (token) {
    headers.Authorization = `Bearer ${token}`;
  }
  return headers;
}

export function postJson(path, data, token) {
  return fetch(`${API_URL}${path}`, {
    method: "POST",
    headers: authHeaders(token),
    body: JSON.stringify(data),
  }).then(handle);
}

export function getJson(path, token) {
  const headers = {};
  if (token) {
    headers.Authorization = `Bearer ${token}`;
  }
  return fetch(`${API_URL}${path}`, { headers }).then(handle);
}