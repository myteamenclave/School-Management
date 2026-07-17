import { Routes, Route } from 'react-router-dom'
import { AttendancePage } from './attendance/AttendancePage'

export function TeacherRoutes() {
  return (
    <Routes>
      <Route path="attendance" element={<AttendancePage />} />
    </Routes>
  )
}
