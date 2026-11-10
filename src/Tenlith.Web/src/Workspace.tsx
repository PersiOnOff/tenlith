import { useEffect, useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import {
  ArrowDownUp,
  ArrowRight,
  ArrowUpRight,
  Building2,
  Check,
  ChevronDown,
  CircleHelp,
  CreditCard,
  FolderKanban,
  LayoutDashboard,
  LogOut,
  Menu,
  Plus,
  Settings2,
  ShieldCheck,
  Users,
  X,
  Zap,
} from "lucide-react";
import { api, setSession } from "./api";
import { useSession } from "./Auth";
import { navigate } from "./navigation";
import {
  ActionForm,
  Avatar,
  Button,
  Empty,
  ErrorBox,
  Field,
  Loading,
  Mark,
  Modal,
  Notice,
  ThemeButton,
  relative,
} from "./ui";
import type { Organization, Overview, Project } from "./types";
import { Projects } from "./Projects";
import { Team } from "./Team";
import { BillingPage, SettingsPage } from "./WorkspaceSettings";
export type OrgProps = { org: Organization; refresh: () => Promise<void> };
export function Workspace({ path }: { path: string }) {
  const client = useQueryClient();
  const session = useSession()!;
  const [selected, setSelected] = useState("");
  const [creating, setCreating] = useState(false);
  const [mobile, setMobile] = useState(false);
  const [logoutError, setLogoutError] = useState<unknown>(null);
  const organizations = useQuery({
    queryKey: ["organizations"],
    queryFn: () => api<Organization[]>("/organizations"),
  });
  const org =
    organizations.data?.find((o) => o.id === selected) ??
    organizations.data?.[0];
  const page = path.split("/")[2] || "overview";
  async function refresh() {
    await Promise.all([
      client.invalidateQueries({ queryKey: ["organizations"] }),
      client.invalidateQueries({ queryKey: ["org", org?.id] }),
    ]);
  }
  useEffect(() => {
    setMobile(false);
  }, [path]);
  if (path === "/invite")
    return (
      <div className="onboarding">
        <a href="/" aria-label="Tenlith home">
          <Mark />
        </a>
        <div className="onboarding-card">
          <span className="auth-symbol">
            <Users />
          </span>
          <h1>A space on the team.</h1>
          <p className="muted">
            You’re signed in as {session.user.email}. Accept an invitation
            addressed to this email to join the workspace.
          </p>
          <ActionForm
            submit="Accept invitation"
            action={async () => {
              const token = new URLSearchParams(location.search).get("token");
              if (!token)
                throw new Error("This invitation link is incomplete.");
              const accepted = await api<Organization>("/invitations/accept", {
                method: "POST",
                body: { token },
              });
              await refresh();
              setSelected(accepted.id);
              navigate("/app");
            }}
          >
            <p className="muted">
              Invitations are personal, single-use and expire automatically.
            </p>
          </ActionForm>
          <a href="/app" className="text-link">
            Back to my workspaces <ArrowRight size={15} />
          </a>
        </div>
      </div>
    );
  if (organizations.isPending) return <Loading />;
  if (organizations.error)
    return (
      <main className="onboarding">
        <Mark />
        <ErrorBox error={organizations.error} />
        <Button onClick={() => organizations.refetch()}>Try again</Button>
      </main>
    );
  const createForm = (
    <ActionForm
      submit="Create workspace"
      action={async (data) => {
        const result = await api<Organization>("/organizations", {
          method: "POST",
          body: { name: data.get("name") },
        });
        await refresh();
        setSelected(result.id);
        setCreating(false);
        navigate("/app");
      }}
    >
      <Field label="Workspace name" hint="You can change this later.">
        <input
          autoFocus
          name="name"
          required
          maxLength={100}
          placeholder="e.g. Acme Studio"
        />
      </Field>
    </ActionForm>
  );
  if (!org)
    return (
      <div className="onboarding">
        <a href="/" aria-label="Tenlith home">
          <Mark />
        </a>
        <div className="onboarding-card">
          <span className="eyebrow">YOUR SPACE TO BUILD</span>
          <h1>Let’s lay the foundation.</h1>
          <p className="muted">
            Create a workspace for your product or team. Projects, people and
            plans, all in one place.
          </p>
          {createForm}
          <div className="onboarding-note">
            <ShieldCheck size={17} /> Start free. Invite your team when you’re
            ready.
          </div>
        </div>
        <span className="micro">Signed in as {session.user.email}</span>
      </div>
    );
  const navItems = [
    { key: "overview", label: "Overview", icon: LayoutDashboard },
    { key: "projects", label: "Projects", icon: FolderKanban },
    { key: "team", label: "Team", icon: Users },
    { key: "billing", label: "Billing", icon: CreditCard },
    { key: "settings", label: "Settings", icon: Settings2 },
  ];
  return (
    <div className="workspace">
      <a className="skip-link" href="#main-content">
        Skip to content
      </a>
      {mobile && (
        <div className="sidebar-scrim" onClick={() => setMobile(false)} />
      )}
      <aside className={`sidebar ${mobile ? "mobile-open" : ""}`}>
        <a className="sidebar-brand" href="/" aria-label="Tenlith home">
          <Mark />
        </a>
        <div className="workspace-picker">
          <div className="workspace-monogram">
            {org.name.slice(0, 1).toUpperCase()}
          </div>
          <div>
            <span className="micro">WORKSPACE</span>
            <select
              aria-label="Organization"
              value={org.id}
              onChange={(e) => setSelected(e.target.value)}
            >
              {organizations.data?.map((o) => (
                <option key={o.id} value={o.id}>
                  {o.name}
                </option>
              ))}
            </select>
          </div>
          <ChevronDown size={15} />
        </div>
        <button className="new-workspace" onClick={() => setCreating(true)}>
          <Plus size={13} /> New workspace
        </button>
        <div className="nav-caption">WORKSPACE</div>
        <nav aria-label="Workspace navigation">
          {navItems.map((item) => (
            <a
              key={item.key}
              href={item.key === "overview" ? "/app" : `/app/${item.key}`}
              aria-current={page === item.key ? "page" : undefined}
              onClick={(e) => {
                e.preventDefault();
                navigate(e.currentTarget.getAttribute("href")!);
              }}
            >
              <item.icon size={18} />
              {item.label}
              {item.key === "projects" && (
                <span className="nav-arrow" aria-hidden="true">
                  ↗
                </span>
              )}
            </a>
          ))}
        </nav>
        <div className="sidebar-bottom">
          <div className="plan-note">
            <span>
              <Zap size={16} />
              {org.plan} plan
            </span>
            <p>
              {org.plan === "Free"
                ? "A solid start for your next thing."
                : "More room for your growing team."}
            </p>
            <a
              href="/app/billing"
              onClick={(e) => {
                e.preventDefault();
                navigate("/app/billing");
              }}
            >
              Manage plan <ArrowUpRight size={14} />
            </a>
          </div>
          <a className="sidebar-help" href="/">
            <CircleHelp size={16} /> About Tenlith <ArrowUpRight size={13} />
          </a>
          <div className="profile">
            <Avatar name={session.user.name} />
            <div>
              <strong>{session.user.name}</strong>
              <span>{org.role}</span>
            </div>
            <button
              className="icon-button"
              aria-label="Sign out"
              onClick={async () => {
                try {
                  await api("/auth/logout", { method: "POST", body: {} });
                  setSession(null);
                  client.clear();
                  navigate("/login");
                } catch (e) {
                  setLogoutError(e);
                }
              }}
            >
              <LogOut size={17} />
            </button>
          </div>
          <ErrorBox error={logoutError} />
        </div>
      </aside>
      <div className="workspace-body">
        <header className="workspace-top">
          <div className="breadcrumb">
            <button
              className="icon-button mobile-menu"
              aria-label="Open navigation"
              onClick={() => setMobile(true)}
            >
              <Menu size={19} />
            </button>
            <span>{org.name}</span>
            <span className="slash">/</span>
            <strong>
              {navItems.find((i) => i.key === page)?.label ?? "Page"}
            </strong>
          </div>
          <div className="top-actions">
            <span className="workspace-badge">
              <span /> WORKSPACE CONNECTED
            </span>
            <ThemeButton />
            <Avatar name={session.user.name} />
          </div>
        </header>
        <main id="main-content" className="workspace-content" key={org.id}>
          {page === "overview" ? (
            <OverviewPage org={org} refresh={refresh} />
          ) : page === "projects" ? (
            <Projects org={org} refresh={refresh} />
          ) : page === "team" ? (
            <Team org={org} refresh={refresh} />
          ) : page === "billing" ? (
            <BillingPage org={org} refresh={refresh} />
          ) : page === "settings" ? (
            <SettingsPage org={org} refresh={refresh} />
          ) : (
            <Empty title="Page not found">
              Choose a page from the workspace navigation.
            </Empty>
          )}
        </main>
        <footer className="workspace-footer">
          <span>Built on a solid foundation.</span>
          <Mark small />
          <span>YOUR SAAS. YOUR WAY.</span>
        </footer>
      </div>
      {creating && (
        <Modal title="Create a workspace" onClose={() => setCreating(false)}>
          {createForm}
        </Modal>
      )}
    </div>
  );
}
function OverviewPage({ org }: OrgProps) {
  const session = useSession()!;
  const overview = useQuery({
    queryKey: ["org", org.id, "overview"],
    queryFn: () => api<Overview>(`/organizations/${org.id}/overview`),
  });
  const projects = useQuery({
    queryKey: ["org", org.id, "projects"],
    queryFn: () => api<Project[]>(`/organizations/${org.id}/projects`),
  });
  const hour = new Date().getHours();
  const greeting = hour < 12 ? "morning" : hour < 18 ? "afternoon" : "evening";
  const data = overview.data;
  return (
    <>
      <div className="page-heading overview-heading">
        <div>
          <div className="eyebrow">YOUR WORKSPACE, AT A GLANCE</div>
          <h1>
            Good {greeting}, {session.user.name.split(" ")[0]}
            <span className="greeting-dot">.</span>
          </h1>
          <p>Here’s what’s happening at {org.name}.</p>
        </div>
        <a
          href="/app/projects"
          className="button primary"
          onClick={(e) => {
            e.preventDefault();
            navigate("/app/projects");
          }}
        >
          <Plus size={16} />{" "}
          {org.role === "Member" ? "View projects" : "Create a project"}
        </a>
      </div>
      <ErrorBox error={overview.error} />
      {overview.isPending ? (
        <Loading />
      ) : (
        data && (
          <>
            <div className="stats-grid">
              <Stat
                icon={<FolderKanban size={18} />}
                label="Total projects"
                value={data.projectCount}
                limit={data.projectLimit}
                note="Projects in your workspace"
              />
              <Stat
                icon={<Users size={18} />}
                label="Team members"
                value={data.memberCount}
                limit={data.memberLimit}
                note="People building together"
              />
              <div className="stat-card plan-stat">
                <div className="stat-top">
                  <span>Current plan</span>
                  <CreditCard size={18} />
                </div>
                <div className="stat-value">
                  {data.plan}
                  <span className="badge lime">
                    {data.plan === "Free" ? "THE FOUNDATION" : "ROOM TO GROW"}
                  </span>
                </div>
                <a
                  className="stat-plan-link"
                  href="/app/billing"
                  onClick={(e) => {
                    e.preventDefault();
                    navigate("/app/billing");
                  }}
                >
                  Explore your plan <ArrowUpRight size={14} />
                </a>
              </div>
            </div>
            <div className="overview-columns">
              <section className="panel projects-panel">
                <div className="panel-heading">
                  <div>
                    <h2>Your projects</h2>
                    <p>Good ideas, moving forward.</p>
                  </div>
                  <a
                    className="text-link"
                    href="/app/projects"
                    onClick={(e) => {
                      e.preventDefault();
                      navigate("/app/projects");
                    }}
                  >
                    View all <ArrowRight size={15} />
                  </a>
                </div>
                <ErrorBox error={projects.error} />
                {projects.isPending ? (
                  <Loading />
                ) : projects.data?.length ? (
                  <div className="project-overview-list">
                    {projects.data.slice(0, 4).map((project) => (
                      <a
                        className="project-overview-row"
                        href="/app/projects"
                        key={project.id}
                        onClick={(e) => {
                          e.preventDefault();
                          navigate("/app/projects");
                        }}
                      >
                        <span
                          className="project-icon"
                          style={
                            {
                              "--project-color": project.color,
                            } as React.CSSProperties
                          }
                        >
                          <FolderKanban size={19} />
                        </span>
                        <div>
                          <strong>{project.name}</strong>
                          <p>
                            {project.description ||
                              "A new idea, ready to take shape."}
                          </p>
                        </div>
                        <span
                          className={`status ${project.status === "Active" ? "active" : ""}`}
                        >
                          <i />
                          {project.status}
                        </span>
                        <ArrowUpRight size={16} className="muted" />
                      </a>
                    ))}
                  </div>
                ) : (
                  <Empty
                    title="A clean slate, full of potential."
                    icon={<FolderKanban />}
                    action={
                      <a
                        href="/app/projects"
                        className="text-link"
                        onClick={(e) => {
                          e.preventDefault();
                          navigate("/app/projects");
                        }}
                      >
                        Start your first project <ArrowRight size={15} />
                      </a>
                    }
                  >
                    Your projects will live here. Give your next idea a home.
                  </Empty>
                )}
                <div className="panel-foot">
                  <span className="tiny-dot" /> {data.projectCount} of{" "}
                  {data.projectLimit} project slots in use
                  <span>{data.projectLimit - data.projectCount} available</span>
                </div>
              </section>
              <section className="panel activity-panel">
                <div className="panel-heading">
                  <div>
                    <h2>Recent activity</h2>
                    <p>The little steps that add up.</p>
                  </div>
                  <span className="activity-indicator" />
                </div>
                {data.activity.length ? (
                  <ol className="activity-list">
                    {data.activity.slice(0, 6).map((event, index) => (
                      <li key={event.id}>
                        <span className={`activity-icon activity-${index % 3}`}>
                          {index % 3 === 0 ? (
                            <FolderKanban size={14} />
                          ) : index % 3 === 1 ? (
                            <Users size={14} />
                          ) : (
                            <Check size={14} />
                          )}
                        </span>
                        <div>
                          <strong>
                            {event.action
                              .replace(/([a-z])([A-Z])/g, "$1 $2")
                              .replace(/[._]/g, " ")}
                          </strong>
                          <p>
                            {event.actorName}{" "}
                            <span>· {relative(event.createdAt)}</span>
                          </p>
                        </div>
                      </li>
                    ))}
                  </ol>
                ) : (
                  <Empty title="Your story starts here.">
                    Workspace activity will appear as your team makes progress.
                  </Empty>
                )}
              </section>
            </div>
            <section className="workspace-callout">
              <div className="callout-icon">
                <Building2 size={27} />
              </div>
              <div>
                <span className="eyebrow">BETTER, TOGETHER</span>
                <h2>A great workspace starts with great people.</h2>
                <p>Bring your team in and build something worth building.</p>
              </div>
              <a
                href="/app/team"
                className="button secondary"
                onClick={(e) => {
                  e.preventDefault();
                  navigate("/app/team");
                }}
              >
                Meet your team <ArrowUpRight size={16} />
              </a>
              <span className="callout-art" aria-hidden="true">
                ↗
              </span>
            </section>
          </>
        )
      )}
    </>
  );
}
function Stat({
  icon,
  label,
  value,
  limit,
  note,
}: {
  icon: React.ReactNode;
  label: string;
  value: number;
  limit: number;
  note: string;
}) {
  return (
    <div className="stat-card">
      <div className="stat-top">
        <span>{label}</span>
        {icon}
      </div>
      <div className="stat-value">
        {String(value).padStart(2, "0")}
        <span className="stat-limit">/ {limit}</span>
      </div>
      <div className="stat-bottom">
        <span>{note}</span>
        <span className="usage-track">
          <i style={{ width: `${Math.min(100, (value / limit) * 100)}%` }} />
        </span>
      </div>
    </div>
  );
}
