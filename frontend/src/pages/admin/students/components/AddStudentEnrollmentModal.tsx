import { useForm, Controller } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useQuery, useMutation } from '@tanstack/react-query'
import { toast } from 'sonner'
import { isAxiosError } from 'axios'
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from '../../../../components/ui/dialog'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '../../../../components/ui/select'
import { Button } from '../../../../components/ui/button'
import { Label } from '../../../../components/ui/label'
import { academicYearsApi } from '../../../../api/academicYears'
import { gradesApi } from '../../../../api/grades'
import { enrollmentsApi } from '../../../../api/enrollments'

interface AddStudentEnrollmentModalProps {
  open: boolean
  studentId: string
  enrolledYearIds: Set<string>
  onClose: () => void
  onEnrolled: () => void
}

const schema = z.object({
  academicYearId: z.string().uuid('Select an academic year'),
  sectionId: z.string().uuid('Select a section'),
})

type FormValues = z.infer<typeof schema>

function extractError(err: unknown): string {
  if (isAxiosError(err) && err.response?.data?.error) return err.response.data.error
  return 'An unexpected error occurred.'
}

export function AddStudentEnrollmentModal({
  open,
  studentId,
  enrolledYearIds,
  onClose,
  onEnrolled,
}: AddStudentEnrollmentModalProps) {
  const { data: years = [] } = useQuery({
    queryKey: ['academic-years'],
    queryFn: academicYearsApi.list,
    enabled: open,
  })

  const { data: grades = [] } = useQuery({
    queryKey: ['grades'],
    queryFn: gradesApi.list,
    enabled: open,
  })

  const availableYears = years.filter((y) => !enrolledYearIds.has(y.id))

  const allSections = grades.flatMap((g) =>
    g.sections.map((s) => ({ id: s.id, label: `${g.name} — ${s.name}` }))
  )

  const {
    handleSubmit,
    control,
    reset,
    formState: { errors },
  } = useForm<FormValues>({ resolver: zodResolver(schema) })

  const mutation = useMutation({
    mutationFn: (data: FormValues) =>
      enrollmentsApi.enroll(data.sectionId, { studentId, academicYearId: data.academicYearId }),
    onSuccess: () => {
      toast.success('Student enrolled')
      reset()
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

  const handleOpenChange = (isOpen: boolean) => {
    if (!isOpen) { reset(); onClose() }
  }

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent className="sm:max-w-sm" onInteractOutside={(e) => e.preventDefault()}>
        <DialogHeader>
          <DialogTitle>Add Section Enrollment</DialogTitle>
        </DialogHeader>

        <form onSubmit={handleSubmit((d) => mutation.mutate(d))} className="flex flex-col gap-4 py-2">
          <div className="flex flex-col gap-1.5">
            <Label>Academic Year</Label>
            <Controller
              name="academicYearId"
              control={control}
              render={({ field }) => (
                <Select value={field.value ?? ''} onValueChange={field.onChange}>
                  <SelectTrigger>
                    <SelectValue placeholder="Select year…" />
                  </SelectTrigger>
                  <SelectContent>
                    {availableYears.map((y) => (
                      <SelectItem key={y.id} value={y.id}>{y.name}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              )}
            />
            {errors.academicYearId && (
              <p className="text-xs text-destructive">{errors.academicYearId.message}</p>
            )}
          </div>

          <div className="flex flex-col gap-1.5">
            <Label>Section</Label>
            <Controller
              name="sectionId"
              control={control}
              render={({ field }) => (
                <Select value={field.value ?? ''} onValueChange={field.onChange}>
                  <SelectTrigger>
                    <SelectValue placeholder="Select section…" />
                  </SelectTrigger>
                  <SelectContent>
                    {allSections.map((s) => (
                      <SelectItem key={s.id} value={s.id}>{s.label}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              )}
            />
            {errors.sectionId && (
              <p className="text-xs text-destructive">{errors.sectionId.message}</p>
            )}
          </div>

          <DialogFooter className="pt-2">
            <Button type="button" variant="outline" onClick={() => handleOpenChange(false)}>
              Cancel
            </Button>
            <Button type="submit" disabled={mutation.isPending}>
              {mutation.isPending ? 'Enrolling…' : 'Enroll'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
