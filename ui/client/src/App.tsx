import { lazy, Suspense } from 'react';
import { Routes, Route } from 'react-router-dom';
import Layout from './components/layout/Layout';
import ProtectedRoute from './components/auth/ProtectedRoute';

const Dashboard = lazy(() => import('./pages/Dashboard'));
const ActivityList = lazy(() => import('./pages/ActivityList'));
const ActivityDetail = lazy(() => import('./pages/ActivityDetail'));
const UploadPage = lazy(() => import('./pages/UploadPage'));
const Integrations = lazy(() => import('./pages/Integrations'));
const SettingsPage = lazy(() => import('./pages/SettingsPage'));
const RoutePlannerPage = lazy(() => import('./pages/RoutePlannerPage'));
const RoutesPage = lazy(() => import('./pages/RoutesPage'));
const EditorPage = lazy(() => import('./pages/EditorPage'));
const LoginPage = lazy(() => import('./pages/LoginPage'));
const RegisterPage = lazy(() => import('./pages/RegisterPage'));

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
          <Route path="/predict" element={<RoutePlannerPage />} />
          <Route path="/routes" element={<RoutesPage />} />
          <Route path="/integrations" element={<Integrations />} />
          <Route path="/settings" element={<SettingsPage />} />
        </Route>

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
