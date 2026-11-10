import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { it, expect, vi, afterEach } from "vitest";
import { Projects } from "./Projects";
import type { Organization } from "./types";
const org: Organization = {
  id: "org",
  name: "Acme",
  slug: "acme",
  role: "Member",
  plan: "Free",
  createdAt: "2026-01-01",
};
afterEach(() => vi.unstubAllGlobals());
function mount(role: Organization["role"]) {
  return render(
    <QueryClientProvider
      client={
        new QueryClient({ defaultOptions: { queries: { retry: false } } })
      }
    >
      <Projects org={{ ...org, role }} refresh={async () => {}} />
    </QueryClientProvider>,
  );
}
it("lets members read projects but prevents project mutation controls", async () => {
  vi.stubGlobal(
    "fetch",
    async () =>
      new Response(
        '[{"id":"p1","name":"Launch","description":"Our product","status":"Active","color":"#c8f56b","createdAt":"2026-01-01"}]',
      ),
  );
  mount("Member");
  expect(await screen.findByText("Launch")).toBeInTheDocument();
  expect(
    screen.queryByRole("button", { name: "New project" }),
  ).not.toBeInTheDocument();
  expect(
    screen.queryByRole("button", { name: "Edit Launch" }),
  ).not.toBeInTheDocument();
});
it("submits a named project and displays a quota error inside the dialog", async () => {
  vi.stubGlobal("fetch", async (_path: string, options: RequestInit) =>
    options.method === "POST"
      ? new Response('{"detail":"Project limit reached. Upgrade your plan."}', {
          status: 409,
        })
      : new Response("[]"),
  );
  mount("Owner");
  await userEvent.click(
    await screen.findByRole("button", { name: "New project" }),
  );
  await userEvent.type(screen.getByLabelText("Project name"), "Next idea");
  await userEvent.click(screen.getByRole("button", { name: "Create project" }));
  expect(await screen.findByRole("alert")).toHaveTextContent(
    "Project limit reached.",
  );
  expect(screen.getByRole("dialog")).toBeInTheDocument();
});
