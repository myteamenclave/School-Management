import { useState, useEffect, useCallback } from 'react'
import { useQuery, useMutation } from '@tanstack/react-query'
import { toast } from 'sonner'
import { isAxiosError } from 'axios'
import { Search } from 'lucide-react'
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from '../../../../components/ui/dialog'
import { Button } from '../../../../components/ui/button'
import { Input } from '../../../../components/ui/input'
import { enrollmentsApi } from '../../../../api/enrollments'
import { studentsApi } from '../../../../api/students'

interface EnrollStudentModalProps {
  open: boolean
  sectionId: string
  academicYearId: string
  onClose: () => void
  onEnrolled: () => void
}

function extractError(err: unknown): string {
  if (isAxiosError(err) && err.response?.data?.error) return err.response.data.error
  return 'An unexpected error occurred.'
}

export function EnrollStudentModal({
  open,
  sectionId,
  academicYearId,
  onClose,
  onEnrolled,
}: EnrollStudentModalProps) {
  const [search, setSearch] = useState('')
  const [debouncedSearch, setDebouncedSearch] = useState('')
  const [selectedId, setSelectedId] = useState<string>('')

  useEffect(() => {
    if (!open) {
      setSearch('')
      setDebouncedSearch('')
      setSelectedId('')
    }
  }, [open])

  useEffect(() => {
    const t = setTimeout(() => setDebouncedSearch(search), 300)
    return () => clearTimeout(t)
  }, [search])

  const { data: enrolledIds = [] } = useQuery({
    queryKey: ['enrollments', 'enrolled-ids', academicYearId],
    queryFn: () => enrollmentsApi.getEnrolledIds(academicYearId),
    enabled: open,
  })

  const { data: studentsData, isLoading } = useQuery({
    queryKey: ['students', 'picker', debouncedSearch],
    queryFn: () =>
      studentsApi.list({ status: 'Active', search: debouncedSearch, page: 1, pageSize: 20 }),
    enabled: open,
  })

  const enrolledSet = new Set(enrolledIds)

  const enrollMutation = useMutation({
    mutationFn: () =>
      enrollmentsApi.enroll(sectionId, { studentId: selectedId, academicYearId }),
    onSuccess: () => {
      toast.success('Student enrolled')
      onEnrolled()
      onClose()
    },
    onError: (err) => {
      if (isAxiosError(err) && err.response?.status === 409) {
        toast.error('Student is already enrolled for this year.')
      } else {
        toast.error(extractError(err))
      }
    },
  })

  const handleSubmit = useCallback(() => {
    if (!selectedId) return
    enrollMutation.mutate()
  }, [selectedId, enrollMutation])

  const students = studentsData?.items ?? []

  return (
    <Dialog open={open} onOpenChange={(o) => { if (!o) onClose() }}>
      <DialogContent className="sm:max-w-md" onInteractOutside={(e) => e.preventDefault()}>
        <DialogHeader>
          <DialogTitle>Enroll Student</DialogTitle>
        </DialogHeader>

        <div className="flex flex-col gap-3 py-2">
          <div className="relative">
            <Search size={15} className="absolute left-3 top-1/2 -translate-y-1/2 text-muted-foreground" />
            <Input
              placeholder="Search by name or code…"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              className="pl-9"
            />
          </div>

          <div className="rounded-lg border border-border overflow-hidden max-h-64 overflow-y-auto">
            {isLoading ? (
              <div className="flex items-center justify-center h-24 text-sm text-muted-foreground">
                Loading…
              </div>
            ) : students.length === 0 ? (
              <div className="flex items-center justify-center h-24 text-sm text-muted-foreground">
                No students found.
              </div>
            ) : (
              <ul>
                {students.map((s) => {
                  const alreadyEnrolled = enrolledSet.has(s.id)
                  const isSelected = selectedId === s.id
                  return (
                    <li
                      key={s.id}
                      onClick={() => { if (!alreadyEnrolled) setSelectedId(s.id) }}
                      className={[
                        'flex items-center gap-3 px-4 py-2.5 text-sm border-b border-border last:border-0 transition-colors',
                        alreadyEnrolled
                          ? 'opacity-40 cursor-not-allowed text-muted-foreground'
                          : isSelected
                          ? 'bg-primary/10 cursor-pointer'
                          : 'hover:bg-accent cursor-pointer',
                      ].join(' ')}
                    >
                      <span className="font-mono text-xs text-muted-foreground w-28 shrink-0">
                        {s.studentCode}
                      </span>
                      <span className="font-medium">
                        {s.firstName} {s.lastName}
                      </span>
                      {alreadyEnrolled && (
                        <span className="ml-auto text-xs text-muted-foreground">Enrolled</span>
                      )}
                    </li>
                  )
                })}
              </ul>
            )}
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={onClose}>Cancel</Button>
          <Button
            disabled={!selectedId || enrollMutation.isPending}
            onClick={handleSubmit}
          >
            {enrollMutation.isPending ? 'Enrolling…' : 'Enroll'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
