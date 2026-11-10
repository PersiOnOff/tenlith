import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import {
  ArrowUpRight,
  FolderKanban,
  Pencil,
  Plus,
  Search,
  Trash2,
} from "lucide-react";
import { api } from "./api";
import {
  ActionForm,
  Button,
  Empty,
  ErrorBox,
  Field,
  Loading,
  Modal,
  date,
} from "./ui";
import type { OrgProps } from "./Workspace";
import type { Project } from "./types";
export function Projects({ org, refresh }: OrgProps) {
  const projects = useQuery({
    queryKey: ["org", org.id, "projects"],
    queryFn: () => api<Project[]>(`/organizations/${org.id}/projects`),
  });
  const [search, setSearch] = useState("");
  const [filter, setFilter] = useState("All");
  const [editing, setEditing] = useState<Project | null | undefined>(undefined);
  const [deleting, setDeleting] = useState<Project | null>(null);
  const canWrite = org.role !== "Member";
  const [color, setColor] = useState("#c8f56b");
  const filtered = projects.data?.filter(
    (p) =>
      (filter === "All" || p.status === filter) &&
      `${p.name} ${p.description}`.toLowerCase().includes(search.toLowerCase()),
  );
  function open(project: Project | null) {
    setColor(project?.color || "#c8f56b");
    setEditing(project);
  }
  return (
    <>
      <div className="page-heading">
        <div>
          <div className="eyebrow">A HOME FOR YOUR NEXT BIG THING</div>
          <h1>
            Projects<span className="greeting-dot">.</span>
          </h1>
          <p>Turn a little momentum into something great.</p>
        </div>
        {canWrite && (
          <Button className="primary" onClick={() => open(null)}>
            <Plus size={16} /> New project
          </Button>
        )}
      </div>
      <div className="toolbar">
        <div className="tabs" role="group" aria-label="Project status">
          {["All", "Active", "Archived"].map((status) => (
            <button
              key={status}
              aria-pressed={filter === status}
              onClick={() => setFilter(status)}
            >
              {status}
              {status === "All" && <span>{projects.data?.length ?? 0}</span>}
            </button>
          ))}
        </div>
        <label className="search">
          <Search size={16} />
          <input
            aria-label="Search projects"
            placeholder="Search projects…"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        </label>
      </div>
      <ErrorBox error={projects.error} />
      {org.role === "Member" && (
        <div className="read-only-note">
          You have read access. An Owner or Admin can create and manage
          projects.
        </div>
      )}
      {projects.isPending ? (
        <Loading />
      ) : filtered?.length ? (
        <div className="project-grid">
          {filtered.map((project) => (
            <article key={project.id} className="project-card">
              <div className="project-card-top">
                <span
                  className="project-icon"
                  style={
                    { "--project-color": project.color } as React.CSSProperties
                  }
                >
                  <FolderKanban size={23} />
                </span>
                <span
                  className={`status ${project.status === "Active" ? "active" : ""}`}
                >
                  <i />
                  {project.status}
                </span>
              </div>
              <h2>{project.name}</h2>
              <p>
                {project.description ||
                  "No description yet. A little space for a big idea."}
              </p>
              <div className="project-card-foot">
                <span>Created {date(project.createdAt)}</span>
                {canWrite && (
                  <div>
                    <button
                      className="icon-button"
                      aria-label={`Edit ${project.name}`}
                      onClick={() => open(project)}
                    >
                      <Pencil size={16} />
                    </button>
                    <button
                      className="icon-button delete-icon"
                      aria-label={`Delete ${project.name}`}
                      onClick={() => setDeleting(project)}
                    >
                      <Trash2 size={16} />
                    </button>
                  </div>
                )}
              </div>
            </article>
          ))}
        </div>
      ) : (
        !projects.error && (
          <div className="panel">
            <Empty
              icon={<FolderKanban />}
              title={
                search || filter !== "All"
                  ? "No projects match."
                  : "Your next idea belongs here."
              }
              action={
                canWrite && !search && filter === "All" ? (
                  <Button className="secondary" onClick={() => open(null)}>
                    <Plus size={15} /> Create your first project
                  </Button>
                ) : undefined
              }
            >
              {search || filter !== "All"
                ? "Try a different search or status."
                : "Create a project to give your work a name, a purpose and a place to grow."}
            </Empty>
          </div>
        )
      )}
      {editing !== undefined && (
        <Modal
          title={editing ? "Edit project" : "Create a project"}
          onClose={() => setEditing(undefined)}
        >
          <ActionForm
            submit={editing ? "Save changes" : "Create project"}
            action={async (data) => {
              await api(
                `/organizations/${org.id}/projects${editing ? `/${editing.id}` : ""}`,
                {
                  method: editing ? "PATCH" : "POST",
                  body: {
                    name: data.get("name"),
                    description: data.get("description"),
                    color,
                    ...(editing ? { status: data.get("status") } : {}),
                  },
                },
              );
              await refresh();
              setEditing(undefined);
            }}
          >
            <Field label="Project name">
              <input
                name="name"
                required
                maxLength={100}
                defaultValue={editing?.name}
                placeholder="e.g. The next big thing"
              />
            </Field>
            <Field label="Description">
              <textarea
                name="description"
                maxLength={2000}
                defaultValue={editing?.description}
                placeholder="What are you building?"
                rows={3}
              />
            </Field>
            <Field label="Project color">
              <div className="color-choices">
                {[
                  "#c8f56b",
                  "#82b8f7",
                  "#e7b77a",
                  "#d4a6e9",
                  "#f18d8d",
                  "#8ccebf",
                ].map((c) => (
                  <button
                    key={c}
                    type="button"
                    style={{ background: c }}
                    className={color === c ? "selected" : ""}
                    aria-label={`Use color ${c}`}
                    aria-pressed={color === c}
                    onClick={() => setColor(c)}
                  />
                ))}
                <input
                  aria-label="Custom project color"
                  type="color"
                  value={color}
                  onChange={(e) => setColor(e.target.value)}
                />
              </div>
            </Field>
            {editing && (
              <Field label="Status">
                <select name="status" defaultValue={editing.status}>
                  <option>Active</option>
                  <option>Archived</option>
                </select>
              </Field>
            )}
          </ActionForm>
        </Modal>
      )}
      {deleting && (
        <Modal title="Delete this project?" onClose={() => setDeleting(null)}>
          <ActionForm
            submit="Delete project"
            danger
            action={async () => {
              await api(`/organizations/${org.id}/projects/${deleting.id}`, {
                method: "DELETE",
              });
              await refresh();
              setDeleting(null);
            }}
          >
            <p className="muted">
              “{deleting.name}” will be permanently deleted. This action cannot
              be undone.
            </p>
          </ActionForm>
        </Modal>
      )}
    </>
  );
}
