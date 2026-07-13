import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { isAxiosError } from 'axios'
import { Plus, ArrowRightLeft, Trash2 } from 'lucide-react'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '../../../../components/ui/table'
import { Button } from '../../../../components/ui/button'
import { TransferStudentModal } from '../../grades/components/TransferStudentModal'
import { AddStudentEnrollmentModal } from './AddStudentEnrollmentModal'
import { enrollmentsApi, ENROLLMENT_KEYS } from '../../../../api/enrollments'
import type { EnrollmentDto } from '../../../../api/enrollments'

interface StudentSectionAssignmentsTabProps {
  studentId: string
}

function extractError(err: unknown): string {
  if (isAxiosError(err) && err.response?.data?.error) return err.response.data.error
  return 'An unexpected error occurred.'
}

export function StudentSectionAssignmentsTab({ studentId }: StudentSectionAssignmentsTabProps) {
  const queryClient = useQueryClient()
  const [addOpen, setAddOpen] = useState(false)
  const [transferEnrollment, setTransferEnrollment] = useState<EnrollmentDto | null>(null)

  const { data: enrollments = [], isLoading } = useQuery({
    queryKey: ENROLLMENT_KEYS.byStudent(studentId),
    queryFn: () => enrollmentsApi.getByStudentId(studentId),
  })

  const enrolledYearIds = new Set(enrollments.map((e) => e.academicYearId))

  const removeMutation = useMutation({
    mutationFn: (id: string) => enrollmentsApi.remove(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ENROLLMENT_KEYS.byStudent(studentId) })
      toast.success('Enrollment removed')
    },
    onError: (err) => toast.error(extractError(err)),
  })

  const handleRemove = (enrollment: EnrollmentDto) => {
    if (!window.confirm(`Remove enrollment for ${enrollment.academicYearName}?`)) return
    removeMutation.mutate(enrollment.id)
  }

  const handleEnrolled = () => {
    queryClient.invalidateQueries({ queryKey: ENROLLMENT_KEYS.byStudent(studentId) })
  }

  const handleTransferred = () => {
    queryClient.invalidateQueries({ queryKey: ENROLLMENT_KEYS.byStudent(studentId) })
  }

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between">
        <p className="text-sm text-muted-foreground">Section placement per academic year.</p>
        <Button size="sm" onClick={() => setAddOpen(true)}>
          <Plus size={14} className="mr-1.5" /> Add Enrollment
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
                <TableHead>Academic Year</TableHead>
                <TableHead>Grade</TableHead>
                <TableHead>Section</TableHead>
                <TableHead className="w-20" />
              </TableRow>
            </TableHeader>
            <TableBody>
              {enrollments.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={4} className="h-24 text-center text-muted-foreground text-sm">
                    No section enrollments yet.
                  </TableCell>
                </TableRow>
              ) : (
                enrollments.map((e) => (
                  <TableRow key={e.id}>
                    <TableCell className="font-medium">{e.academicYearName}</TableCell>
                    <TableCell className="text-sm text-muted-foreground">{e.gradeName}</TableCell>
                    <TableCell className="text-sm text-muted-foreground">{e.sectionName}</TableCell>
                    <TableCell>
                      <div className="flex items-center gap-1">
                        <Button
                          size="sm"
                          variant="ghost"
                          className="h-7 w-7 p-0"
                          title="Transfer to another section"
                          onClick={() => setTransferEnrollment(e)}
                        >
                          <ArrowRightLeft size={13} />
                        </Button>
                        <Button
                          size="sm"
                          variant="ghost"
                          className="h-7 w-7 p-0 text-destructive hover:text-destructive"
                          title="Remove enrollment"
                          onClick={() => handleRemove(e)}
                          disabled={removeMutation.isPending}
                        >
                          <Trash2 size={13} />
                        </Button>
                      </div>
                    </TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>
        </div>
      )}

      <AddStudentEnrollmentModal
        open={addOpen}
        studentId={studentId}
        enrolledYearIds={enrolledYearIds}
        onClose={() => setAddOpen(false)}
        onEnrolled={handleEnrolled}
      />

      <TransferStudentModal
        enrollment={transferEnrollment}
        onClose={() => setTransferEnrollment(null)}
        onTransferred={handleTransferred}
      />
    </div>
  )
}
