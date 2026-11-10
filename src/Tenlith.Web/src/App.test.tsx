import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { it, expect, vi, afterEach } from "vitest";
import { App } from "./App";
import { setSession } from "./api";

afterEach(() => {
  setSession(null);
  vi.unstubAllGlobals();
  history.replaceState({}, "", "/");
});
const mount = () =>
  render(
    <QueryClientProvider
      client={
        new QueryClient({ defaultOptions: { queries: { retry: false } } })
      }
    >
      <App />
    </QueryClientProvider>,
  );
it("keeps the invitation journey open after signing in from an invitation link", async () => {
  history.replaceState({}, "", "/invite?token=personal-invitation");
  vi.stubGlobal("fetch", async (path: string) => {
    if (path.endsWith("/config"))
      return new Response('{"demoEnabled":false,"githubEnabled":false}');
    if (path.endsWith("/refresh")) return new Response("{}", { status: 401 });
    if (path.endsWith("/login"))
      return new Response(
        '{"accessToken":"memory-only","user":{"id":"u1","name":"Ada","email":"ada@example.com"}}',
      );
    if (path.endsWith("/organizations")) return new Response("[]");
    return new Response("{}");
  });
  mount();
  await userEvent.type(
    await screen.findByLabelText("Email address"),
    "ada@example.com",
  );
  await userEvent.type(
    screen.getByLabelText("Password"),
    "Correct-password123!",
  );
  await userEvent.click(screen.getByRole("button", { name: "Sign in" }));
  expect(
    await screen.findByRole("heading", { name: "A space on the team." }),
  ).toBeInTheDocument();
  expect(location.pathname).toBe("/invite");
});
it("keeps login server errors visible and lets the user retry", async () => {
  history.replaceState({}, "", "/login");
  vi.stubGlobal("fetch", async (path: string) =>
    path.endsWith("/config")
      ? new Response('{"demoEnabled":false,"githubEnabled":false}')
      : new Response('{"detail":"Email or password is incorrect."}', {
          status: 400,
        }),
  );
  mount();
  await userEvent.type(
    await screen.findByLabelText("Email address"),
    "ada@example.com",
  );
  await userEvent.type(screen.getByLabelText("Password"), "incorrect");
  await userEvent.click(screen.getByRole("button", { name: "Sign in" }));
  expect(await screen.findByRole("alert")).toHaveTextContent(
    "Email or password is incorrect.",
  );
  expect(screen.getByRole("button", { name: "Sign in" })).toBeEnabled();
});
it("selects an existing organization and scopes overview to its id", async () => {
  history.replaceState({}, "", "/app");
  setSession({
    accessToken: "in-memory",
    user: { id: "u1", name: "Ada Lovelace", email: "ada@example.com" },
  });
  const paths: string[] = [];
  vi.stubGlobal("fetch", async (path: string) => {
    paths.push(path);
    if (path.endsWith("/organizations"))
      return new Response(
        '[{"id":"org-1","name":"Analytical","role":"Owner","plan":"Free"}]',
      );
    if (path.endsWith("/overview"))
      return new Response(
        '{"projectCount":2,"memberCount":1,"plan":"Free","projectLimit":3,"memberLimit":3,"activity":[]}',
      );
    if (path.endsWith("/projects")) return new Response("[]");
    return new Response("{}");
  });
  mount();
  expect(
    await screen.findByRole("heading", { name: /Good .*Ada/ }),
  ).toBeInTheDocument();
  await waitFor(() =>
    expect(paths).toContain("/api/organizations/org-1/overview"),
  );
  expect(screen.getByRole("combobox", { name: "Organization" })).toHaveValue(
    "org-1",
  );
});
