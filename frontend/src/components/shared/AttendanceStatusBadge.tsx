import type { AttendanceHistoryEntry } from '../../api/parentPortal'

type AttendanceStatus = AttendanceHistoryEntry['status']

// Shared status → colour map. Same values already used inline by the admin
// (AttendanceViewPage) and teacher (AttendancePage) attendance tables; centralised
// here so the parent portal stays consistent without a fourth copy.
export const ATTENDANCE_STATUS_COLORS: Record<AttendanceStatus, string> = {
  Present: 'bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-400',
  Late: 'bg-amber-100 text-amber-700 dark:bg-amber-900/30 dark:text-amber-400',
  Absent: 'bg-red-100 text-red-700 dark:bg-red-900/30 dark:text-red-400',
  Excused: 'bg-blue-100 text-blue-700 dark:bg-blue-900/30 dark:text-blue-400',
}

// Text-only variant for numeric summaries (coloured counts without a chip background).
export const ATTENDANCE_STATUS_TEXT_COLORS: Record<AttendanceStatus, string> = {
  Present: 'text-green-700 dark:text-green-400',
  Late: 'text-amber-700 dark:text-amber-400',
  Absent: 'text-red-700 dark:text-red-400',
  Excused: 'text-blue-700 dark:text-blue-400',
}

export function AttendanceStatusBadge({ status }: { status: AttendanceStatus }) {
  return (
    <span
      className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${ATTENDANCE_STATUS_COLORS[status]}`}
    >
      {status}
    </span>
  )
}
