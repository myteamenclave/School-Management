import { Label } from '../../components/ui/label'
import { Badge } from '../../components/ui/badge'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '../../components/ui/select'
import type { ParentChild, ParentAcademicYear } from '../../api/parentPortal'

interface ParentChildYearBarProps {
  children: ParentChild[]
  years: ParentAcademicYear[]
  childId: string | null
  academicYearId: string | null
  onChildChange: (id: string) => void
  onYearChange: (id: string) => void
}

// Shared parent-portal control bar: child switcher (hidden for a single child) + academic-year
// selector, plus the selected-child summary line. Used by both the grades and attendance pages.
export function ParentChildYearBar({
  children,
  years,
  childId,
  academicYearId,
  onChildChange,
  onYearChange,
}: ParentChildYearBarProps) {
  const selectedChild = children.find((c) => c.studentId === childId)

  return (
    <>
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 mb-6">
        {/* Child switcher — hidden when there's only one child. */}
        {children.length > 1 && (
          <div className="flex flex-col gap-1.5">
            <Label>Child</Label>
            <Select value={childId ?? ''} onValueChange={onChildChange}>
              <SelectTrigger>
                <SelectValue placeholder="Select child" />
              </SelectTrigger>
              <SelectContent>
                {children.map((c) => (
                  <SelectItem key={c.studentId} value={c.studentId}>
                    {c.studentName} ({c.studentCode})
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        )}

        <div className="flex flex-col gap-1.5">
          <Label>Academic Year</Label>
          <Select value={academicYearId ?? ''} onValueChange={onYearChange}>
            <SelectTrigger>
              <SelectValue placeholder="Select year" />
            </SelectTrigger>
            <SelectContent>
              {years.map((y) => (
                <SelectItem key={y.id} value={y.id}>
                  {y.name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      </div>

      {/* Selected child summary. */}
      {selectedChild && (
        <div className="mb-4 flex flex-wrap items-center gap-2">
          <span className="text-base font-semibold text-foreground">{selectedChild.studentName}</span>
          <span className="font-mono text-xs text-muted-foreground">{selectedChild.studentCode}</span>
          {selectedChild.currentGradeLabel && (
            <Badge variant="secondary">
              {selectedChild.currentGradeLabel}
              {selectedChild.currentSectionName ? ` · ${selectedChild.currentSectionName}` : ''}
            </Badge>
          )}
          {selectedChild.enrollmentStatus !== 'Active' && (
            <Badge variant="outline">{selectedChild.enrollmentStatus}</Badge>
          )}
        </div>
      )}
    </>
  )
}
