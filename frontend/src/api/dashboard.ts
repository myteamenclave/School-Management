import api from './axios'

export interface FinanceSummaryDto {
  billed: number
  collected: number
  outstanding: number
  overdue: number
  collectionRate: number // 0..1
  issuedInvoiceCount: number
  draftInvoiceCount: number
}

export interface MonthlyMoneyPointDto {
  year: number
  month: number
  billed: number
  collected: number
}

export interface MonthlyAttendancePointDto {
  year: number
  month: number
  totalRecords: number
  presentCount: number
  presentRate: number // 0..1
}

export interface GradeCountDto {
  gradeId: string
  gradeName: string
  count: number
}

export interface StatusCountDto {
  status: string
  count: number
}

export interface EnrollmentBreakdownDto {
  totalEnrolled: number
  byGrade: GradeCountDto[]
  byStatus: StatusCountDto[]
}

export interface TeacherCoverageDto {
  teacherCount: number
  assignmentCount: number
  sectionsWithEnrollments: number
  sectionsWithoutAnyTeacher: number
  teachersWithoutAssignment: number
}

export interface DashboardOverviewDto {
  academicYearId: string
  academicYearName: string
  finance: FinanceSummaryDto
  financeMonthly: MonthlyMoneyPointDto[]
  attendanceMonthly: MonthlyAttendancePointDto[]
  enrollment: EnrollmentBreakdownDto
  teachers: TeacherCoverageDto
}

export const DASHBOARD_KEYS = {
  overview: (academicYearId: string | null) => ['dashboard', 'overview', academicYearId] as const,
}

export const dashboardApi = {
  overview: (academicYearId?: string): Promise<DashboardOverviewDto> =>
    api
      .get<DashboardOverviewDto>('/dashboard/overview', {
        params: academicYearId ? { academicYearId } : undefined,
      })
      .then((r) => r.data),
}
