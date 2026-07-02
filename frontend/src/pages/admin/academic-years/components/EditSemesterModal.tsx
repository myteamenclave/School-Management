import { useEffect } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { isAxiosError } from 'axios'
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from '../../../../components/ui/dialog'
import { Button } from '../../../../components/ui/button'
import { Input } from '../../../../components/ui/input'
import { Label } from '../../../../components/ui/label'
import type { SemesterDto } from '../../../../api/academicYears'
import { academicYearsApi, ACADEMIC_YEAR_KEYS } from '../../../../api/academicYears'

const schema = z
  .object({
    name: z.string().min(1, 'Required').max(100),
    startDate: z.string().min(1, 'Required'),
    endDate: z.string().min(1, 'Required'),
  })
  .refine((d) => d.endDate > d.startDate, {
    message: 'End date must be after start date',
    path: ['endDate'],
  })

type FormValues = z.infer<typeof schema>

interface EditSemesterModalProps {
  semester: SemesterDto | null
  onClose: () => void
}

export function EditSemesterModal({ semester, onClose }: EditSemesterModalProps) {
  const queryClient = useQueryClient()

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({ resolver: zodResolver(schema) })

  useEffect(() => {
    if (semester) {
      reset({
        name: semester.name,
        startDate: semester.startDate,
        endDate: semester.endDate,
      })
    }
  }, [semester, reset])

  const mutation = useMutation({
    mutationFn: (data: FormValues) =>
      academicYearsApi.updateSemester(semester!.academicYearId, semester!.id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ACADEMIC_YEAR_KEYS.all })
      toast.success('Semester updated')
      onClose()
    },
    onError: (err) => {
      if (isAxiosError(err) && err.response?.data?.error) {
        toast.error(err.response.data.error)
      } else {
        toast.error('Failed to update semester.')
      }
    },
  })

  const onSubmit = (data: FormValues) => mutation.mutateAsync(data)

  const handleOpenChange = (open: boolean) => {
    if (!open) onClose()
  }

  return (
    <Dialog open={semester !== null} onOpenChange={handleOpenChange}>
      <DialogContent className="modal-fade-in sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Edit Semester</DialogTitle>
        </DialogHeader>

        <form onSubmit={handleSubmit(onSubmit)} className="flex flex-col gap-4 py-2">
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="es-name">Semester Name</Label>
            <Input id="es-name" {...register('name')} />
            {errors.name && (
              <p className="text-xs text-destructive">{errors.name.message}</p>
            )}
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="es-start">Start Date</Label>
              <input
                id="es-start"
                type="date"
                className="flex h-11 w-full rounded-lg border border-border bg-card px-3 py-2 text-sm text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                {...register('startDate')}
              />
              {errors.startDate && (
                <p className="text-xs text-destructive">{errors.startDate.message}</p>
              )}
            </div>
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="es-end">End Date</Label>
              <input
                id="es-end"
                type="date"
                className="flex h-11 w-full rounded-lg border border-border bg-card px-3 py-2 text-sm text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                {...register('endDate')}
              />
              {errors.endDate && (
                <p className="text-xs text-destructive">{errors.endDate.message}</p>
              )}
            </div>
          </div>

          <DialogFooter className="pt-2">
            <Button type="button" variant="outline" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" disabled={isSubmitting}>
              {isSubmitting ? 'Saving…' : 'Save Changes'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
