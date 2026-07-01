import { createBrowserRouter, Navigate } from 'react-router-dom'
import { ProtectedRoute } from './guards/ProtectedRoute'
import { PublicOnlyRoute } from './guards/PublicOnlyRoute'
import { RoleRoute } from './guards/RoleRoute'
import { AppShell } from '../layouts/AppShell'
import { LoginPage } from '../pages/auth/LoginPage'
import { DashboardPage } from '../pages/dashboard/DashboardPage'
import { AdminRoutes } from '../pages/admin'
import { TeacherRoutes } from '../pages/teacher'
import { ParentRoutes } from '../pages/parent'

export const router = createBrowserRouter([
  {
    path: '/login',
    element: (
      <PublicOnlyRoute>
        <LoginPage />
      </PublicOnlyRoute>
    ),
  },
  { path: '/', element: <Navigate to="/dashboard" replace /> },
  {
    element: <ProtectedRoute />,
    children: [
      {
        element: <AppShell />,
        children: [
          { path: '/dashboard', element: <DashboardPage /> },
          {
            path: '/admin/*',
            element: (
              <RoleRoute role="Admin">
                <AdminRoutes />
              </RoleRoute>
            ),
          },
          {
            path: '/teacher/*',
            element: (
              <RoleRoute role="Teacher">
                <TeacherRoutes />
              </RoleRoute>
            ),
          },
          {
            path: '/parent/*',
            element: (
              <RoleRoute role="Parent">
                <ParentRoutes />
              </RoleRoute>
            ),
          },
        ],
      },
    ],
  },
])
