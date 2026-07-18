import { Routes, Route } from 'react-router-dom'
import { AttendancePage } from './attendance/AttendancePage'
import { GradebookPage } from './gradebook/GradebookPage'

export function TeacherRoutes() {
  return (
    <Routes>
      <Route path="attendance" element={<AttendancePage />} />
      <Route path="gradebook" element={<GradebookPage />} />
    </Routes>
  )
}
