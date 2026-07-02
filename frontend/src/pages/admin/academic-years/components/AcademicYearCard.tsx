import { CheckCircle, Lock, Pencil } from 'lucide-react'
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
  const [year, month, day] = dateStr.split('-')
  const d = new Date(Number(year), Number(month) - 1, Number(day))
  return d.toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' })
}

function semesterSubLabel(semester: SemesterDto, year: AcademicYearDto): string | null {
  if (semester.isCurrent) return null
  if (year.isCurrent) return 'Upcoming'
  return 'Completed'
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
      {/* Card header */}
      <div
        className={`px-5 py-4 flex items-start justify-between gap-4 ${
          year.isCurrent ? 'bg-primary/5' : ''
        }`}
      >
        <div className="flex flex-col gap-1">
          <div className="flex items-center gap-2 flex-wrap">
            <h3 className="font-heading font-semibold text-base text-foreground">{year.name}</h3>
            {/* Status badge */}
            {isArchived ? (
              <span className="inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium bg-muted text-muted-foreground">
                Archived
              </span>
            ) : (
              <span className="inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium bg-accent/10 text-accent">
                Active
              </span>
            )}
            {/* Current year badge */}
            {year.isCurrent && (
              <span className="inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-medium bg-primary text-white">
                <CheckCircle size={11} />
                Current Year
              </span>
            )}
          </div>
          <p className="text-sm text-muted-foreground">
            {formatDate(year.startDate)} – {formatDate(year.endDate)}
          </p>
        </div>

        {/* Right-side actions */}
        <div className="flex items-center gap-2 flex-shrink-0">
          {isArchived ? (
            <span className="text-xs text-muted-foreground italic">Archived — read only</span>
          ) : year.isCurrent ? (
            <span className="inline-flex items-center gap-1 rounded-full border border-border px-2.5 py-1 text-xs text-muted-foreground">
              <Lock size={11} />
              Protected — cannot archive
            </span>
          ) : (
            <>
              <Button
                size="sm"
                variant="outline"
                onClick={() => onSetCurrent(year.id)}
              >
                Set as Current
              </Button>
              <Button
                size="sm"
                variant="outline"
                className="text-destructive hover:text-destructive"
                onClick={handleArchive}
              >
                Archive
              </Button>
            </>
          )}
        </div>
      </div>

      {/* Semesters */}
      {year.semesters.length > 0 && (
        <div className="border-t border-border">
          {year.semesters.map((semester, idx) => {
            const subLabel = semesterSubLabel(semester, year)
            return (
              <div
                key={semester.id}
                className={`flex items-center gap-0 ${
                  idx < year.semesters.length - 1 ? 'border-b border-border' : ''
                }`}
              >
                {/* Accent bar */}
                <div
                  className="self-stretch w-[3px] flex-shrink-0"
                  style={{ backgroundColor: semester.isCurrent ? '#1E3A5F' : '#E4E7EB' }}
                />
                <div className="flex flex-1 items-center justify-between px-4 py-3 gap-4">
                  <div className="flex flex-col gap-0.5 min-w-0">
                    <div className="flex items-center gap-2 flex-wrap">
                      <span className="text-sm font-medium text-foreground">{semester.name}</span>
                      {semester.isCurrent && (
                        <span className="inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-medium bg-secondary/10 text-secondary">
                          <CheckCircle size={10} />
                          Current
                        </span>
                      )}
                    </div>
                    <div className="flex items-center gap-2">
                      <span className="text-xs text-muted-foreground">
                        {formatDate(semester.startDate)} – {formatDate(semester.endDate)}
                      </span>
                      {subLabel && (
                        <span className="text-xs text-muted-foreground/70">{subLabel}</span>
                      )}
                    </div>
                  </div>

                  {!isArchived && (
                    <div className="flex items-center gap-2 flex-shrink-0">
                      <Button
                        size="sm"
                        variant="ghost"
                        className="h-7 px-2 text-xs"
                        onClick={() => onEditSemester(semester)}
                        aria-label={`Edit ${semester.name}`}
                      >
                        <Pencil size={13} className="mr-1" />
                        Edit
                      </Button>
                      {year.isCurrent && !semester.isCurrent && (
                        <Button
                          size="sm"
                          variant="ghost"
                          className="h-7 px-2 text-xs"
                          onClick={() => onSetCurrentSemester(year.id, semester.id)}
                        >
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
      )}
    </div>
  )
}
