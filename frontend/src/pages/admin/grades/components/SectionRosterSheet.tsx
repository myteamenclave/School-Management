import { useState, useEffect } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { isAxiosError } from 'axios'
import { Plus, ArrowRightLeft, Trash2 } from 'lucide-react'
import {
  Sheet,
  SheetContent,
  SheetHeader,
  SheetTitle,
  SheetDescription,
} from '../../../../components/ui/sheet'
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
import { EnrollStudentModal } from './EnrollStudentModal'
import { TransferStudentModal } from './TransferStudentModal'
import { academicYearsApi, ACADEMIC_YEAR_KEYS } from '../../../../api/academicYears'
import { enrollmentsApi, ENROLLMENT_KEYS } from '../../../../api/enrollments'
import type { EnrollmentDto } from '../../../../api/enrollments'
import type { SectionDto } from '../../../../api/grades'

interface SectionRosterSheetProps {
  section: SectionDto | null
  onClose: () => void
}

function extractError(err: unknown): string {
  if (isAxiosError(err) && err.response?.data?.error) return err.response.data.error
  return 'An unexpected error occurred.'
}

export function SectionRosterSheet({ section, onClose }: SectionRosterSheetProps) {
  const queryClient = useQueryClient()
  const [selectedYearId, setSelectedYearId] = useState<string>('')
  const [enrollOpen, setEnrollOpen] = useState(false)
  const [transferEnrollment, setTransferEnrollment] = useState<EnrollmentDto | null>(null)

  const { data: years = [] } = useQuery({
    queryKey: ACADEMIC_YEAR_KEYS.all,
    queryFn: academicYearsApi.list,
    enabled: section !== null,
  })

  useEffect(() => {
    if (years.length > 0 && !selectedYearId) {
      const active = years.find((y) => y.status === 'Active')
      setSelectedYearId(active?.id ?? years[0].id)
    }
  }, [years, selectedYearId])

  // Reset year selection when sheet closes
  useEffect(() => {
    if (!section) setSelectedYearId('')
  }, [section])

  const { data: enrollments = [], isLoading } = useQuery({
    queryKey: ENROLLMENT_KEYS.bySectionAndYear(section?.id ?? '', selectedYearId),
    queryFn: () => enrollmentsApi.getBySectionAndYear(section!.id, selectedYearId),
    enabled: section !== null && selectedYearId !== '',
  })

  const removeMutation = useMutation({
    mutationFn: (id: string) => enrollmentsApi.remove(id),
    onSuccess: () => {
      queryClient.invalidateQueries({
        queryKey: ENROLLMENT_KEYS.bySectionAndYear(section!.id, selectedYearId),
      })
      toast.success('Student removed from roster')
    },
    onError: (err) => toast.error(extractError(err)),
  })

  const handleRemove = (enrollment: EnrollmentDto) => {
    if (!window.confirm(`Remove ${enrollment.studentFirstName} ${enrollment.studentLastName} from this section?`)) return
    removeMutation.mutate(enrollment.id)
  }

  const handleEnrolled = () => {
    queryClient.invalidateQueries({
      queryKey: ENROLLMENT_KEYS.bySectionAndYear(section!.id, selectedYearId),
    })
    setEnrollOpen(false)
  }

  const handleTransferred = () => {
    queryClient.invalidateQueries({
      queryKey: ENROLLMENT_KEYS.bySectionAndYear(section!.id, selectedYearId),
    })
    setTransferEnrollment(null)
  }

  return (
    <>
      <Sheet open={section !== null} onOpenChange={(isOpen: boolean) => { if (!isOpen) onClose() }}>
        <SheetContent side="right" className="w-[520px] sm:max-w-[520px] flex flex-col gap-0 p-0" onOpenAutoFocus={(e) => e.preventDefault()}>
          <SheetHeader className="px-6 pt-6 pb-4 border-b border-border">
            <SheetTitle>Section Roster</SheetTitle>
            <SheetDescription>
              {section ? `${section.name} — enrolled students` : ''}
            </SheetDescription>
          </SheetHeader>

          <div className="flex-1 overflow-y-auto px-6 py-4 flex flex-col gap-4">
            {/* Year selector + enroll button */}
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

              <Button
                size="sm"
                disabled={!selectedYearId}
                onClick={() => setEnrollOpen(true)}
              >
                <Plus size={14} className="mr-1.5" /> Enroll Student
              </Button>
            </div>

            {/* Roster table */}
            {isLoading ? (
              <div className="flex items-center justify-center h-32 text-sm text-muted-foreground">
                Loading…
              </div>
            ) : (
              <div className="rounded-lg border border-border overflow-hidden">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Code</TableHead>
                      <TableHead>Name</TableHead>
                      <TableHead>Enrolled</TableHead>
                      <TableHead className="w-20" />
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {enrollments.length === 0 ? (
                      <TableRow>
                        <TableCell colSpan={4} className="h-24 text-center text-muted-foreground text-sm">
                          {selectedYearId ? 'No students enrolled.' : 'Select a year to view the roster.'}
                        </TableCell>
                      </TableRow>
                    ) : (
                      enrollments.map((e) => (
                        <TableRow key={e.id}>
                          <TableCell>
                            <span className="font-mono text-xs text-muted-foreground">{e.studentCode}</span>
                          </TableCell>
                          <TableCell className="font-medium">
                            {e.studentFirstName} {e.studentLastName}
                          </TableCell>
                          <TableCell className="text-xs text-muted-foreground">
                            {new Date(e.createdAt).toLocaleDateString()}
                          </TableCell>
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
                                title="Remove from section"
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
          </div>
        </SheetContent>
      </Sheet>

      {section && selectedYearId && (
        <EnrollStudentModal
          open={enrollOpen}
          sectionId={section.id}
          academicYearId={selectedYearId}
          onClose={() => setEnrollOpen(false)}
          onEnrolled={handleEnrolled}
        />
      )}

      <TransferStudentModal
        enrollment={transferEnrollment}
        onClose={() => setTransferEnrollment(null)}
        onTransferred={handleTransferred}
      />
    </>
  )
}
