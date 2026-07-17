import { Routes, Route } from 'react-router-dom'
import { AcademicYearsPage } from './academic-years/AcademicYearsPage'
import { GradesPage } from './grades/GradesPage'
import { StudentsPage } from './students/StudentsPage'
import { StudentDetailPage } from './students/StudentDetailPage'
import { TeachersPage } from './teachers/TeachersPage'
import { TeacherDetailPage } from './teachers/TeacherDetailPage'
import { SubjectsPage } from './subjects/SubjectsPage'
import { FeeTemplatesPage } from './fee-templates/FeeTemplatesPage'
import { FeeTemplatePage } from './fee-templates/FeeTemplatePage'
import { FeeInvoicesPage } from './fee-invoices/FeeInvoicesPage'
import { FeeInvoicePage } from './fee-invoices/FeeInvoicePage'
import { AttendanceViewPage } from './attendance/AttendanceViewPage'

export function AdminRoutes() {
  return (
    <Routes>
      <Route path="academic-years" element={<AcademicYearsPage />} />
      <Route path="grades" element={<GradesPage />} />
      <Route path="students" element={<StudentsPage />} />
      <Route path="students/:id" element={<StudentDetailPage />} />
      <Route path="teachers" element={<TeachersPage />} />
      <Route path="teachers/:id" element={<TeacherDetailPage />} />
      <Route path="subjects" element={<SubjectsPage />} />
      <Route path="fee-templates" element={<FeeTemplatesPage />} />
      <Route path="fee-templates/:id" element={<FeeTemplatePage />} />
      <Route path="fee-invoices" element={<FeeInvoicesPage />} />
      <Route path="fee-invoices/:id" element={<FeeInvoicePage />} />
      <Route path="attendance" element={<AttendanceViewPage />} />
    </Routes>
  )
}
