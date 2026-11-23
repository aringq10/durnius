import { Link, NavLink, Outlet } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";
import GoldDisplay from "./GoldDisplay";

/** App shell for authenticated pages: top bar, navigation and user menu. */
export default function Layout() {
  const { username, logout } = useAuth();

  return (
    <div className="shell">
      <header className="topbar">
        <div className="topbar-inner">
          <Link to="/lobby" className="brand" aria-label="Durnius — lobby">
            <span className="brand-suit" aria-hidden="true">
              ♠
            </span>
            <span className="brand-name">Durnius</span>
          </Link>
          <nav className="topnav" aria-label="Main">
            <NavLink to="/lobby">Lobby</NavLink>
            <NavLink to="/account">Account</NavLink>
          </nav>
          <div className="topbar-user">
            <GoldDisplay />
            {username && (
              <span className="user-chip">
                <span className="avatar" aria-hidden="true">
                  {username.charAt(0).toUpperCase()}
                </span>
                <span className="user-name">{username}</span>
              </span>
            )}
            <button
              type="button"
              className="btn btn-ghost btn-sm"
              onClick={logout}
            >
              Log out
            </button>
          </div>
        </div>
      </header>
      <main className="page">
        <Outlet />
      </main>
    </div>
  );
}
