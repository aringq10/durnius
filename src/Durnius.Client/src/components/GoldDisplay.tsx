import { useEffect, useState } from "react";
import { useAuth } from "../auth/AuthContext";
import { getCurrentAccount } from "../lib/api";

/** How often the balance is re-fetched while the player is signed in. */
const REFRESH_MS = 60_000;

/**
 * The signed-in player's gold balance: a compact chip for the top bar,
 * or a larger stat for the account profile (`large`).
 */
export default function GoldDisplay({ large = false }: { large?: boolean }) {
  const { token } = useAuth();
  const [gold, setGold] = useState<number | null>(null);

  useEffect(() => {
    if (!token) return;
    let active = true;

    const load = () => {
      getCurrentAccount()
        .then((account) => {
          if (active) setGold(account.gold);
        })
        .catch(() => {
          if (active) setGold(null);
        });
    };

    load();
    const timer = window.setInterval(load, REFRESH_MS);

    return () => {
      active = false;
      window.clearInterval(timer);
    };
  }, [token]);

  return (
    <span
      className={large ? "gold-chip gold-chip-lg" : "gold-chip"}
      title="Your gold"
    >
      <span className="gold-coin" aria-hidden="true" />
      <span className="gold-amount">
        {gold === null ? "…" : gold.toLocaleString()}
      </span>
      {large && <span className="gold-label">gold</span>}
    </span>
  );
}