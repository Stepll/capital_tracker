import { BrowserRouter, Routes, Route } from "react-router-dom";
import { AuthProvider } from "./shared/auth/AuthContext";
import { ProtectedRoute } from "./shared/auth/ProtectedRoute";
import { AppLayout } from "./features/navigation/AppLayout";
import { LoginPage } from "./features/auth/LoginPage";
import { DashboardPage } from "./features/dashboard/DashboardPage";
import { SettingsPage } from "./features/settings/SettingsPage";
import { AccountDetailPage } from "./features/accounts/AccountDetailPage";
import { HoldingDetailPage } from "./features/holdings/HoldingDetailPage";
import { InsightsPage } from "./features/insights/InsightsPage";

function App() {
  return (
    <AuthProvider>
      <BrowserRouter>
        <Routes>
          <Route path="/login" element={<LoginPage />} />

          {/* One guard and one chrome for everything behind the login: the pinned bar
              carries the breadcrumbs, the back arrow and the links out, so no page
              renders its own. Login stays outside — it has nowhere to navigate to. */}
          <Route
            element={
              <ProtectedRoute>
                <AppLayout />
              </ProtectedRoute>
            }
          >
            <Route path="/" element={<DashboardPage />} />
            <Route path="/settings" element={<SettingsPage />} />
            <Route path="/accounts/:id" element={<AccountDetailPage />} />
            <Route path="/holdings/:id" element={<HoldingDetailPage />} />
            <Route path="/insights" element={<InsightsPage />} />
          </Route>
        </Routes>
      </BrowserRouter>
    </AuthProvider>
  );
}

export default App
