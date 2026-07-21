import api from './axios'

export interface ParentChild {
  studentId: string
  studentName: string
  studentCode: string
  enrollmentStatus: string
  currentGradeLabel: string | null
  currentSectionName: string | null
}

export interface ParentAcademicYear {
  id: string
  name: string
  isCurrent: boolean
}

// Same shape the admin/teacher student-grade view uses (spec 15 StudentGradeDto).
export interface StudentGrade {
  id: string
  subjectId: string
  subjectName: string
  semesterId: string
  semesterName: string
  midtermScore: number | null
  finalScore: number | null
  courseworkScore: number | null
  termScore: number | null
  letterGrade: string | null
  notes: string | null
}

// Reverse-chronological daily log row (spec 14 AttendanceHistoryEntryDto, unchanged).
export interface AttendanceHistoryEntry {
  id: string
  sectionId: string
  sectionName: string
  date: string // "yyyy-MM-dd"
  status: 'Present' | 'Late' | 'Absent' | 'Excused'
  notes: string | null
}

export interface StudentAttendanceSummary {
  totalMarked: number
  presentCount: number
  lateCount: number
  absentCount: number
  excusedCount: number
  attendanceRate: number | null // 0–100, or null when nothing is marked
}

export interface ParentAttendance {
  summary: StudentAttendanceSummary
  entries: AttendanceHistoryEntry[]
}

export interface FeeInvoiceLineItem {
  id: string
  name: string
  originalAmount: number
  discountAmount: number
  finalAmount: number
  displayOrder: number
}

export interface FeeInvoiceInstallment {
  id: string
  name: string
  percentage: number
  dueDate: string | null // "yyyy-MM-dd"
  amount: number
  status: string // stored status ("Pending"); overdue comes from the summary
  displayOrder: number
}

// Same shape the admin FeeInvoiceDto uses (spec 13), read-only for the parent.
export interface FeeInvoice {
  id: string
  invoiceCode: string
  studentId: string
  studentName: string
  studentCode: string
  academicYearId: string
  academicYearName: string
  feeTemplateId: string
  templateName: string
  totalAmount: number
  status: string
  issuedAt: string | null
  cancelledAt: string | null
  createdAt: string
  updatedAt: string | null
  lineItems: FeeInvoiceLineItem[]
  installments: FeeInvoiceInstallment[]
}

// Server-computed balance rollup. Overdue is a set of installment ids so the UI
// never recomputes the money rule (spec 20).
export interface StudentFeeSummary {
  hasInvoice: boolean
  totalBilled: number
  totalPaid: number
  outstanding: number
  nextDueDate: string | null
  nextDueAmount: number | null
  overdueAmount: number
  overdueCount: number
  overdueInstallmentIds: string[]
}

export interface StudentFeeOverview {
  summary: StudentFeeSummary
  invoice: FeeInvoice | null
}

export const PARENT_KEYS = {
  children: () => ['parent', 'children'] as const,
  academicYears: () => ['parent', 'academic-years'] as const,
  childGrades: (childId: string, academicYearId: string) =>
    ['parent', 'grades', childId, academicYearId] as const,
  childAttendance: (childId: string, academicYearId: string) =>
    ['parent', 'attendance', childId, academicYearId] as const,
  childFees: (childId: string, academicYearId: string) =>
    ['parent', 'fees', childId, academicYearId] as const,
}

export const parentPortalApi = {
  getChildren: () =>
    api.get<ParentChild[]>('/parent/children').then((r) => r.data),

  getAcademicYears: () =>
    api.get<ParentAcademicYear[]>('/parent/academic-years').then((r) => r.data),

  getChildGrades: (childId: string, academicYearId?: string) =>
    api
      .get<StudentGrade[]>(`/parent/children/${childId}/grades`, {
        params: academicYearId ? { academicYearId } : undefined,
      })
      .then((r) => r.data),

  getChildAttendance: (childId: string, academicYearId?: string) =>
    api
      .get<ParentAttendance>(`/parent/children/${childId}/attendance`, {
        params: academicYearId ? { academicYearId } : undefined,
      })
      .then((r) => r.data),

  getChildFees: (childId: string, academicYearId?: string) =>
    api
      .get<StudentFeeOverview>(`/parent/children/${childId}/fees`, {
        params: academicYearId ? { academicYearId } : undefined,
      })
      .then((r) => r.data),
}
