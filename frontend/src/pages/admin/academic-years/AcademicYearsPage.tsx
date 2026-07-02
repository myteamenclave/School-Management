import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { isAxiosError } from 'axios'
import { Plus, ChevronDown, ChevronUp } from 'lucide-react'
import { academicYearsApi, ACADEMIC_YEAR_KEYS } from '../../../api/academicYears'
import type { SemesterDto } from '../../../api/academicYears'
import { AcademicYearCard } from './components/AcademicYearCard'
import { CreateYearModal } from './components/CreateYearModal'
import { EditSemesterModal } from './components/EditSemesterModal'
import { Button } from '../../../components/ui/button'

function extractError(err: unknown): string {
  if (isAxiosError(err) && err.response?.data?.error) return err.response.data.error
  return 'An unexpected error occurred.'
}

export function AcademicYearsPage() {
  const queryClient = useQueryClient()
  const [createOpen, setCreateOpen] = useState(false)
  const [editingSemester, setEditingSemester] = useState<SemesterDto | null>(null)
  const [showArchived, setShowArchived] = useState(false)

  const { data: years = [], isLoading, isError } = useQuery({
    queryKey: ACADEMIC_YEAR_KEYS.all,
    queryFn: academicYearsApi.list,
  })

  const currentYear = years.find((y) => y.isCurrent) ?? null
  const previousYears = years.filter((y) => !y.isCurrent && y.status === 'Active')
  const archivedYears = years.filter((y) => y.status === 'Archived')

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ACADEMIC_YEAR_KEYS.all })

  const setCurrentMutation = useMutation({
    mutationFn: academicYearsApi.setCurrentYear,
    onSuccess: () => { invalidate(); toast.success('Current year updated') },
    onError: (err) => toast.error(extractError(err)),
  })

  const archiveMutation = useMutation({
    mutationFn: academicYearsApi.archive,
    onSuccess: () => { invalidate(); toast.success('Academic year archived') },
    onError: (err) => toast.error(extractError(err)),
  })

  const setCurrentSemesterMutation = useMutation({
    mutationFn: ({ yearId, semesterId }: { yearId: string; semesterId: string }) =>
      academicYearsApi.setCurrentSemester(yearId, semesterId),
    onSuccess: () => { invalidate(); toast.success('Current semester updated') },
    onError: (err) => toast.error(extractError(err)),
  })

  if (isLoading) {
    return (
      <div className="flex items-center justify-center h-48 text-muted-foreground text-sm">
        Loading academic years…
      </div>
    )
  }

  if (isError) {
    return (
      <div className="p-8 text-destructive text-sm">
        Failed to load academic years. Please refresh the page.
      </div>
    )
  }

  return (
    <div className="px-8 py-8 max-w-4xl mx-auto">
      {/* Page header */}
      <div className="flex items-start justify-between mb-8">
        <div>
          <h1 className="font-heading text-2xl font-semibold text-foreground">Academic Years</h1>
          <p className="text-sm text-muted-foreground mt-1">
            Manage academic years, semesters, and set the current active period.
          </p>
        </div>
        <Button onClick={() => setCreateOpen(true)} className="flex items-center gap-2">
          <Plus size={16} />
          New Academic Year
        </Button>
      </div>

      {/* Empty state */}
      {years.length === 0 && (
        <div className="flex flex-col items-center justify-center py-20 text-center">
          <p className="text-muted-foreground text-sm mb-4">
            No academic years yet. Create your first one to get started.
          </p>
          <Button onClick={() => setCreateOpen(true)} variant="outline">
            <Plus size={15} className="mr-2" />
            New Academic Year
          </Button>
        </div>
      )}

      {/* Current Year */}
      {currentYear && (
        <section className="mb-6">
          <h2 className="text-xs font-semibold text-muted-foreground uppercase tracking-wide mb-3">
            Current Year
          </h2>
          <AcademicYearCard
            year={currentYear}
            onSetCurrent={(id) => setCurrentMutation.mutate(id)}
            onArchive={(id) => archiveMutation.mutate(id)}
            onEditSemester={setEditingSemester}
            onSetCurrentSemester={(yearId, semesterId) =>
              setCurrentSemesterMutation.mutate({ yearId, semesterId })
            }
          />
        </section>
      )}

      {/* Previous Years */}
      {previousYears.length > 0 && (
        <section className="mb-6">
          <h2 className="text-xs font-semibold text-muted-foreground uppercase tracking-wide mb-3">
            Previous Years
          </h2>
          <div className="flex flex-col gap-3">
            {previousYears.map((year) => (
              <AcademicYearCard
                key={year.id}
                year={year}
                onSetCurrent={(id) => setCurrentMutation.mutate(id)}
                onArchive={(id) => archiveMutation.mutate(id)}
                onEditSemester={setEditingSemester}
                onSetCurrentSemester={(yearId, semesterId) =>
                  setCurrentSemesterMutation.mutate({ yearId, semesterId })
                }
              />
            ))}
          </div>
        </section>
      )}

      {/* Archived toggle */}
      {archivedYears.length > 0 && (
        <section>
          <button
            className="flex items-center gap-2 text-sm text-muted-foreground hover:text-foreground transition-colors mb-3"
            onClick={() => setShowArchived((v) => !v)}
          >
            {showArchived ? <ChevronUp size={15} /> : <ChevronDown size={15} />}
            {showArchived ? 'Hide archived' : `Show archived (${archivedYears.length})`}
          </button>
          {showArchived && (
            <div className="flex flex-col gap-3">
              {archivedYears.map((year) => (
                <AcademicYearCard
                  key={year.id}
                  year={year}
                  onSetCurrent={(id) => setCurrentMutation.mutate(id)}
                  onArchive={(id) => archiveMutation.mutate(id)}
                  onEditSemester={setEditingSemester}
                  onSetCurrentSemester={(yearId, semesterId) =>
                    setCurrentSemesterMutation.mutate({ yearId, semesterId })
                  }
                />
              ))}
            </div>
          )}
        </section>
      )}

      <CreateYearModal open={createOpen} onClose={() => setCreateOpen(false)} />
      <EditSemesterModal semester={editingSemester} onClose={() => setEditingSemester(null)} />
    </div>
  )
}
