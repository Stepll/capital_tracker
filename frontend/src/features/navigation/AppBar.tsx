import { Link, useMatch } from "react-router-dom";
import { useAccountDetail } from "../accounts/useAccountDetail";
import { useHoldingDetail } from "../holdings/useHoldingDetail";
import { useAuth } from "../../shared/auth/AuthContext";
import styles from "./AppBar.module.css";

interface Crumb {
  label: string;
  /** Absent on the last crumb — you are already there. */
  to?: string;
}

/**
 * The one place the app's chrome lives: where you are, how to go up, and the links out.
 * Pinned to the top of every page inside the layout route, so the back control never
 * moves between pages the way per-page links did.
 *
 * The trail is derived from the route rather than published by each page — the names it
 * needs are already in the query cache by the time the page has rendered them, so asking
 * for them here costs nothing beyond a cache read (same keys, deduped by TanStack Query).
 */
export function AppBar() {
  const accountMatch = useMatch("/accounts/:id");
  const holdingMatch = useMatch("/holdings/:id");
  const insightsMatch = useMatch("/insights");
  const settingsMatch = useMatch("/settings");
  const { logout } = useAuth();

  // Both hooks run on every page; each is disabled unless its route matched.
  const { data: account } = useAccountDetail(accountMatch?.params.id);
  const { data: holding } = useHoldingDetail(holdingMatch?.params.id);

  const trail: Crumb[] = [{ label: "Дашборд", to: "/" }];

  if (accountMatch) {
    trail.push({ label: account?.name ?? "Рахунок" });
  }

  if (holdingMatch) {
    // Falls back to a generic label while the holding is still loading, so the bar keeps
    // its height and the crumbs don't jump into place a moment later.
    trail.push({
      label: holding?.accountName ?? "Рахунок",
      to: holding ? `/accounts/${holding.accountId}` : undefined,
    });
    trail.push({ label: holding?.name ?? "Актив" });
  }

  if (insightsMatch) trail.push({ label: "AI-аналітика" });
  if (settingsMatch) trail.push({ label: "Налаштування" });

  // Up one level, not "the page you came from": a breadcrumb parent is where the arrow
  // visibly points, and browser history can hold anything at all.
  const parent = [...trail].reverse().find((crumb) => crumb.to)?.to;
  const isRoot = trail.length === 1;

  return (
    <header className={styles.bar}>
      <div className={styles.inner}>
        {isRoot ? (
          <span className={styles.backPlaceholder} aria-hidden="true" />
        ) : (
          <Link to={parent ?? "/"} className={styles.back} aria-label="На рівень вище">
            ←
          </Link>
        )}

        <nav className={styles.trail} aria-label="Шлях">
          {trail.map((crumb, index) => (
            <span key={`${crumb.label}-${index}`} className={styles.crumbWrap}>
              {index > 0 && <span className={styles.separator} aria-hidden="true">›</span>}
              {crumb.to ? (
                <Link to={crumb.to} className={styles.crumb}>
                  {crumb.label}
                </Link>
              ) : (
                <span className={styles.crumbCurrent} aria-current="page">
                  {crumb.label}
                </span>
              )}
            </span>
          ))}
        </nav>

        <div className={styles.actions}>
          <Link to="/insights" className={styles.action}>
            AI-аналітика
          </Link>
          <Link to="/settings" className={styles.action}>
            Налаштування
          </Link>
          <button className={styles.action} onClick={logout}>
            Вийти
          </button>
        </div>
      </div>
    </header>
  );
}
