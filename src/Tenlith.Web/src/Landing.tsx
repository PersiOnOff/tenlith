import {
  ArrowDown,
  ArrowRight,
  ArrowUpRight,
  Check,
  Code2,
  CreditCard,
  Layers3,
  LockKeyhole,
  MoveUpRight,
  Users,
  Workflow,
  Zap,
} from "lucide-react";
import { DemoButton, useAuthConfig, useSession } from "./Auth";
import { Mark, ThemeButton } from "./ui";
export function Landing() {
  const config = useAuthConfig();
  const session = useSession();
  return (
    <div className="landing">
      <nav className="landing-nav">
        <a href="/" aria-label="Tenlith home">
          <Mark />
        </a>
        <div className="landing-nav-links">
          <a href="#foundation">The foundation</a>
          <a href="#stack">The stack</a>
          <a href="#start">
            Get started <ArrowUpRight size={13} />
          </a>
        </div>
        <div className="nav-actions">
          <ThemeButton />
          <a href={session ? "/app" : "/login"} className="button secondary">
            {session ? "Open workspace" : "Sign in"} <ArrowRight size={15} />
          </a>
        </div>
      </nav>
      <main>
        <section className="hero">
          <div className="hero-copy">
            <div className="release-label">
              <span /> OPEN SOURCE. YOURS TO BUILD ON.
            </div>
            <h1>
              Your SaaS.
              <br />
              Solid from
              <br />
              <span>day one.</span>
              <svg viewBox="0 0 155 15" aria-hidden="true">
                <path d="M3 11Q72 0 150 7" />
              </svg>
            </h1>
            <p>
              The essentials, already together. A thoughtfully built .NET +
              React starter that gets you from first commit to your next big
              thing.
            </p>
            <div className="hero-actions">
              {config.data?.demoEnabled ? (
                <DemoButton />
              ) : (
                <a href="/register" className="button primary">
                  Start your workspace <ArrowUpRight size={17} />
                </a>
              )}
              <a href="#foundation" className="button plain">
                See what’s inside <ArrowDown size={16} />
              </a>
            </div>
            <div className="hero-notes">
              <span>
                <Check size={14} /> Free & open source
              </span>
              <span>
                <Check size={14} /> No vendor lock-in
              </span>
              <span>
                <Check size={14} /> MIT licensed
              </span>
            </div>
          </div>
          <div
            className="foundation-art"
            aria-label="Four connected foundations: identity, workspaces, billing, and your product"
          >
            <div className="art-grid" />
            <span className="art-note top">LESS SETUP. MORE MOMENTUM.</span>
            <div className="art-orbit">
              <div className="art-core">
                <Mark small />
                <span>THE SOLID START</span>
              </div>
              <div className="art-node node-one">
                <LockKeyhole />
                <div>
                  <strong>Identity</strong>
                  <small>Secure from the start</small>
                </div>
                <span className="node-dot" />
              </div>
              <div className="art-node node-two">
                <Users />
                <div>
                  <strong>Workspaces</strong>
                  <small>Built for your team</small>
                </div>
                <span className="node-dot" />
              </div>
              <div className="art-node node-three">
                <CreditCard />
                <div>
                  <strong>Billing</strong>
                  <small>Ready for your plans</small>
                </div>
                <span className="node-dot" />
              </div>
              <div className="art-node node-four">
                <Zap />
                <div>
                  <strong>Your product</strong>
                  <small>The part only you can build</small>
                </div>
                <MoveUpRight size={18} />
              </div>
            </div>
            <div className="art-footer">
              <span className="mono">01 — FOUNDATION FIRST</span>
              <span className="art-coordinate">↗</span>
            </div>
          </div>
        </section>
        <div className="stack-strip" id="stack">
          <span>
            A FAMILIAR STACK.
            <br />
            <strong>A better starting point.</strong>
          </span>
          <div>
            <b>.NET</b>
            <small>ASP.NET Core</small>
          </div>
          <div>
            <b>React</b>
            <small>TypeScript</small>
          </div>
          <div>
            <b>PostgreSQL</b>
            <small>Your data, your database</small>
          </div>
          <div>
            <b>Stripe</b>
            <small>Checkout + portal</small>
          </div>
        </div>
        <section className="foundation-section" id="foundation">
          <div className="section-lead">
            <span className="eyebrow">THE BORING PARTS, THOUGHT THROUGH.</span>
            <h2>
              Skip the groundwork.
              <br />
              Keep the control.
            </h2>
            <p>
              A starter should give you momentum, not a new framework to learn.
              Start with real features, then make them your own.
            </p>
          </div>
          <div className="feature-grid">
            {[
              {
                n: "01",
                icon: <LockKeyhole />,
                title: "A proper front door.",
                text: "Email verification, password recovery and GitHub sign-in. Secure sessions without browser-stored tokens.",
              },
              {
                n: "02",
                icon: <Users />,
                title: "Room for everyone.",
                text: "Separate organizations, invitations and clear roles. Give every team a space of their own.",
              },
              {
                n: "03",
                icon: <CreditCard />,
                title: "Plans with a purpose.",
                text: "Free and Pro entitlements, Stripe checkout and a local simulation to try the whole journey.",
              },
              {
                n: "04",
                icon: <Layers3 />,
                title: "A real place to build.",
                text: "Project CRUD, audit activity and an accessible dashboard. A working example you can replace with your product.",
              },
            ].map((f) => (
              <article key={f.n}>
                <div>
                  <span className="mono">{f.n}</span>
                  {f.icon}
                </div>
                <h3>{f.title}</h3>
                <p>{f.text}</p>
              </article>
            ))}
          </div>
        </section>
        <section className="start-section" id="start">
          <div>
            <span className="eyebrow">YOUR NEXT THING STARTS HERE.</span>
            <h2>
              Make the first step
              <br />a solid one.
            </h2>
            <p>
              Explore an isolated demo workspace, or create your own account.
            </p>
          </div>
          <div className="start-actions">
            {config.data?.demoEnabled && <DemoButton />}
            <a href="/register" className="button secondary">
              Create an account <ArrowRight size={17} />
            </a>
            <span>
              <Code2 size={15} /> .NET + React. Built to be yours.
            </span>
          </div>
        </section>
      </main>
      <footer className="landing-footer">
        <a href="/" aria-label="Tenlith home">
          <Mark small />
        </a>
        <p>Your SaaS. Solid from day one.</p>
        <span>
          Open source, open possibilities. <Workflow size={16} />
        </span>
      </footer>
    </div>
  );
}
