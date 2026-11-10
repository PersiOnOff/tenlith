import { useEffect, useState, useSyncExternalStore } from "react";
import { useQuery } from "@tanstack/react-query";
import {
  ArrowLeft,
  ArrowUpRight,
  Check,
  Code2 as Github,
  Layers3,
  ShieldCheck,
} from "lucide-react";
import {
  api,
  getSession,
  setSession,
  subscribeSession,
  type Session,
} from "./api";
import {
  ActionForm,
  Button,
  ErrorBox,
  Field,
  Mark,
  Notice,
  ThemeButton,
} from "./ui";
import { navigate } from "./navigation";
export function useSession() {
  return useSyncExternalStore(subscribeSession, getSession);
}
export function useAuthConfig() {
  return useQuery({
    queryKey: ["auth-config"],
    queryFn: () =>
      api<{ githubEnabled: boolean; demoEnabled: boolean }>("/auth/config"),
    staleTime: 60000,
  });
}
export function DemoButton({ className = "" }: { className?: string }) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<unknown>(null);
  return (
    <>
      <Button
        className={`primary ${className}`}
        busy={busy}
        onClick={async () => {
          setBusy(true);
          setError(null);
          try {
            setSession(
              await api<Session>("/auth/demo", { method: "POST", body: {} }),
            );
            navigate("/app");
          } catch (e) {
            setError(e);
          } finally {
            setBusy(false);
          }
        }}
      >
        Explore the demo <ArrowUpRight size={17} />
      </Button>
      <ErrorBox error={error} />
    </>
  );
}
export function Auth({ path }: { path: string }) {
  const config = useAuthConfig();
  const [message, setMessage] = useState("");
  const params = new URLSearchParams(location.search);
  const mode =
    path === "/register"
      ? "register"
      : path === "/forgot-password"
        ? "forgot"
        : path === "/reset-password"
          ? "reset"
          : path === "/verify-email"
            ? "verify"
            : "login";
  const title = {
    register: "Build something great.",
    login: "Welcome back.",
    forgot: "Let’s get you back in.",
    reset: "A fresh start.",
    verify: "Verify your email.",
  }[mode];
  const subtitle = {
    register: "Your next chapter starts with a solid foundation.",
    login: "Your workspace is right where you left it.",
    forgot: "We’ll send you a secure password reset link.",
    reset: "Choose a strong password for your account.",
    verify: "Confirm your address to unlock your workspace.",
  }[mode];
  const session = useSession();
  useEffect(() => {
    if (session && mode === "login") navigate("/app");
  }, [session, mode]);
  return (
    <div className="auth-layout">
      <aside className="auth-story">
        <a href="/" aria-label="Tenlith home">
          <Mark />
        </a>
        <div>
          <span className="eyebrow">A SOLID START. AN OPEN FUTURE.</span>
          <h2>
            Less groundwork.
            <br />
            More <em>great work.</em>
          </h2>
          <p>
            All the essentials your SaaS needs, thoughtfully put together. Make
            it yours.
          </p>
          <div className="auth-proof">
            <ShieldCheck size={20} />
            <span>
              Secure accounts. Separate workspaces.
              <br />A foundation you can own.
            </span>
          </div>
        </div>
        <span className="micro">.NET + React · Open source · MIT licensed</span>
      </aside>
      <main className="auth-main">
        <div className="auth-top">
          <a href="/" className="text-link">
            <ArrowLeft size={15} /> Back to Tenlith
          </a>
          <ThemeButton />
        </div>
        <div className="auth-form">
          <span className="auth-symbol">
            <Layers3 size={28} />
          </span>
          <h1>{title}</h1>
          <p className="muted">{subtitle}</p>
          {message ? (
            <>
              <Notice>{message}</Notice>
              <a className="button primary" href="/login">
                Continue to sign in <ArrowUpRight size={16} />
              </a>
            </>
          ) : (
            <>
              {mode === "verify" ? (
                <ActionForm
                  submit="Verify email"
                  action={async () => {
                    if (!params.get("userId") || !params.get("token"))
                      throw new Error(
                        "This verification link is incomplete. Open the full link from your email.",
                      );
                    const result = await api<{ message: string }>(
                      "/auth/verify-email",
                      {
                        method: "POST",
                        body: {
                          userId: params.get("userId"),
                          token: params.get("token"),
                        },
                      },
                    );
                    setMessage(result.message);
                  }}
                >
                  <p className="muted">
                    Confirm the email address linked to this verification link.
                  </p>
                </ActionForm>
              ) : (
                <ActionForm
                  key={mode}
                  submit={
                    mode === "register"
                      ? "Create account"
                      : mode === "forgot"
                        ? "Send reset link"
                        : mode === "reset"
                          ? "Reset password"
                          : "Sign in"
                  }
                  action={async (data) => {
                    const email = String(data.get("email"));
                    const password = String(data.get("password"));
                    if (mode === "login") {
                      const result = await api<Session>("/auth/login", {
                        method: "POST",
                        body: { email, password },
                      });
                      setSession(result);
                      if (location.pathname !== "/invite") navigate("/app");
                    } else {
                      const body =
                        mode === "register"
                          ? { name: data.get("name"), email, password }
                          : mode === "forgot"
                            ? { email }
                            : { email, password, token: params.get("token") };
                      const result = await api<{ message: string }>(
                        `/auth/${mode === "register" ? "register" : mode === "forgot" ? "forgot-password" : "reset-password"}`,
                        { method: "POST", body },
                      );
                      setMessage(result.message);
                    }
                  }}
                >
                  {mode === "register" && (
                    <Field label="Full name">
                      <input
                        name="name"
                        autoComplete="name"
                        required
                        maxLength={100}
                        placeholder="Ada Lovelace"
                      />
                    </Field>
                  )}
                  <Field label="Email address">
                    <input
                      name="email"
                      type="email"
                      autoComplete="email"
                      defaultValue={params.get("email") ?? ""}
                      required
                      placeholder="you@company.com"
                    />
                  </Field>
                  {mode !== "forgot" && (
                    <Field
                      label="Password"
                      hint={
                        mode !== "login"
                          ? "At least 12 characters, with uppercase, lowercase, a number and a symbol."
                          : undefined
                      }
                    >
                      <input
                        name="password"
                        type="password"
                        autoComplete={
                          mode === "login" ? "current-password" : "new-password"
                        }
                        minLength={mode === "login" ? 1 : 12}
                        required
                        placeholder={
                          mode === "login"
                            ? "Your password"
                            : "Create a strong password"
                        }
                      />
                    </Field>
                  )}
                  {mode === "login" && (
                    <a className="text-link forgot" href="/forgot-password">
                      Forgot password?
                    </a>
                  )}
                </ActionForm>
              )}
              {(mode === "login" || mode === "register") && (
                <>
                  <div className="divider">
                    <span>or continue with</span>
                  </div>
                  {config.data?.githubEnabled ? (
                    <a
                      className="button secondary full"
                      href="/api/auth/github"
                    >
                      <Github size={17} /> GitHub
                    </a>
                  ) : (
                    <div className="provider-unavailable">
                      <Github size={17} /> GitHub sign-in is not configured
                    </div>
                  )}
                  <p className="auth-switch">
                    {mode === "login"
                      ? "New around here?"
                      : "Already have an account?"}{" "}
                    <a href={mode === "login" ? "/register" : "/login"}>
                      {mode === "login" ? "Create an account" : "Sign in"}
                    </a>
                  </p>
                  {config.data?.demoEnabled && (
                    <div className="demo-auth">
                      <span>Just looking around?</span>
                      <DemoButton className="secondary" />
                    </div>
                  )}
                </>
              )}
            </>
          )}
          <div className="auth-security">
            <Check size={14} /> Your data stays in your workspace.
          </div>
        </div>
        <div className="auth-foot">
          A small step for you. A solid start for your SaaS.
        </div>
      </main>
    </div>
  );
}
