import { getToken, notifyUnauthorized } from "./auth";

// Base URL of the Durnius.Web API. Empty in production (the SPA is served
// same-origin); override with VITE_API_URL for local development.
export const API_URL: string =
  import.meta.env.VITE_API_URL ?? "http://localhost:8080";

/** Successful response from POST /api/login and POST /api/register. */
export interface AuthResponse {
  token: string;
  tokenType: string;
}

export interface LeaderboardEntry {
  rank: number;
  userId: number;
  username: string;
  wins: number;
}

export interface LeaderboardResponse {
  periodStartUtc: string;
  resetAtUtc: string;
  entries: LeaderboardEntry[];
}

interface ApiErrorBody {
  errors?: string[];
  message?: string;
}

export class ApiError extends Error {
  readonly status: number;
  readonly body: unknown;

  constructor(message: string, status: number, body: unknown) {
    super(message);
    this.name = "ApiError";
    this.status = status;
    this.body = body;
  }
}

// A 401 from these endpoints means "bad credentials", not "session expired",
// so it must not trigger the global logout-and-redirect handling.
const CREDENTIAL_ENDPOINTS = new Set(["/api/login", "/api/register"]);

function parseJson(text: string): unknown {
  try {
    return JSON.parse(text);
  } catch {
    return null;
  }
}

async function request<T>(
  path: string,
  method: string,
  body?: unknown
): Promise<T> {
  const headers: Record<string, string> = {};
  if (body !== undefined) {
    headers["Content-Type"] = "application/json";
  }
  const token = getToken();
  if (token) {
    headers.Authorization = `Bearer ${token}`;
  }

  const response = await fetch(`${API_URL}${path}`, {
    method,
    headers,
    body: body === undefined ? undefined : JSON.stringify(body),
  });

  // Any "not logged in" answer clears the JWT and redirects to /login.
  if (
    (response.status === 401 || response.status === 403) &&
    !CREDENTIAL_ENDPOINTS.has(path)
  ) {
    notifyUnauthorized();
  }

  const text = await response.text();
  const data: unknown = text ? parseJson(text) : null;
  if (!response.ok) {
    const errorBody = data as ApiErrorBody | null;
    const message =
      errorBody?.errors?.join(" ") ??
      errorBody?.message ??
      `Request failed (${response.status})`;
    throw new ApiError(message, response.status, data);
  }
  return data as T;
}

/** Extracts a human-readable message from an unknown thrown value. */
export function errorMessage(error: unknown): string {
  return error instanceof Error ? error.message : "Something went wrong.";
}

/** Extracts the validation errors the API returned, if any. */
export function errorMessages(error: unknown): string[] {
  if (error instanceof ApiError) {
    const body = error.body as ApiErrorBody | null;
    if (Array.isArray(body?.errors) && body.errors.length > 0) {
      return body.errors;
    }
  }
  return [errorMessage(error)];
}

export function login(
  username: string,
  password: string
): Promise<AuthResponse> {
  return request<AuthResponse>("/api/login", "POST", { username, password });
}

export function register(
  username: string,
  password: string
): Promise<AuthResponse> {
  return request<AuthResponse>("/api/register", "POST", { username, password });
}

export function changePassword(
  currentPassword: string,
  newPassword: string
): Promise<void> {
  return request<void>("/api/account/password", "PUT", {
    currentPassword,
    newPassword,
  });
}

export function getLeaderboard(): Promise<LeaderboardResponse> {
  return request<LeaderboardResponse>("/api/leaderboard", "GET");
}

export interface CurrentAccount {
  userId: number;
  username: string;
  gold: number;
}

export function getCurrentAccount(): Promise<CurrentAccount> {
  return request<CurrentAccount>("/api/account/me", "GET");
}
