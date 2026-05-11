import { lazy, Suspense } from 'react';
import { Routes, Route } from 'react-router-dom';
import Layout from './components/layout/Layout';
import ProtectedRoute from './components/auth/ProtectedRoute';

const Dashboard = lazy(() => import('./pages/Dashboard'));
const ActivityList = lazy(() => import('./pages/ActivityList'));
const ActivityDetail = lazy(() => import('./pages/ActivityDetail'));
const UploadPage = lazy(() => import('./pages/UploadPage'));
const SettingsPage = lazy(() => import('./pages/SettingsPage'));
const SystemSettingsPage = lazy(() => import('./pages/SystemSettingsPage'));
const RoutesPage = lazy(() => import('./pages/RoutesPage'));
const EditorPage = lazy(() => import('./pages/EditorPage'));
const ProfilePage = lazy(() => import('./pages/ProfilePage'));
const LoginPage = lazy(() => import('./pages/LoginPage'));
const RegisterPage = lazy(() => import('./pages/RegisterPage'));
const RacePlansPage = lazy(() => import('./pages/RacePlansPage'));
const RacePlanDetailPage = lazy(() => import('./pages/RacePlanDetailPage'));
const RacePlanPrintPage = lazy(() => import('./pages/RacePlanPrintPage'));
const NutritionCataloguePage = lazy(() => import('./pages/NutritionCataloguePage'));
const SharedRacePlanPage = lazy(() => import('./pages/SharedRacePlanPage'));

function PageLoader() {
  return (
    <div className="flex items-center justify-center h-64">
      <div className="w-8 h-8 border-2 border-accent/30 border-t-accent rounded-full animate-spin" />
    </div>
  );
}

export default function App() {
  return (
    <Suspense fallback={<PageLoader />}>
      <Routes>
        {/* Public routes */}
        <Route path="/login" element={<LoginPage />} />
        <Route path="/register" element={<RegisterPage />} />
        <Route path="/share/race-plan/:token" element={<SharedRacePlanPage />} />

        {/* Protected routes */}
        <Route
          element={
            <ProtectedRoute>
              <Layout />
            </ProtectedRoute>
          }
        >
          <Route path="/" element={<Dashboard />} />
          <Route path="/activities" element={<ActivityList />} />
          <Route path="/activities/:id" element={<ActivityDetail />} />
          <Route path="/upload" element={<UploadPage />} />
          <Route path="/routes" element={<RoutesPage />} />
          <Route path="/race-plans" element={<RacePlansPage />} />
          <Route path="/race-plans/nutrition" element={<NutritionCataloguePage />} />
          <Route path="/race-plans/:id" element={<RacePlanDetailPage />} />
          <Route path="/settings" element={<SettingsPage />} />
          <Route path="/system-settings" element={<SystemSettingsPage />} />
          <Route path="/profile" element={<ProfilePage />} />
        </Route>

        {/* Print page — full-screen, no Layout wrapper, but still protected */}
        <Route
          path="/race-plans/:id/print"
          element={
            <ProtectedRoute>
              <RacePlanPrintPage />
            </ProtectedRoute>
          }
        />

        {/* Editor pages — full-screen, no Layout wrapper, but still protected */}
        <Route
          path="/editor"
          element={
            <ProtectedRoute>
              <EditorPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="/editor/:id"
          element={
            <ProtectedRoute>
              <EditorPage />
            </ProtectedRoute>
          }
        />
      </Routes>
    </Suspense>
  );
}
