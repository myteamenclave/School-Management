import { Routes, Route } from 'react-router-dom'
import { AcademicYearsPage } from './academic-years/AcademicYearsPage'
import { GradesPage } from './grades/GradesPage'
import { StudentsPage } from './students/StudentsPage'

export function AdminRoutes() {
  return (
    <Routes>
      <Route path="academic-years" element={<AcademicYearsPage />} />
      <Route path="grades" element={<GradesPage />} />
      <Route path="students" element={<StudentsPage />} />
    </Routes>
  )
}
