import { render, screen } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { it, expect, vi, afterEach } from "vitest";
import { BillingPage } from "./WorkspaceSettings";
const org = {
  id: "org",
  name: "Acme",
  slug: "acme",
  role: "Member" as const,
  plan: "Free" as const,
  createdAt: "2026-01-01",
};
afterEach(() => vi.unstubAllGlobals());
it("explains billing owner restrictions without an upgrade action", async () => {
  vi.stubGlobal(
    "fetch",
    async () =>
      new Response(
        '{"plan":"Free","status":"active","provider":"simulation","projectLimit":3,"memberLimit":3,"canManage":false,"configured":true}',
      ),
  );
  render(
    <QueryClientProvider client={new QueryClient()}>
      <BillingPage org={org} refresh={async () => {}} />
    </QueryClientProvider>,
  );
  expect(
    await screen.findByText(/Only the workspace Owner can change/),
  ).toBeInTheDocument();
  expect(
    screen.queryByRole("button", { name: /Switch to Pro/ }),
  ).not.toBeInTheDocument();
});
