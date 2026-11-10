import type {
  ClassSummary,
  CreatedClass,
  Hold,
  MyHold,
} from './types';

const BASE_URL = (import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:8080').replace(/\/$/, '');

/** Error carrying a user-friendly message parsed from the backend response. */
export class ApiRequestError extends Error {
  status: number;
  code: string;

  constructor(status: number, code: string, message: string) {
    super(message);
    this.name = 'ApiRequestError';
    this.status = status;
    this.code = code;
  }
}

async function request<T>(
  path: string,
  options: RequestInit = {},
): Promise<T> {
  let response: Response;
  try {
    response = await fetch(`${BASE_URL}${path}`, {
      ...options,
      headers: {
        Accept: 'application/json',
        ...(options.headers ?? {}),
      },
    });
  } catch {
    // Network failure / server down.
    throw new ApiRequestError(
      0,
      'NETWORK',
      'Cannot reach the server. Please check your connection and try again.',
    );
  }

  if (response.status === 204) {
    return undefined as T;
  }

  let body: unknown = null;
  const text = await response.text();
  if (text) {
    try {
      body = JSON.parse(text);
    } catch {
      body = null;
    }
  }

  if (!response.ok) {
    const err = body as { error?: string; message?: string } | null;
    throw new ApiRequestError(
      response.status,
      err?.error ?? 'ERROR',
      err?.message ?? friendlyFallback(response.status),
    );
  }

  return body as T;
}

function friendlyFallback(status: number): string {
  switch (status) {
    case 400:
      return 'The request was invalid. Please check your input.';
    case 403:
      return 'You are not allowed to perform this action.';
    case 404:
      return 'The requested item was not found.';
    case 409:
      return 'This action conflicts with the current state. Please refresh and try again.';
    case 503:
      return 'The system is busy. Please try again in a moment.';
    default:
      return 'Something went wrong. Please try again.';
  }
}

function jsonHeaders(studentId?: string): HeadersInit {
  const headers: Record<string, string> = { 'Content-Type': 'application/json' };
  if (studentId) headers['X-Student-Id'] = studentId;
  return headers;
}

export const api = {
  listClasses(): Promise<ClassSummary[]> {
    return request<ClassSummary[]>('/classes');
  },

  getClass(classId: string): Promise<ClassSummary> {
    return request<ClassSummary>(`/classes/${encodeURIComponent(classId)}`);
  },

  createClass(name: string, totalSeats: number): Promise<CreatedClass> {
    return request<CreatedClass>('/classes', {
      method: 'POST',
      headers: jsonHeaders(),
      body: JSON.stringify({ name, totalSeats }),
    });
  },

  createHold(classId: string, studentId: string): Promise<Hold> {
    return request<Hold>(`/classes/${encodeURIComponent(classId)}/holds`, {
      method: 'POST',
      headers: jsonHeaders(studentId),
    });
  },

  confirmHold(holdId: string, studentId: string): Promise<{ holdId: string; status: string }> {
    return request(`/holds/${encodeURIComponent(holdId)}/confirm`, {
      method: 'POST',
      headers: jsonHeaders(studentId),
    });
  },

  cancelHold(holdId: string, studentId: string): Promise<void> {
    return request<void>(`/holds/${encodeURIComponent(holdId)}`, {
      method: 'DELETE',
      headers: jsonHeaders(studentId),
    });
  },

  myHolds(studentId: string): Promise<MyHold[]> {
    return request<MyHold[]>('/me/holds', {
      headers: jsonHeaders(studentId),
    });
  },
};
