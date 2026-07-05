import { useEffect } from 'react'
import { useForm, Controller } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useMutation, useQuery } from '@tanstack/react-query'
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
import { teachersApi, TEACHER_KEYS } from '../../../../api/teachers'

function extractError(err: unknown): string {
  if (isAxiosError(err) && err.response?.data?.error) return err.response.data.error
  return 'An unexpected error occurred.'
}

const schema = z.object({
  firstName:   z.string().min(1, 'Required').max(100),
  lastName:    z.string().min(1, 'Required').max(100),
  joiningDate: z.string().min(1, 'Required'),
  phone:       z.string().max(20).optional(),
  isActive:    z.boolean(),
})

type FormValues = z.infer<typeof schema>

interface EditTeacherModalProps {
  teacherId: string | null
  onClose: () => void
  onUpdated: () => void
}

export function EditTeacherModal({ teacherId, onClose, onUpdated }: EditTeacherModalProps) {
  const open = teacherId !== null

  const { data: teacher, isLoading } = useQuery({
    queryKey: TEACHER_KEYS.detail(teacherId ?? ''),
    queryFn: () => teachersApi.getById(teacherId!),
    enabled: teacherId !== null,
  })

  const {
    register,
    handleSubmit,
    reset,
    control,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({ resolver: zodResolver(schema) })

  useEffect(() => {
    if (teacher) {
      reset({
        firstName:   teacher.firstName,
        lastName:    teacher.lastName,
        joiningDate: teacher.joiningDate,
        phone:       teacher.phone ?? '',
        isActive:    teacher.isActive,
      })
    }
  }, [teacher, reset])

  const mutation = useMutation({
    mutationFn: (data: FormValues) => teachersApi.update(teacherId!, {
      firstName:   data.firstName,
      lastName:    data.lastName,
      joiningDate: data.joiningDate,
      phone:       data.phone || undefined,
      isActive:    data.isActive,
    }),
    onSuccess: () => {
      toast.success('Teacher updated')
      onUpdated()
      onClose()
    },
    onError: (err) => toast.error(extractError(err)),
  })

  const onSubmit = (data: FormValues) => mutation.mutate(data)

  const handleOpenChange = (open: boolean) => {
    if (!open) onClose()
  }

  const dateInputClass = 'flex h-11 w-full rounded-lg border border-border bg-card px-3 py-2 text-sm text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring'

  const clampYear = (e: React.FormEvent<HTMLInputElement>) => {
    const input = e.currentTarget
    if (!input.value) return
    const [year, ...rest] = input.value.split('-')
    if (year.length > 4) input.value = year.slice(0, 4) + (rest.length ? '-' + rest.join('-') : '')
  }

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent className="modal-fade-in sm:max-w-lg" onInteractOutside={(e) => e.preventDefault()}>
        <DialogHeader>
          <DialogTitle>Edit Teacher</DialogTitle>
          {teacher && (
            <p className="text-xs text-muted-foreground font-mono mt-0.5">
              {teacher.teacherCode}
            </p>
          )}
        </DialogHeader>

        {teacher && (
          <p className="text-sm text-muted-foreground -mt-2 mb-2">{teacher.email}</p>
        )}

        {isLoading ? (
          <div className="flex items-center justify-center h-48 text-sm text-muted-foreground">
            Loading…
          </div>
        ) : (
          <form onSubmit={handleSubmit(onSubmit)} className="flex flex-col gap-4 py-2">
            <div className="grid grid-cols-2 gap-4">
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="et-firstName">First Name</Label>
                <Input id="et-firstName" {...register('firstName')} />
                {errors.firstName && <p className="text-xs text-destructive">{errors.firstName.message}</p>}
              </div>
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="et-lastName">Last Name</Label>
                <Input id="et-lastName" {...register('lastName')} />
                {errors.lastName && <p className="text-xs text-destructive">{errors.lastName.message}</p>}
              </div>
            </div>

            <div className="grid grid-cols-2 gap-4">
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="et-joiningDate">Joining Date</Label>
                <input id="et-joiningDate" type="date" className={dateInputClass} onInput={clampYear} {...register('joiningDate')} />
                {errors.joiningDate && <p className="text-xs text-destructive">{errors.joiningDate.message}</p>}
              </div>
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="et-phone">Phone</Label>
                <Input id="et-phone" {...register('phone')} />
                {errors.phone && <p className="text-xs text-destructive">{errors.phone.message}</p>}
              </div>
            </div>

            <Controller
              name="isActive"
              control={control}
              render={({ field }) => (
                <div className="flex items-center gap-2">
                  <input
                    type="checkbox"
                    id="et-isActive"
                    checked={field.value ?? false}
                    onChange={(e) => field.onChange(e.target.checked)}
                    className="h-4 w-4 rounded border-border accent-primary cursor-pointer"
                  />
                  <label htmlFor="et-isActive" className="text-sm text-foreground cursor-pointer">
                    Active (unchecking disables login)
                  </label>
                </div>
              )}
            />

            <DialogFooter className="pt-2">
              <Button type="button" variant="outline" onClick={onClose}>Cancel</Button>
              <Button type="submit" disabled={isSubmitting || mutation.isPending}>
                {isSubmitting || mutation.isPending ? 'Saving…' : 'Save Changes'}
              </Button>
            </DialogFooter>
          </form>
        )}
      </DialogContent>
    </Dialog>
  )
}
