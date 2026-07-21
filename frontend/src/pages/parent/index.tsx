import { Routes, Route, Navigate } from 'react-router-dom'
import { ChildGradesPage } from './grades/ChildGradesPage'
import { ChildAttendancePage } from './attendance/ChildAttendancePage'
import { ChildFeesPage } from './fees/ChildFeesPage'

export function ParentRoutes() {
  return (
    <Routes>
      <Route path="grades" element={<ChildGradesPage />} />
      <Route path="attendance" element={<ChildAttendancePage />} />
      <Route path="fees" element={<ChildFeesPage />} />
      <Route path="*" element={<Navigate to="grades" replace />} />
    </Routes>
  )
}
