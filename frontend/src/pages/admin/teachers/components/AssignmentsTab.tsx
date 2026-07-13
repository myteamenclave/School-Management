import { useState, useEffect } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { isAxiosError } from 'axios'
import { Plus, Trash2 } from 'lucide-react'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '../../../../components/ui/select'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '../../../../components/ui/table'
import { Button } from '../../../../components/ui/button'
import { Badge } from '../../../../components/ui/badge'
import { AddAssignmentModal } from './AddAssignmentModal'
import { academicYearsApi, ACADEMIC_YEAR_KEYS } from '../../../../api/academicYears'
import { teacherAssignmentsApi, ASSIGNMENT_KEYS } from '../../../../api/teacherAssignments'

interface AssignmentsTabProps {
  teacherId: string
}

function extractError(err: unknown): string {
  if (isAxiosError(err) && err.response?.data?.error) return err.response.data.error
  return 'An unexpected error occurred.'
}

export function AssignmentsTab({ teacherId }: AssignmentsTabProps) {
  const queryClient = useQueryClient()
  const [selectedYearId, setSelectedYearId] = useState<string>('')
  const [addOpen, setAddOpen] = useState(false)

  const { data: years = [] } = useQuery({
    queryKey: ACADEMIC_YEAR_KEYS.all,
    queryFn: academicYearsApi.list,
  })

  useEffect(() => {
    if (years.length > 0 && !selectedYearId) {
      const active = years.find((y) => y.status === 'Active')
      setSelectedYearId(active?.id ?? years[0].id)
    }
  }, [years, selectedYearId])

  const { data: assignments = [], isLoading } = useQuery({
    queryKey: ASSIGNMENT_KEYS.byTeacherAndYear(teacherId, selectedYearId),
    queryFn: () => teacherAssignmentsApi.getByTeacherAndYear(teacherId, selectedYearId),
    enabled: selectedYearId !== '',
  })

  const removeMutation = useMutation({
    mutationFn: (assignmentId: string) =>
      teacherAssignmentsApi.remove(teacherId, assignmentId),
    onSuccess: () => {
      queryClient.invalidateQueries({
        queryKey: ASSIGNMENT_KEYS.byTeacherAndYear(teacherId, selectedYearId),
      })
      toast.success('Assignment removed')
    },
    onError: (err) => toast.error(extractError(err)),
  })

  const handleRemove = (id: string, subjectName: string) => {
    if (!window.confirm(`Remove assignment for "${subjectName}"?`)) return
    removeMutation.mutate(id)
  }

  const handleAssigned = () => {
    queryClient.invalidateQueries({
      queryKey: ASSIGNMENT_KEYS.byTeacherAndYear(teacherId, selectedYearId),
    })
  }

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center gap-3">
        <Select value={selectedYearId} onValueChange={setSelectedYearId}>
          <SelectTrigger className="w-52">
            <SelectValue placeholder="Select academic year" />
          </SelectTrigger>
          <SelectContent>
            {years.map((y) => (
              <SelectItem key={y.id} value={y.id}>{y.name}</SelectItem>
            ))}
          </SelectContent>
        </Select>

        <Button size="sm" disabled={!selectedYearId} onClick={() => setAddOpen(true)}>
          <Plus size={14} className="mr-1.5" /> Add Assignment
        </Button>
      </div>

      {isLoading ? (
        <div className="flex items-center justify-center h-32 text-sm text-muted-foreground">
          Loading…
        </div>
      ) : (
        <div className="rounded-lg border border-border overflow-hidden">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Subject</TableHead>
                <TableHead>Grade</TableHead>
                <TableHead>Section</TableHead>
                <TableHead className="w-14" />
              </TableRow>
            </TableHeader>
            <TableBody>
              {assignments.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={4} className="h-24 text-center text-muted-foreground text-sm">
                    {selectedYearId ? 'No assignments for this year.' : 'Select a year to view assignments.'}
                  </TableCell>
                </TableRow>
              ) : (
                assignments.map((a) => (
                  <TableRow key={a.id}>
                    <TableCell>
                      <div className="flex items-center gap-2">
                        <span className="font-medium">{a.subjectName}</span>
                        <Badge variant="secondary" className="font-mono text-xs">{a.subjectCode}</Badge>
                      </div>
                    </TableCell>
                    <TableCell className="text-sm text-muted-foreground">{a.gradeName}</TableCell>
                    <TableCell className="text-sm text-muted-foreground">{a.sectionName}</TableCell>
                    <TableCell>
                      <Button
                        size="sm"
                        variant="ghost"
                        className="h-7 w-7 p-0 text-destructive hover:text-destructive"
                        onClick={() => handleRemove(a.id, a.subjectName)}
                        disabled={removeMutation.isPending}
                      >
                        <Trash2 size={13} />
                      </Button>
                    </TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>
        </div>
      )}

      {selectedYearId && (
        <AddAssignmentModal
          open={addOpen}
          teacherId={teacherId}
          academicYearId={selectedYearId}
          onClose={() => setAddOpen(false)}
          onAssigned={handleAssigned}
        />
      )}
    </div>
  )
}
