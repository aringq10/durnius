// Base URL of the Durnius.Web API. Override with VITE_API_URL (see .env.example).
export const API_URL = import.meta.env.VITE_API_URL ?? "http://localhost:8080";

/** Error body shape returned by the API (e.g. validation or auth failures). */
export interface ApiErrorBody {
  errors?: string[];
  message?: string;
}

/** Successful response from POST /login and POST /register. */
export interface AuthResponse {
  token: string;
  tokenType: string;
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

async function toJson(response: Response): Promise<unknown> {
  const text = await response.text();
  if (!text) return null;
  try {
    return JSON.parse(text);
  } catch {
    return null;
  }
}

async function handle<T>(response: Response): Promise<T> {
  const body = await toJson(response);
  if (!response.ok) {
    const errorBody = body as ApiErrorBody | null;
    const message =
      errorBody?.errors?.join(" ") ??
      errorBody?.message ??
      `Request failed (${response.status})`;
    throw new ApiError(message, response.status, body);
  }
  return body as T;
}

function authHeaders(token?: string | null): Record<string, string> {
  const headers: Record<string, string> = {
    "Content-Type": "application/json",
  };
  if (token) {
    headers.Authorization = `Bearer ${token}`;
  }
  return headers;
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

export function postJson<T>(
  path: string,
  data: unknown,
  token?: string | null
): Promise<T> {
  return fetch(`${API_URL}${path}`, {
    method: "POST",
    headers: authHeaders(token),
    body: JSON.stringify(data),
  }).then((response) => handle<T>(response));
}

export function getJson<T>(path: string, token?: string | null): Promise<T> {
  const headers: Record<string, string> = {};
  if (token) {
    headers.Authorization = `Bearer ${token}`;
  }
  return fetch(`${API_URL}${path}`, { headers }).then((response) =>
    handle<T>(response)
  );
}
