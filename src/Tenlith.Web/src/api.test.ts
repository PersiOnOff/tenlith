import { describe, it, expect, vi, afterEach } from "vitest";
import { api, setSession, getSession } from "./api";

afterEach(() => {
  setSession(null);
  vi.unstubAllGlobals();
});
describe("same-origin API client", () => {
  it("sends the in-memory bearer and CSRF client header for a mutation", async () => {
    setSession({
      accessToken: "memory-secret",
      user: { id: "u1", name: "Ada", email: "ada@example.com" },
    });
    const fetcher = vi.fn(
      async () => new Response(JSON.stringify({ id: "p1" }), { status: 201 }),
    );
    vi.stubGlobal("fetch", fetcher);
    await api("/organizations/o1/projects", {
      method: "POST",
      body: { name: "Launch" },
    });
    const [path, options] = fetcher.mock.calls[0] as unknown as [
      string,
      RequestInit,
    ];
    expect(path).toBe("/api/organizations/o1/projects");
    expect(new Headers(options.headers).get("Authorization")).toBe(
      "Bearer memory-secret",
    );
    expect(new Headers(options.headers).get("X-Tenlith-Client")).toBe("web");
    expect(options.credentials).toBe("same-origin");
    expect(localStorage.getItem("accessToken")).toBeNull();
    expect(getSession()?.user.name).toBe("Ada");
  });
  it("shows server validation details without losing field messages", async () => {
    vi.stubGlobal(
      "fetch",
      async () =>
        new Response(
          JSON.stringify({
            detail: "Validation failed.",
            errors: { Name: ["A name is required."] },
          }),
          { status: 400 },
        ),
    );
    await expect(
      api("/organizations", { method: "POST", body: { name: "" } }),
    ).rejects.toThrow("A name is required.");
  });
  it("shares a single refresh request across concurrent expired requests", async () => {
    let refreshes = 0;
    vi.stubGlobal("fetch", async (path: string, options: RequestInit) => {
      if (path.endsWith("/auth/refresh")) {
        refreshes++;
        return new Response(
          JSON.stringify({
            accessToken: "new",
            user: { id: "u1", name: "Ada", email: "a@b.c" },
          }),
        );
      }
      return new Headers(options.headers).get("Authorization") === "Bearer new"
        ? new Response("[]")
        : new Response("", { status: 401 });
    });
    await Promise.all([api("/organizations"), api("/auth/me")]);
    expect(refreshes).toBe(1);
  });
});
