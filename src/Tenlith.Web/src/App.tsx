import { useEffect, useState } from "react";
import { refreshSession, getSession } from "./api";
import { Auth, useSession } from "./Auth";
import { Landing } from "./Landing";
import { Workspace } from "./Workspace";
import { Loading } from "./ui";
import { usePath } from "./navigation";
export function App() {
  const path = usePath();
  const session = useSession();
  const protectedPath = path.startsWith("/app") || path === "/invite";
  const [restoring, setRestoring] = useState(protectedPath && !getSession());
  useEffect(() => {
    if (protectedPath && !getSession()) {
      setRestoring(true);
      refreshSession()
        .catch(() => {})
        .finally(() => setRestoring(false));
    }
  }, [protectedPath]);
  if (path === "/") return <Landing />;
  if (protectedPath) {
    if (restoring) return <Loading label="Restoring your secure session…" />;
    return session ? <Workspace path={path} /> : <Auth path="/login" />;
  }
  if (
    [
      "/login",
      "/register",
      "/forgot-password",
      "/reset-password",
      "/verify-email",
    ].includes(path)
  )
    return <Auth key={path} path={path} />;
  return (
    <main className="not-found">
      <h1>That page took a wrong turn.</h1>
      <p>Head back to a solid starting point.</p>
      <a href="/" className="button primary">
        Back home
      </a>
    </main>
  );
}
