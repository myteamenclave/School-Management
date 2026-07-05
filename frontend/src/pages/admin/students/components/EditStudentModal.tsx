import { useEffect } from 'react'
import { useForm, Controller } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useMutation, useQuery } from '@tanstack/react-query'
import { toast } from 'sonner'
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
import { isAxiosError } from 'axios'
import { studentsApi, STUDENT_KEYS } from '../../../../api/students'
import { createSchema } from './CreateStudentModal'

function extractError(err: unknown): string {
  if (isAxiosError(err) && err.response?.data?.error) return err.response.data.error
  return 'An unexpected error occurred.'
}

const schema = createSchema.extend({
  enrollmentStatus: z.enum(['Active', 'Transferred', 'Graduated', 'Dropped'], {
    error: 'Required',
  }),
})

type FormValues = z.infer<typeof schema>

interface EditStudentModalProps {
  studentId: string | null
  onClose: () => void
  onUpdated: () => void
}

export function EditStudentModal({ studentId, onClose, onUpdated }: EditStudentModalProps) {
  const open = studentId !== null

  const { data: student, isLoading } = useQuery({
    queryKey: STUDENT_KEYS.detail(studentId ?? ''),
    queryFn: () => studentsApi.getById(studentId!),
    enabled: studentId !== null,
  })

  const {
    register,
    handleSubmit,
    reset,
    control,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({ resolver: zodResolver(schema) })

  useEffect(() => {
    if (student) {
      reset({
        firstName:        student.firstName,
        lastName:         student.lastName,
        dateOfBirth:      student.dateOfBirth,
        gender:           student.gender as 'Male' | 'Female' | 'Other',
        enrollmentDate:   student.enrollmentDate,
        enrollmentStatus: student.enrollmentStatus as 'Active' | 'Transferred' | 'Graduated' | 'Dropped',
        guardianName:     student.guardianName  ?? '',
        guardianPhone:    student.guardianPhone ?? '',
        guardianEmail:    student.guardianEmail ?? '',
      })
    }
  }, [student, reset])

  const mutation = useMutation({
    mutationFn: (data: FormValues) => studentsApi.update(studentId!, {
      firstName:        data.firstName,
      lastName:         data.lastName,
      dateOfBirth:      data.dateOfBirth,
      gender:           data.gender,
      enrollmentDate:   data.enrollmentDate,
      enrollmentStatus: data.enrollmentStatus,
      guardianName:     data.guardianName  || undefined,
      guardianPhone:    data.guardianPhone || undefined,
      guardianEmail:    data.guardianEmail || undefined,
    }),
    onSuccess: () => {
      toast.success('Student updated')
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
          <DialogTitle>Edit Student</DialogTitle>
          {student && (
            <p className="text-xs text-muted-foreground font-mono mt-0.5">
              {student.studentCode}
            </p>
          )}
        </DialogHeader>

        {isLoading ? (
          <div className="flex items-center justify-center h-48 text-sm text-muted-foreground">
            Loading…
          </div>
        ) : (
          <form onSubmit={handleSubmit(onSubmit)} className="flex flex-col gap-4 py-2">
            <div className="grid grid-cols-2 gap-4">
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="es-firstName">First Name</Label>
                <Input id="es-firstName" {...register('firstName')} />
                {errors.firstName && <p className="text-xs text-destructive">{errors.firstName.message}</p>}
              </div>
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="es-lastName">Last Name</Label>
                <Input id="es-lastName" {...register('lastName')} />
                {errors.lastName && <p className="text-xs text-destructive">{errors.lastName.message}</p>}
              </div>
            </div>

            <div className="grid grid-cols-2 gap-4">
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="es-dob">Date of Birth</Label>
                <input id="es-dob" type="date" className={dateInputClass} onInput={clampYear} {...register('dateOfBirth')} />
                {errors.dateOfBirth && <p className="text-xs text-destructive">{errors.dateOfBirth.message}</p>}
              </div>
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="es-gender">Gender</Label>
                <Controller
                  name="gender"
                  control={control}
                  render={({ field }) => (
                    <Select onValueChange={field.onChange} value={field.value}>
                      <SelectTrigger id="es-gender">
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
              <Label htmlFor="es-enrollDate">Enrollment Date</Label>
              <input id="es-enrollDate" type="date" className={dateInputClass} onInput={clampYear} {...register('enrollmentDate')} />
              {errors.enrollmentDate && <p className="text-xs text-destructive">{errors.enrollmentDate.message}</p>}
            </div>

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="es-status">Enrollment Status</Label>
              <Controller
                name="enrollmentStatus"
                control={control}
                render={({ field }) => (
                  <Select onValueChange={field.onChange} value={field.value}>
                    <SelectTrigger id="es-status">
                      <SelectValue placeholder="Select status" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="Active">Active</SelectItem>
                      <SelectItem value="Transferred">Transferred</SelectItem>
                      <SelectItem value="Graduated">Graduated</SelectItem>
                      <SelectItem value="Dropped">Dropped</SelectItem>
                    </SelectContent>
                  </Select>
                )}
              />
              {errors.enrollmentStatus && <p className="text-xs text-destructive">{errors.enrollmentStatus.message}</p>}
            </div>

            <div className="border-t border-border pt-4">
              <p className="text-xs font-medium text-muted-foreground uppercase tracking-wide mb-3">Guardian (optional)</p>
              <div className="flex flex-col gap-4">
                <div className="flex flex-col gap-1.5">
                  <Label htmlFor="es-guardianName">Guardian Name</Label>
                  <Input id="es-guardianName" {...register('guardianName')} />
                </div>
                <div className="grid grid-cols-2 gap-4">
                  <div className="flex flex-col gap-1.5">
                    <Label htmlFor="es-guardianPhone">Guardian Phone</Label>
                    <Input id="es-guardianPhone" {...register('guardianPhone')} />
                  </div>
                  <div className="flex flex-col gap-1.5">
                    <Label htmlFor="es-guardianEmail">Guardian Email</Label>
                    <Input id="es-guardianEmail" type="email" {...register('guardianEmail')} />
                    {errors.guardianEmail && <p className="text-xs text-destructive">{errors.guardianEmail.message}</p>}
                  </div>
                </div>
              </div>
            </div>

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
