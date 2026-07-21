import { Routes, Route, Navigate } from 'react-router-dom'
import { ChildGradesPage } from './grades/ChildGradesPage'
import { ChildAttendancePage } from './attendance/ChildAttendancePage'

export function ParentRoutes() {
  return (
    <Routes>
      <Route path="grades" element={<ChildGradesPage />} />
      <Route path="attendance" element={<ChildAttendancePage />} />
      <Route path="*" element={<Navigate to="grades" replace />} />
    </Routes>
  )
}
