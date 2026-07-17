import api from './axios'

export type AttendanceStatus = 'Present' | 'Late' | 'Absent' | 'Excused'
export const ATTENDANCE_STATUSES: AttendanceStatus[] = ['Present', 'Late', 'Absent', 'Excused']

export interface AttendanceRosterEntry {
  studentId: string
  studentName: string
  studentCode: string
  status: AttendanceStatus | null
  notes: string | null
}

export interface SectionAttendanceRoster {
  sectionId: string
  sectionName: string
  date: string
  entries: AttendanceRosterEntry[]
}

export interface AttendanceEntryRequest {
  studentId: string
  status: AttendanceStatus
  notes?: string | null
}

export interface BulkUpsertAttendanceRequest {
  sectionId: string
  academicYearId: string
  date: string
  entries: AttendanceEntryRequest[]
}

export interface AttendanceHistoryEntry {
  id: string
  sectionId: string
  sectionName: string
  date: string
  status: AttendanceStatus
  notes: string | null
}

export const ATTENDANCE_KEYS = {
  sectionRoster: (sectionId: string, date: string, academicYearId: string) =>
    ['attendance', 'section-roster', sectionId, date, academicYearId] as const,
  studentHistory: (studentId: string, academicYearId: string) =>
    ['attendance', 'student-history', studentId, academicYearId] as const,
}

export const attendanceApi = {
  getSectionRoster: (sectionId: string, date: string, academicYearId: string) =>
    api
      .get<SectionAttendanceRoster>('/attendance/section-roster', {
        params: { sectionId, date, academicYearId },
      })
      .then((r) => r.data),

  bulkUpsert: (request: BulkUpsertAttendanceRequest) =>
    api.put<{ upserted: number }>('/attendance/bulk', request).then((r) => r.data),

  getStudentHistory: (studentId: string, academicYearId: string) =>
    api
      .get<AttendanceHistoryEntry[]>('/attendance/student-history', {
        params: { studentId, academicYearId },
      })
      .then((r) => r.data),
}
