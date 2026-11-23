import { Navigate, Route, Routes } from "react-router-dom";
import type { ReactNode } from "react";
import { useAuth } from "./auth/AuthContext";
import { isSessionExpired } from "./lib/auth";
import Layout from "./components/Layout";
import Login from "./pages/Login";
import Register from "./pages/Register";
import Lobby from "./pages/Lobby";
import Karas from "./pages/Karas";
import Durnius from "./pages/Durnius";
import Account from "./pages/Account";

function Home() {
  const { isAuthenticated } = useAuth();
  return <Navigate to={isAuthenticated ? "/lobby" : "/login"} replace />;
}

function RequireAuth({ children }: { children: ReactNode }) {
  const { isAuthenticated } = useAuth();
  if (!isAuthenticated) {
    return (
      <Navigate
        to="/login"
        replace
        state={{ expired: isSessionExpired() }}
      />
    );
  }
  return children;
}

export default function App() {
  return (
    <Routes>
      <Route path="/" element={<Home />} />
      <Route path="/login" element={<Login />} />
      <Route path="/register" element={<Register />} />
      <Route
        element={
          <RequireAuth>
            <Layout />
          </RequireAuth>
        }
      >
        <Route path="/lobby" element={<Lobby />} />
        <Route path="/karas" element={<Karas />} />
        <Route path="/durnius" element={<Durnius />} />
        <Route path="/account" element={<Account />} />
      </Route>
      {/* The leaderboard now lives on the lobby page. */}
      <Route path="/leaderboard" element={<Navigate to="/lobby" replace />} />
      <Route path="*" element={<Home />} />
    </Routes>
  );
}
