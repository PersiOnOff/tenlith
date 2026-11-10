import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import {
  ArrowUpRight,
  Building2,
  Check,
  CreditCard,
  FlaskConical,
  LockKeyhole,
  ShieldCheck,
  Trash2,
  Zap,
} from "lucide-react";
import { api } from "./api";
import { useSession } from "./Auth";
import { navigate } from "./navigation";
import {
  ActionForm,
  Avatar,
  Button,
  ErrorBox,
  Field,
  Loading,
  Modal,
  Notice,
  date,
} from "./ui";
import type { OrgProps } from "./Workspace";
import type { Billing } from "./types";
export function BillingPage({ org, refresh }: OrgProps) {
  const billing = useQuery({
    queryKey: ["org", org.id, "billing"],
    queryFn: () => api<Billing>(`/organizations/${org.id}/billing`),
  });
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<unknown>(null);
  const [message, setMessage] = useState("");
  const data = billing.data;
  async function manage(
    action: "simulate" | "checkout" | "portal",
    plan?: string,
  ) {
    setBusy(true);
    setError(null);
    setMessage("");
    try {
      const result = await api<{ url?: string }>(
        `/organizations/${org.id}/billing/${action}`,
        { method: "POST", body: plan ? { plan } : {} },
      );
      if (result.url) {
        const url = new URL(result.url, location.origin);
        if (!["https:", "http:"].includes(url.protocol))
          throw new Error("Billing provider returned an invalid URL.");
        location.assign(url.href);
      } else {
        await refresh();
        setMessage(
          `Workspace switched to ${plan}. Local simulation only; no payment was taken.`,
        );
      }
    } catch (e) {
      setError(e);
    } finally {
      setBusy(false);
    }
  }
  return (
    <>
      <div className="page-heading">
        <div>
          <div className="eyebrow">ROOM FOR WHAT COMES NEXT</div>
          <h1>
            Plans & billing<span className="greeting-dot">.</span>
          </h1>
          <p>A solid foundation. Space to grow when you need it.</p>
        </div>
        <span className="badge">
          <ShieldCheck size={14} /> Owner-managed billing
        </span>
      </div>
      <ErrorBox error={billing.error} />
      <ErrorBox error={error} />
      {message && <Notice>{message}</Notice>}
      {billing.isPending ? (
        <Loading />
      ) : (
        data && (
          <>
            {data.provider === "simulation" && (
              <div className="simulation-banner">
                <FlaskConical size={21} />
                <div>
                  <strong>You’re in the local billing sandbox.</strong>
                  <p>
                    Try plan changes freely. This simulation never charges a
                    card and is available only in development.
                  </p>
                </div>
                <span className="badge">SIMULATION</span>
              </div>
            )}
            {!data.canManage && (
              <div className="notice info">
                <LockKeyhole size={17} /> Only the workspace Owner can change
                the plan or manage billing.
              </div>
            )}
            {data.provider === "unconfigured" && (
              <div className="notice info">
                Live billing is not configured. Configure the payment provider
                to enable checkout.
              </div>
            )}
            <div className="billing-summary panel">
              <div>
                <span className="micro">YOUR CURRENT PLAN</span>
                <h2>
                  {data.plan} <span className="badge lime">{data.status}</span>
                </h2>
                <p>
                  {data.projectLimit} projects · {data.memberLimit} team members
                </p>
              </div>
              <CreditCard size={38} />
              {data.canManage &&
                data.provider === "stripe" &&
                data.configured && (
                  <Button
                    className="secondary"
                    busy={busy}
                    onClick={() => manage("portal")}
                  >
                    Manage subscription <ArrowUpRight size={16} />
                  </Button>
                )}
            </div>
            <div className="pricing-grid">
              {(["Free", "Pro"] as const).map((plan) => (
                <article
                  key={plan}
                  className={`pricing-card ${plan === "Pro" ? "pro" : ""}`}
                >
                  <div className="pricing-label">
                    <span className="plan-symbol">
                      {plan === "Pro" ? (
                        <Zap size={23} />
                      ) : (
                        <Building2 size={23} />
                      )}
                    </span>
                    {data.plan === plan ? (
                      <span className="badge">CURRENT PLAN</span>
                    ) : plan === "Pro" ? (
                      <span className="badge lime">SPACE TO GROW</span>
                    ) : null}
                  </div>
                  <h2>{plan}</h2>
                  <p>
                    {plan === "Free"
                      ? "The foundation for your next idea."
                      : "For teams with more on the horizon."}
                  </p>
                  <div className="plan-price">
                    {plan === "Free" ? "Free" : "Grow your way"}
                    <span>
                      {plan === "Free"
                        ? "No payment required"
                        : data.provider === "simulation"
                          ? "Explore in the local sandbox"
                          : "Pricing shown securely at checkout"}
                    </span>
                  </div>
                  <ul>
                    {(plan === data.plan
                      ? [
                          `${data.projectLimit} projects`,
                          `${data.memberLimit} team members`,
                          "Separate workspace & role controls",
                          "Project activity & team invitations",
                        ]
                      : plan === "Free"
                        ? [
                            "Starter project capacity",
                            "A small team workspace",
                            "Separate workspace & role controls",
                            "Project activity & team invitations",
                          ]
                        : [
                            "Expanded project capacity",
                            "More room for teammates",
                            "The same workspace, more possibilities",
                            "Project activity & team invitations",
                          ]
                    ).map((feature) => (
                      <li key={feature}>
                        <Check size={16} />
                        {feature}
                      </li>
                    ))}
                  </ul>
                  {data.plan === plan ? (
                    <div className="current-plan">
                      <Check size={16} /> You’re on {plan}
                    </div>
                  ) : data.canManage && data.provider === "simulation" ? (
                    <Button
                      className={plan === "Pro" ? "primary" : "secondary"}
                      busy={busy}
                      onClick={() => manage("simulate", plan)}
                    >
                      Switch to {plan} <ArrowUpRight size={16} />
                    </Button>
                  ) : data.canManage &&
                    data.provider === "stripe" &&
                    data.configured ? (
                    <Button
                      className={plan === "Pro" ? "primary" : "secondary"}
                      busy={busy}
                      onClick={() =>
                        manage(plan === "Pro" ? "checkout" : "portal")
                      }
                    >
                      {plan === "Pro"
                        ? "Continue to checkout"
                        : "Manage in billing portal"}{" "}
                      <ArrowUpRight size={16} />
                    </Button>
                  ) : (
                    <p className="plan-unavailable">
                      {!data.canManage
                        ? "Ask your Owner to change plans."
                        : "Checkout becomes available when billing is configured."}
                    </p>
                  )}
                </article>
              ))}
            </div>
            <div className="billing-footnote">
              <ShieldCheck size={18} />
              <p>
                Plan limits are enforced by the API. Live subscription changes
                take effect after confirmation from your payment provider.
              </p>
            </div>
          </>
        )
      )}
    </>
  );
}
export function SettingsPage({ org, refresh }: OrgProps) {
  const session = useSession()!;
  const [message, setMessage] = useState("");
  const [deleting, setDeleting] = useState(false);
  return (
    <>
      <div className="page-heading">
        <div>
          <div className="eyebrow">MAKE YOURSELF AT HOME</div>
          <h1>
            Settings<span className="greeting-dot">.</span>
          </h1>
          <p>The details that make this workspace yours.</p>
        </div>
      </div>
      {message && <Notice>{message}</Notice>}
      <div className="settings-layout">
        <section className="panel settings-panel">
          <div className="panel-heading">
            <div>
              <h2>Your account</h2>
              <p>Your identity across all your workspaces.</p>
            </div>
            <ShieldCheck size={19} className="muted" />
          </div>
          <div className="account-detail">
            <Avatar name={session.user.name} />
            <div>
              <h3>{session.user.name}</h3>
              <p>{session.user.email}</p>
              <span className="badge">{org.role} in this workspace</span>
            </div>
          </div>
          <div className="account-footer">
            <LockKeyhole size={16} />
            <span>Need a new password?</span>
            <a href="/forgot-password" className="text-link">
              Send a recovery link <ArrowUpRight size={14} />
            </a>
          </div>
        </section>
        <section className="panel settings-panel">
          <div className="panel-heading">
            <div>
              <h2>Workspace details</h2>
              <p>A name your team can make their own.</p>
            </div>
            <Building2 size={19} className="muted" />
          </div>
          <div className="settings-form">
            {org.role === "Owner" ? (
              <ActionForm
                submit="Save workspace"
                action={async (data) => {
                  await api(`/organizations/${org.id}`, {
                    method: "PATCH",
                    body: { name: data.get("name") },
                  });
                  await refresh();
                  setMessage("Workspace details updated.");
                }}
              >
                <Field label="Workspace name">
                  <input
                    key={org.name}
                    name="name"
                    defaultValue={org.name}
                    required
                    maxLength={100}
                  />
                </Field>
                <div className="workspace-facts">
                  <span>Created {date(org.createdAt)}</span>
                  <span className="mono">{org.slug}</span>
                </div>
              </ActionForm>
            ) : (
              <>
                <Field label="Workspace name">
                  <input value={org.name} readOnly />
                </Field>
                <p className="muted">
                  Only the workspace Owner can update these details.
                </p>
              </>
            )}
          </div>
        </section>
        {org.role === "Owner" && (
          <section className="panel danger-panel">
            <div>
              <span className="danger-label">
                <Trash2 size={17} /> A permanent decision
              </span>
              <h2>Delete workspace</h2>
              <p>
                Permanently remove {org.name}, its projects and team access.
                This cannot be undone.
              </p>
            </div>
            <Button
              className="danger-outline"
              onClick={() => setDeleting(true)}
            >
              Delete workspace
            </Button>
          </section>
        )}
      </div>
      {deleting && (
        <Modal
          title="Delete this workspace?"
          onClose={() => setDeleting(false)}
        >
          <ActionForm
            submit="Permanently delete workspace"
            danger
            action={async (data) => {
              if (data.get("confirm") !== org.name)
                throw new Error("Enter the workspace name exactly to confirm.");
              await api(`/organizations/${org.id}`, { method: "DELETE" });
              await refresh();
              setDeleting(false);
              navigate("/app");
            }}
          >
            <p className="muted">
              All projects, membership and workspace data in “{org.name}” will
              be permanently removed.
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
