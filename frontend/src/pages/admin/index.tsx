import { Routes, Route } from 'react-router-dom'
import { AcademicYearsPage } from './academic-years/AcademicYearsPage'
import { GradesPage } from './grades/GradesPage'
import { StudentsPage } from './students/StudentsPage'
import { TeachersPage } from './teachers/TeachersPage'
import { TeacherDetailPage } from './teachers/TeacherDetailPage'
import { SubjectsPage } from './subjects/SubjectsPage'
import { FeeTemplatesPage } from './fee-templates/FeeTemplatesPage'
import { FeeTemplatePage } from './fee-templates/FeeTemplatePage'

export function AdminRoutes() {
  return (
    <Routes>
      <Route path="academic-years" element={<AcademicYearsPage />} />
      <Route path="grades" element={<GradesPage />} />
      <Route path="students" element={<StudentsPage />} />
      <Route path="teachers" element={<TeachersPage />} />
      <Route path="teachers/:id" element={<TeacherDetailPage />} />
      <Route path="subjects" element={<SubjectsPage />} />
      <Route path="fee-templates" element={<FeeTemplatesPage />} />
      <Route path="fee-templates/:id" element={<FeeTemplatePage />} />
    </Routes>
  )
}
