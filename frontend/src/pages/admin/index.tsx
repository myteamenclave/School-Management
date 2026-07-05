import { Routes, Route } from 'react-router-dom'
import { AcademicYearsPage } from './academic-years/AcademicYearsPage'
import { GradesPage } from './grades/GradesPage'
import { StudentsPage } from './students/StudentsPage'
import { TeachersPage } from './teachers/TeachersPage'

export function AdminRoutes() {
  return (
    <Routes>
      <Route path="academic-years" element={<AcademicYearsPage />} />
      <Route path="grades" element={<GradesPage />} />
      <Route path="students" element={<StudentsPage />} />
      <Route path="teachers" element={<TeachersPage />} />
    </Routes>
  )
}
