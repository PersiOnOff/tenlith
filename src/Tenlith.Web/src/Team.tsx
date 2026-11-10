import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import {
  ArrowRightLeft,
  Mail,
  Plus,
  ShieldCheck,
  Trash2,
  Users,
} from "lucide-react";
import { api } from "./api";
import { useSession } from "./Auth";
import {
  ActionForm,
  Avatar,
  Button,
  Empty,
  ErrorBox,
  Field,
  Loading,
  Modal,
  Notice,
  date,
} from "./ui";
import type { OrgProps } from "./Workspace";
import type { Invitation, Member, Role } from "./types";
export function Team({ org, refresh }: OrgProps) {
  const session = useSession()!;
  const canManage = org.role !== "Member";
  const members = useQuery({
    queryKey: ["org", org.id, "members"],
    queryFn: () => api<Member[]>(`/organizations/${org.id}/members`),
  });
  const invitations = useQuery({
    queryKey: ["org", org.id, "invitations"],
    queryFn: () => api<Invitation[]>(`/organizations/${org.id}/invitations`),
    enabled: canManage,
  });
  const [inviting, setInviting] = useState(false);
  const [message, setMessage] = useState("");
  const [error, setError] = useState<unknown>(null);
  const [pending, setPending] = useState(false);
  const [removing, setRemoving] = useState<Member | null>(null);
  const [transferring, setTransferring] = useState<Member | null>(null);
  async function changeRole(member: Member, role: Role) {
    setError(null);
    setPending(true);
    try {
      await api(`/organizations/${org.id}/members/${member.id}`, {
        method: "PATCH",
        body: { role },
      });
      await refresh();
    } catch (e) {
      setError(e);
    } finally {
      setPending(false);
    }
  }
  return (
    <>
      <div className="page-heading">
        <div>
          <div className="eyebrow">GOOD THINGS ARE BUILT TOGETHER</div>
          <h1>
            Your team<span className="greeting-dot">.</span>
          </h1>
          <p>The people behind your next big thing.</p>
        </div>
        {canManage && (
          <Button
            className="primary"
            onClick={() => {
              setMessage("");
              setInviting(true);
            }}
          >
            <Plus size={16} /> Invite member
          </Button>
        )}
      </div>
      {message && <Notice>{message}</Notice>}
      <ErrorBox error={error} />
      <ErrorBox error={members.error} />
      <section className="panel">
        <div className="panel-heading">
          <div>
            <h2>
              Workspace members{" "}
              <span className="count-badge">{members.data?.length ?? 0}</span>
            </h2>
            <p>Everyone with a place in {org.name}.</p>
          </div>
          <span className="badge">{org.role} access</span>
        </div>
        {members.isPending ? (
          <Loading />
        ) : (
          <div className="table-scroll">
            <table className="team-table">
              <thead>
                <tr>
                  <th>MEMBER</th>
                  <th>ROLE</th>
                  <th>JOINED</th>
                  <th>
                    <span className="sr-only">Actions</span>
                  </th>
                </tr>
              </thead>
              <tbody>
                {members.data?.map((member) => {
                  const editable =
                    member.role !== "Owner" &&
                    (org.role === "Owner" ||
                      (org.role === "Admin" && member.role === "Member"));
                  return (
                    <tr key={member.id}>
                      <td>
                        <div className="member-cell">
                          <Avatar name={member.name} />
                          <div>
                            <strong>
                              {member.name}{" "}
                              {member.userId === session.user.id && (
                                <small className="you-badge">you</small>
                              )}
                            </strong>
                            <span>{member.email}</span>
                          </div>
                        </div>
                      </td>
                      <td>
                        {org.role === "Owner" && member.role !== "Owner" ? (
                          <select
                            className="role-select"
                            aria-label={`Role for ${member.name}`}
                            value={member.role}
                            disabled={pending}
                            onChange={(e) =>
                              changeRole(member, e.target.value as Role)
                            }
                          >
                            <option>Admin</option>
                            <option>Member</option>
                          </select>
                        ) : (
                          <span
                            className={`role-badge ${member.role === "Owner" ? "owner" : ""}`}
                          >
                            {member.role === "Owner" && (
                              <ShieldCheck size={13} />
                            )}{" "}
                            {member.role}
                          </span>
                        )}
                      </td>
                      <td className="muted">{date(member.joinedAt)}</td>
                      <td>
                        <div className="row-actions">
                          {org.role === "Owner" && member.role !== "Owner" && (
                            <button
                              className="icon-button"
                              aria-label={`Transfer ownership to ${member.name}`}
                              onClick={() => setTransferring(member)}
                            >
                              <ArrowRightLeft size={16} />
                            </button>
                          )}
                          {editable && (
                            <button
                              className="icon-button delete-icon"
                              aria-label={`Remove ${member.name}`}
                              onClick={() => setRemoving(member)}
                            >
                              <Trash2 size={16} />
                            </button>
                          )}
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </section>
      {canManage && (
        <section className="panel invitations-panel">
          <div className="panel-heading">
            <div>
              <h2>
                Pending invitations{" "}
                <span className="count-badge">
                  {invitations.data?.length ?? 0}
                </span>
              </h2>
              <p>A little room for more good people.</p>
            </div>
            <Mail size={20} className="muted" />
          </div>
          <ErrorBox error={invitations.error} />
          {invitations.isPending ? (
            <Loading />
          ) : invitations.data?.length ? (
            <div className="invitation-list">
              {invitations.data.map((invitation) => (
                <div key={invitation.id}>
                  <span className="invitation-icon">
                    <Mail size={18} />
                  </span>
                  <div>
                    <strong>{invitation.email}</strong>
                    <p>
                      {invitation.role} · Expires {date(invitation.expiresAt)}
                    </p>
                  </div>
                  <Button
                    className="plain"
                    busy={pending}
                    onClick={async () => {
                      setError(null);
                      setPending(true);
                      try {
                        await api(
                          `/organizations/${org.id}/invitations/${invitation.id}`,
                          { method: "DELETE" },
                        );
                        await refresh();
                      } catch (e) {
                        setError(e);
                      } finally {
                        setPending(false);
                      }
                    }}
                  >
                    Revoke
                  </Button>
                </div>
              ))}
            </div>
          ) : (
            <Empty icon={<Mail />} title="No invitations waiting.">
              Invite a teammate by email. They’ll get a personal link to join.
            </Empty>
          )}
        </section>
      )}
      <div className="role-guide">
        <ShieldCheck size={20} />
        <div>
          <strong>A little clarity goes a long way.</strong>
          <p>
            Owners manage everything. Admins manage projects and ordinary
            members. Members can explore projects.
          </p>
        </div>
      </div>
      {inviting && (
        <Modal title="Invite a teammate" onClose={() => setInviting(false)}>
          <ActionForm
            submit="Send invitation"
            action={async (data) => {
              const result = await api<{ message: string }>(
                `/organizations/${org.id}/invitations`,
                {
                  method: "POST",
                  body: { email: data.get("email"), role: data.get("role") },
                },
              );
              await refresh();
              setInviting(false);
              setMessage(result.message);
            }}
          >
            <p className="muted">
              Good work is better together. Send a personal invitation to your
              workspace.
            </p>
            <Field label="Email address">
              <input
                name="email"
                type="email"
                required
                placeholder="teammate@company.com"
              />
            </Field>
            <Field label="Role">
              <select name="role">
                <option>Member</option>
                {org.role === "Owner" && <option>Admin</option>}
              </select>
            </Field>
          </ActionForm>
        </Modal>
      )}
      {removing && (
        <Modal title="Remove this member?" onClose={() => setRemoving(null)}>
          <ActionForm
            submit="Remove member"
            danger
            action={async () => {
              await api(`/organizations/${org.id}/members/${removing.id}`, {
                method: "DELETE",
              });
              await refresh();
              setRemoving(null);
            }}
          >
            <p className="muted">
              {removing.name} will lose access to {org.name}. You can invite
              them again later.
            </p>
          </ActionForm>
        </Modal>
      )}
      {transferring && (
        <Modal
          title="Transfer workspace ownership?"
          onClose={() => setTransferring(null)}
        >
          <ActionForm
            submit="Transfer ownership"
            danger
            action={async (data) => {
              if (data.get("confirm") !== org.name)
                throw new Error("Enter the workspace name exactly to confirm.");
              await api(`/organizations/${org.id}/transfer-ownership`, {
                method: "POST",
                body: { memberId: transferring.id },
              });
              await refresh();
              setTransferring(null);
            }}
          >
            <p className="muted">
              {transferring.name} will become the Owner and gain control of
              billing and workspace deletion. Your role will become Admin.
            </p>
            <Field label={`Type ${org.name} to confirm`}>
              <input name="confirm" required autoComplete="off" />
            </Field>
          </ActionForm>
        </Modal>
      )}
    </>
  );
}
