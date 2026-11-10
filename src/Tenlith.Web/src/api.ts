export type User = { id: string; name: string; email: string };
export type Session = { accessToken: string; user: User };
let session: Session | null = null;
let refreshPromise: Promise<Session> | null = null;
const listeners = new Set<() => void>();
export function setSession(value: Session | null) {
  session = value;
  listeners.forEach((listener) => listener());
}
export function getSession() {
  return session;
}
export function subscribeSession(listener: () => void) {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}
export class ApiError extends Error {
  constructor(
    message: string,
    public status: number,
  ) {
    super(message);
  }
}
type Options = { method?: string; body?: unknown };
async function request<T>(path: string, options: Options = {}): Promise<T> {
  const method = options.method ?? "GET";
  const headers: Record<string, string> = { Accept: "application/json" };
  if (session) headers.Authorization = `Bearer ${session.accessToken}`;
  if (method !== "GET") {
    headers["X-Tenlith-Client"] = "web";
    headers["Content-Type"] = "application/json";
  }
  const response = await fetch(`/api${path}`, {
    method,
    headers,
    credentials: "same-origin",
    body: options.body === undefined ? undefined : JSON.stringify(options.body),
  });
  if (!response.ok) {
    const problem = (await response.json().catch(() => ({}))) as {
      detail?: string;
      title?: string;
      errors?: Record<string, string[]>;
    };
    const fields = Object.values(problem.errors ?? {})
      .flat()
      .join(" ");
    throw new ApiError(
      fields ||
        problem.detail ||
        problem.title ||
        `Request failed (${response.status}).`,
      response.status,
    );
  }
  return response.status === 204
    ? (undefined as T)
    : ((await response.json()) as T);
}
export function refreshSession() {
  if (!refreshPromise)
    refreshPromise = request<Session>("/auth/refresh", {
      method: "POST",
      body: {},
    })
      .then((value) => {
        setSession(value);
        return value;
      })
      .catch((error) => {
        setSession(null);
        throw error;
      })
      .finally(() => {
        refreshPromise = null;
      });
  return refreshPromise;
}
export async function api<T = unknown>(
  path: string,
  options: Options = {},
): Promise<T> {
  try {
    return await request<T>(path, options);
  } catch (error) {
    if (
      error instanceof ApiError &&
      error.status === 401 &&
      (!path.startsWith("/auth/") || path === "/auth/me")
    ) {
      await refreshSession();
      return request<T>(path, options);
    }
    throw error;
  }
}
