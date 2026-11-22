import { Navigate, Route, Routes } from "react-router-dom";
import type { ReactNode } from "react";
import { useAuth } from "./auth/AuthContext";
import Login from "./pages/Login";
import Register from "./pages/Register";
import Lobby from "./pages/Lobby";
import Leaderboard from "./pages/Leaderboard";
import Karas from "./pages/Karas";
import Durnius from "./pages/Durnius";

function Home() {
  const { isAuthenticated } = useAuth();
  return <Navigate to={isAuthenticated ? "/lobby" : "/login"} replace />;
}

function RequireAuth({ children }: { children: ReactNode }) {
  const { isAuthenticated } = useAuth();
  if (!isAuthenticated) {
    return <Navigate to="/login" replace />;
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
        path="/lobby"
        element={
          <RequireAuth>
            <Lobby />
          </RequireAuth>
        }
      />
      <Route path="/leaderboard" element={<Leaderboard />} />
      <Route
        path="/karas"
        element={
          <RequireAuth>
            <Karas />
          </RequireAuth>
        }
      />
      <Route
        path="/durnius"
        element={
          <RequireAuth>
            <Durnius />
          </RequireAuth>
        }
      />
      <Route path="*" element={<Home />} />
    </Routes>
  );
}
