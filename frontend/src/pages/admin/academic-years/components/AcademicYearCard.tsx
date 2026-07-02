import { AlertTriangle, CalendarDays, Check, Lock, Pencil, Shield } from 'lucide-react'
import type { AcademicYearDto, SemesterDto } from '../../../../api/academicYears'
import { Button } from '../../../../components/ui/button'

interface AcademicYearCardProps {
  year: AcademicYearDto
  onSetCurrent: (id: string) => void
  onArchive: (id: string) => void
  onEditSemester: (semester: SemesterDto) => void
  onSetCurrentSemester: (yearId: string, semesterId: string) => void
}

function formatDate(dateStr: string): string {
  const [y, m, d] = dateStr.split('-')
  const date = new Date(Number(y), Number(m) - 1, Number(d))
  return date.toLocaleDateString('en-GB', { day: 'numeric', month: 'short', year: 'numeric' })
}

type SemesterSubLabel = 'Completed' | 'Upcoming' | 'InDateRange' | null

function semesterSubLabel(semester: SemesterDto): SemesterSubLabel {
  if (semester.isCurrent) return null
  const today = new Date().toISOString().slice(0, 10) // YYYY-MM-DD — compares correctly against ISO date strings
  if (semester.endDate < today) return 'Completed'
  if (semester.startDate > today) return 'Upcoming'
  return 'InDateRange' // today is within this semester's range but it isn't flagged as current
}

export function AcademicYearCard({
  year,
  onSetCurrent,
  onArchive,
  onEditSemester,
  onSetCurrentSemester,
}: AcademicYearCardProps) {
  const isArchived = year.status === 'Archived'

  const handleArchive = () => {
    if (window.confirm(`Archive "${year.name}"? This action cannot be undone.`)) {
      onArchive(year.id)
    }
  }

  return (
    <div
      className={`rounded-lg border bg-card shadow-sm overflow-hidden card-fade-in ${
        isArchived ? 'opacity-75' : ''
      } ${year.isCurrent ? 'border-l-4 border-l-primary' : ''}`}
    >
      {/* ── Card header ── */}
      <div
        className={`px-5 py-4 flex items-start justify-between gap-4 ${
          year.isCurrent ? 'bg-primary/5' : ''
        }`}
      >
        <div className="flex flex-col gap-1.5">
          {/* Name + badges row */}
          <div className="flex items-center gap-2 flex-wrap">
            <h3 className="font-heading font-bold text-lg text-foreground leading-tight">
              {year.name}
            </h3>

            {/* Status badge */}
            {isArchived ? (
              <span className="inline-flex items-center gap-1.5 rounded-full px-2.5 py-0.5 text-xs font-medium bg-muted text-muted-foreground">
                <span className="w-1.5 h-1.5 rounded-full bg-muted-foreground/50 flex-shrink-0" />
                Archived
              </span>
            ) : (
              <span className="inline-flex items-center gap-1.5 rounded-full px-2.5 py-0.5 text-xs font-medium bg-accent/10 text-accent">
                <span className="w-1.5 h-1.5 rounded-full bg-accent flex-shrink-0" />
                Active
              </span>
            )}

            {/* Current year indicator — inline text, no pill */}
            {year.isCurrent && (
              <span className="inline-flex items-center gap-1 text-xs font-medium text-secondary">
                <Check size={12} strokeWidth={2.5} />
                Current Year
              </span>
            )}
          </div>

          {/* Date range */}
          <div className="flex items-center gap-1.5 text-sm text-muted-foreground">
            <CalendarDays size={13} className="flex-shrink-0" />
            {formatDate(year.startDate)} – {formatDate(year.endDate)}
          </div>

          {/* Nudge: current year whose end date has passed */}
          {year.isCurrent && year.endDate < new Date().toISOString().slice(0, 10) && (
            <div className="flex items-center gap-1.5 text-xs font-medium text-amber-600">
              <AlertTriangle size={12} className="flex-shrink-0" />
              This year has ended — consider setting a new current year
            </div>
          )}
        </div>

        {/* Right-side actions */}
        <div className="flex items-center gap-2 flex-shrink-0 mt-0.5">
          {isArchived ? (
            <span className="text-xs text-muted-foreground italic">Archived — read only</span>
          ) : year.isCurrent ? (
            <span className="inline-flex items-center gap-1.5 rounded-md border border-border px-3 py-1.5 text-xs text-muted-foreground">
              <Shield size={11} />
              Protected — cannot archive
            </span>
          ) : (
            <>
              <Button size="sm" variant="outline" onClick={() => onSetCurrent(year.id)}>
                <Check size={13} className="mr-1.5" />
                Set as Current
              </Button>
              <Button
                size="sm"
                variant="outline"
                className="text-destructive hover:text-destructive"
                onClick={handleArchive}
              >
                <Lock size={13} className="mr-1.5" />
                Archive
              </Button>
            </>
          )}
        </div>
      </div>

      {/* ── Semesters ── */}
      {year.semesters.length > 0 && (
        <div className="border-t border-border">
          {/* Section label */}
          <div className="px-5 py-2.5">
            <span className="text-[10px] font-semibold tracking-widest uppercase text-muted-foreground/70">
              Semesters
            </span>
          </div>

          <div className="border-t border-border pl-4">
            {year.semesters.map((semester, idx) => {
              const subLabel = semesterSubLabel(semester)
              return (
                <div
                  key={semester.id}
                  className={`flex items-stretch gap-0 ${
                    idx < year.semesters.length - 1 ? 'border-b border-border' : ''
                  }`}
                >
                  {/* Accent bar — amber when in-range but not flagged current */}
                  <div
                    className="w-[3px] flex-shrink-0"
                    style={{
                      backgroundColor: semester.isCurrent
                        ? '#1E3A5F'
                        : subLabel === 'InDateRange'
                          ? '#D97706'
                          : '#E4E7EB',
                    }}
                  />

                  {/* Row content: name/status | date | actions */}
                  <div className="flex flex-1 items-center gap-4 px-4 py-3">
                    {/* Name + status */}
                    <div className="flex flex-col gap-0.5 min-w-[120px]">
                      <span className="text-sm font-semibold text-foreground">
                        {semester.name}
                      </span>
                      {semester.isCurrent ? (
                        <span className="inline-flex items-center gap-1 text-xs font-medium text-secondary">
                          <Check size={11} strokeWidth={2.5} />
                          Current
                        </span>
                      ) : subLabel === 'InDateRange' ? (
                        <span className="inline-flex items-center gap-1 text-xs font-medium text-amber-600">
                          <CalendarDays size={11} />
                          In date range
                        </span>
                      ) : subLabel ? (
                        <span className="text-xs text-muted-foreground">{subLabel}</span>
                      ) : null}
                    </div>

                    {/* Date range */}
                    <div className="flex items-center gap-1.5 text-xs text-muted-foreground flex-1">
                      <CalendarDays size={12} className="flex-shrink-0" />
                      {formatDate(semester.startDate)} – {formatDate(semester.endDate)}
                    </div>

                    {/* Actions */}
                    {!isArchived && (
                      <div className="flex items-center gap-2 flex-shrink-0">
                        <Button
                          size="sm"
                          variant="outline"
                          className="h-8 text-xs"
                          onClick={() => onEditSemester(semester)}
                          aria-label={`Edit ${semester.name}`}
                        >
                          <Pencil size={12} className="mr-1.5" />
                          Edit
                        </Button>
                        {year.isCurrent && !semester.isCurrent && (
                          <Button
                            size="sm"
                            variant="outline"
                            className="h-8 text-xs"
                            onClick={() => onSetCurrentSemester(year.id, semester.id)}
                          >
                            <Check size={12} className="mr-1.5" />
                            Set Current
                          </Button>
                        )}
                      </div>
                    )}
                  </div>
                </div>
              )
            })}
          </div>
        </div>
      )}
    </div>
  )
}
