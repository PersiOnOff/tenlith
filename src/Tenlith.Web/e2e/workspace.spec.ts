import { test, expect, type Page } from "@playwright/test";

async function navigate(page: Page, name: string) {
  const menu = page.getByRole("button", { name: "Open navigation" });
  if (await menu.isVisible()) await menu.click();
  await page
    .getByRole("navigation", { name: "Workspace navigation" })
    .getByRole("link", { name, exact: true })
    .click();
}

test("the real workspace journey works without provider credentials", async ({
  page,
}, testInfo) => {
  const errors: string[] = [];
  page.on("pageerror", (error) => errors.push(error.message));
  await page.goto("/");
  await expect(page.getByRole("heading", { level: 1 })).toContainText(
    "Your SaaS.",
  );
  await expect(
    page.getByRole("button", { name: "Explore the demo" }).first(),
  ).toBeVisible();
  if (testInfo.project.name === "desktop")
    await page.screenshot({
      path: "../../assets/screenshots/landing.png",
      fullPage: true,
    });
  await page.getByRole("button", { name: "Explore the demo" }).first().click();
  await expect(page.getByRole("heading", { level: 1 })).toContainText("Alex");
  await expect(page.getByText("Brand refresh", { exact: true })).toBeVisible();
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);

  if (testInfo.project.name === "desktop") {
    await page.screenshot({
      path: "../../assets/screenshots/workspace.png",
      fullPage: true,
    });
  } else {
    await page.screenshot({
      path: "../../assets/screenshots/mobile.png",
      fullPage: true,
    });
  }

  await navigate(page, "Projects");
  await page.getByRole("button", { name: "New project", exact: true }).click();
  await page
    .getByLabel("Project name", { exact: true })
    .fill("Browser acceptance");
  await page
    .getByLabel("Description", { exact: true })
    .fill("Created through the real API.");
  await page
    .getByRole("button", { name: "Create project", exact: true })
    .click();
  await expect(
    page.getByRole("heading", { name: "Browser acceptance", exact: true }),
  ).toBeVisible();
  await page.getByRole("button", { name: "New project", exact: true }).click();
  await page
    .getByLabel("Project name", { exact: true })
    .fill("Over the free quota");
  await page
    .getByRole("button", { name: "Create project", exact: true })
    .click();
  await expect(page.getByRole("alert")).toContainText("Project limit reached");
  await page.getByRole("button", { name: "Close dialog" }).click();

  await navigate(page, "Billing");
  await expect(page.locator(".simulation-banner")).toBeVisible();
  await page.getByRole("button", { name: "Switch to Pro" }).click();
  await expect(page.getByRole("status")).toContainText(
    "Workspace switched to Pro",
  );
  await navigate(page, "Projects");
  await page.getByRole("button", { name: "New project", exact: true }).click();
  await page.getByLabel("Project name", { exact: true }).fill("Room to grow");
  await page
    .getByRole("button", { name: "Create project", exact: true })
    .click();
  await expect(
    page.getByRole("heading", { name: "Room to grow", exact: true }),
  ).toBeVisible();
  await page
    .getByRole("button", { name: "Edit Room to grow", exact: true })
    .click();
  await page.getByRole("combobox", { name: "Status", exact: true }).selectOption("Archived");
  await page.getByRole("button", { name: "Save changes", exact: true }).click();
  await page.getByRole("button", { name: "Archived", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: "Room to grow", exact: true }),
  ).toBeVisible();

  await navigate(page, "Team");
  await page
    .getByRole("button", { name: "Invite member", exact: true })
    .click();
  const email = `browser-${Date.now()}@example.test`;
  await page.getByLabel("Email address", { exact: true }).fill(email);
  await page
    .getByRole("button", { name: "Send invitation", exact: true })
    .click();
  await expect(page.getByText(email, { exact: true })).toBeVisible();

  await navigate(page, "Settings");
  await page
    .getByLabel("Workspace name", { exact: true })
    .fill("Browser Studio");
  await page
    .getByRole("button", { name: "Save workspace", exact: true })
    .click();
  await expect(page.getByRole("status")).toContainText(
    "Workspace details updated",
  );
  await page.reload();
  await expect(page.getByLabel("Workspace name", { exact: true })).toHaveValue(
    "Browser Studio",
  );
  await page.getByRole("button", { name: "Switch to light theme" }).click();
  await expect(page.locator("html")).toHaveAttribute("data-theme", "light");
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);
  expect(errors).toEqual([]);
});
