import { useEffect } from 'react'
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
import { gradesApi } from '../../../../api/grades'
import { subjectsApi } from '../../../../api/subjects'
import { teacherAssignmentsApi } from '../../../../api/teacherAssignments'

interface AddAssignmentModalProps {
  open: boolean
  teacherId: string
  academicYearId: string
  onClose: () => void
  onAssigned: () => void
}

const schema = z.object({
  gradeId:   z.string().uuid('Select a grade'),
  sectionId: z.string().uuid('Select a section'),
  subjectId: z.string().uuid('Select a subject'),
})

type FormValues = z.infer<typeof schema>

function extractError(err: unknown): string {
  if (isAxiosError(err) && err.response?.data?.error) return err.response.data.error
  return 'An unexpected error occurred.'
}

export function AddAssignmentModal({
  open,
  teacherId,
  academicYearId,
  onClose,
  onAssigned,
}: AddAssignmentModalProps) {
  const {
    handleSubmit,
    control,
    watch,
    reset,
    formState: { errors },
  } = useForm<FormValues>({ resolver: zodResolver(schema) })

  const selectedGradeId = watch('gradeId')

  useEffect(() => {
    if (!open) reset()
  }, [open, reset])

  const { data: grades = [] } = useQuery({
    queryKey: ['grades'],
    queryFn: gradesApi.list,
    enabled: open,
  })

  const { data: subjectsData } = useQuery({
    queryKey: ['subjects', 'picker'],
    queryFn: () => subjectsApi.list({ isActive: true, search: '', page: 1, pageSize: 100 }),
    enabled: open,
  })

  const subjects = subjectsData?.items ?? []
  const selectedGrade = grades.find((g) => g.id === selectedGradeId)

  const assignMutation = useMutation({
    mutationFn: (data: FormValues) =>
      teacherAssignmentsApi.assign(teacherId, {
        subjectId: data.subjectId,
        sectionId: data.sectionId,
        academicYearId,
      }),
    onSuccess: () => {
      toast.success('Assignment added')
      reset()
      onAssigned()
      onClose()
    },
    onError: (err) => {
      if (isAxiosError(err) && err.response?.status === 409) {
        toast.error('A teacher is already assigned to this subject in this section for this year.')
      } else {
        toast.error(extractError(err))
      }
    },
  })

  return (
    <Dialog open={open} onOpenChange={(o) => { if (!o) { reset(); onClose() } }}>
      <DialogContent className="sm:max-w-sm" onInteractOutside={(e) => e.preventDefault()}>
        <DialogHeader>
          <DialogTitle>Add Assignment</DialogTitle>
        </DialogHeader>

        <form
          onSubmit={handleSubmit((d) => assignMutation.mutate(d))}
          className="flex flex-col gap-4 py-2"
        >
          {/* Grade */}
          <div className="flex flex-col gap-1.5">
            <Label>Grade</Label>
            <Controller
              name="gradeId"
              control={control}
              render={({ field }) => (
                <Select value={field.value ?? ''} onValueChange={field.onChange}>
                  <SelectTrigger>
                    <SelectValue placeholder="Select grade…" />
                  </SelectTrigger>
                  <SelectContent>
                    {grades.map((g) => (
                      <SelectItem key={g.id} value={g.id}>{g.name}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              )}
            />
            {errors.gradeId && <p className="text-xs text-destructive">{errors.gradeId.message}</p>}
          </div>

          {/* Section */}
          <div className="flex flex-col gap-1.5">
            <Label>Section</Label>
            <Controller
              name="sectionId"
              control={control}
              render={({ field }) => (
                <Select
                  value={field.value ?? ''}
                  onValueChange={field.onChange}
                  disabled={!selectedGradeId}
                >
                  <SelectTrigger>
                    <SelectValue placeholder={selectedGradeId ? 'Select section…' : 'Select grade first'} />
                  </SelectTrigger>
                  <SelectContent>
                    {(selectedGrade?.sections ?? []).map((s) => (
                      <SelectItem key={s.id} value={s.id}>{s.name}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              )}
            />
            {errors.sectionId && <p className="text-xs text-destructive">{errors.sectionId.message}</p>}
          </div>

          {/* Subject */}
          <div className="flex flex-col gap-1.5">
            <Label>Subject</Label>
            <Controller
              name="subjectId"
              control={control}
              render={({ field }) => (
                <Select value={field.value ?? ''} onValueChange={field.onChange}>
                  <SelectTrigger>
                    <SelectValue placeholder="Select subject…" />
                  </SelectTrigger>
                  <SelectContent>
                    {subjects.map((s) => (
                      <SelectItem key={s.id} value={s.id}>
                        {s.name} <span className="text-muted-foreground ml-1">({s.code})</span>
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              )}
            />
            {errors.subjectId && <p className="text-xs text-destructive">{errors.subjectId.message}</p>}
          </div>

          <DialogFooter className="pt-2">
            <Button type="button" variant="outline" onClick={() => { reset(); onClose() }}>
              Cancel
            </Button>
            <Button type="submit" disabled={assignMutation.isPending}>
              {assignMutation.isPending ? 'Adding…' : 'Add'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
