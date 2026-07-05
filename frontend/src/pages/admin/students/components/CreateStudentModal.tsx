import { useForm, Controller } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useMutation } from '@tanstack/react-query'
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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '../../../../components/ui/select'
import { studentsApi } from '../../../../api/students'

function extractError(err: unknown): string {
  if (isAxiosError(err) && err.response?.data?.error) return err.response.data.error
  return 'An unexpected error occurred.'
}

export const createSchema = z.object({
  firstName:     z.string().min(1, 'Required').max(100),
  lastName:      z.string().min(1, 'Required').max(100),
  dateOfBirth:   z.string().min(1, 'Required'),
  gender:        z.enum(['Male', 'Female', 'Other'], { error: 'Required' }),
  enrollmentDate: z.string().min(1, 'Required'),
  guardianName:  z.string().max(200).optional(),
  guardianPhone: z.string().max(20).optional(),
  guardianEmail: z.string().email('Invalid email').max(256).optional().or(z.literal('')),
})

type FormValues = z.infer<typeof createSchema>

interface CreateStudentModalProps {
  open: boolean
  onClose: () => void
  onCreated: () => void
}

export function CreateStudentModal({ open, onClose, onCreated }: CreateStudentModalProps) {
  const {
    register,
    handleSubmit,
    reset,
    control,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({ resolver: zodResolver(createSchema) })

  const mutation = useMutation({
    mutationFn: studentsApi.create,
    onSuccess: () => {
      toast.success('Student created')
      reset()
      onCreated()
      onClose()
    },
    onError: (err) => toast.error(extractError(err)),
  })

  const onSubmit = (data: FormValues) => mutation.mutate({
    ...data,
    guardianName:  data.guardianName  || undefined,
    guardianPhone: data.guardianPhone || undefined,
    guardianEmail: data.guardianEmail || undefined,
  })

  const handleOpenChange = (open: boolean) => {
    if (!open) { reset(); onClose() }
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
          <DialogTitle>Add Student</DialogTitle>
        </DialogHeader>

        <form onSubmit={handleSubmit(onSubmit)} className="flex flex-col gap-4 py-2">
          <div className="grid grid-cols-2 gap-4">
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="cs-firstName">First Name</Label>
              <Input id="cs-firstName" {...register('firstName')} />
              {errors.firstName && <p className="text-xs text-destructive">{errors.firstName.message}</p>}
            </div>
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="cs-lastName">Last Name</Label>
              <Input id="cs-lastName" {...register('lastName')} />
              {errors.lastName && <p className="text-xs text-destructive">{errors.lastName.message}</p>}
            </div>
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="cs-dob">Date of Birth</Label>
              <input id="cs-dob" type="date" className={dateInputClass} onInput={clampYear} {...register('dateOfBirth')} />
              {errors.dateOfBirth && <p className="text-xs text-destructive">{errors.dateOfBirth.message}</p>}
            </div>
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="cs-gender">Gender</Label>
              <Controller
                name="gender"
                control={control}
                render={({ field }) => (
                  <Select onValueChange={field.onChange} value={field.value}>
                    <SelectTrigger id="cs-gender">
                      <SelectValue placeholder="Select gender" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="Male">Male</SelectItem>
                      <SelectItem value="Female">Female</SelectItem>
                      <SelectItem value="Other">Other</SelectItem>
                    </SelectContent>
                  </Select>
                )}
              />
              {errors.gender && <p className="text-xs text-destructive">{errors.gender.message}</p>}
            </div>
          </div>

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="cs-enrollDate">Enrollment Date</Label>
            <input id="cs-enrollDate" type="date" className={dateInputClass} onInput={clampYear} {...register('enrollmentDate')} />
            {errors.enrollmentDate && <p className="text-xs text-destructive">{errors.enrollmentDate.message}</p>}
          </div>

          <div className="border-t border-border pt-4">
            <p className="text-xs font-medium text-muted-foreground uppercase tracking-wide mb-3">Guardian (optional)</p>
            <div className="flex flex-col gap-4">
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="cs-guardianName">Guardian Name</Label>
                <Input id="cs-guardianName" {...register('guardianName')} />
              </div>
              <div className="grid grid-cols-2 gap-4">
                <div className="flex flex-col gap-1.5">
                  <Label htmlFor="cs-guardianPhone">Guardian Phone</Label>
                  <Input id="cs-guardianPhone" {...register('guardianPhone')} />
                </div>
                <div className="flex flex-col gap-1.5">
                  <Label htmlFor="cs-guardianEmail">Guardian Email</Label>
                  <Input id="cs-guardianEmail" type="email" {...register('guardianEmail')} />
                  {errors.guardianEmail && <p className="text-xs text-destructive">{errors.guardianEmail.message}</p>}
                </div>
              </div>
            </div>
          </div>

          <DialogFooter className="pt-2">
            <Button type="button" variant="outline" onClick={onClose}>Cancel</Button>
            <Button type="submit" disabled={isSubmitting || mutation.isPending}>
              {isSubmitting || mutation.isPending ? 'Creating…' : 'Create Student'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
