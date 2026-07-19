import { Users, ClipboardList, AlertTriangle } from 'lucide-react'
import type { TeacherCoverageDto } from '../../../api/dashboard'

function Stat({ label, value }: { label: string; value: number }) {
  return (
    <div>
      <p className="font-heading text-2xl font-semibold text-foreground">{value}</p>
      <p className="text-xs text-muted-foreground">{label}</p>
    </div>
  )
}

function Gap({ label, value }: { label: string; value: number }) {
  const active = value > 0
  return (
    <div
      className={`flex items-center gap-2 rounded-lg border px-3 py-2 text-sm ${
        active ? 'border-amber-200 bg-amber-50 text-amber-800' : 'border-border bg-muted text-muted-foreground'
      }`}
    >
      <AlertTriangle size={15} className={active ? '' : 'opacity-40'} />
      <span>
        <strong>{value}</strong> {label}
      </span>
    </div>
  )
}

export function TeacherCoverage({ teachers }: { teachers: TeacherCoverageDto }) {
  return (
    <div className="space-y-4">
      <div className="grid grid-cols-3 gap-4">
        <div className="flex items-start gap-2">
          <Users size={16} className="mt-1 text-muted-foreground" />
          <Stat label="active teachers" value={teachers.teacherCount} />
        </div>
        <div className="flex items-start gap-2">
          <ClipboardList size={16} className="mt-1 text-muted-foreground" />
          <Stat label="assignments" value={teachers.assignmentCount} />
        </div>
        <Stat label="sections with students" value={teachers.sectionsWithEnrollments} />
      </div>

      <div className="space-y-2">
        <Gap label="section(s) with students but no teacher" value={teachers.sectionsWithoutAnyTeacher} />
        <Gap label="teacher(s) with no assignment this year" value={teachers.teachersWithoutAssignment} />
      </div>
    </div>
  )
}
