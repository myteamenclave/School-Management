import { Routes, Route, Navigate } from 'react-router-dom'
import { ChildGradesPage } from './grades/ChildGradesPage'

export function ParentRoutes() {
  return (
    <Routes>
      <Route path="grades" element={<ChildGradesPage />} />
      <Route path="*" element={<Navigate to="grades" replace />} />
    </Routes>
  )
}
