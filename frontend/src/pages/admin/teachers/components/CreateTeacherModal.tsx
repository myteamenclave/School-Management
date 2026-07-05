import { useForm } from 'react-hook-form'
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
import { teachersApi } from '../../../../api/teachers'

function extractError(err: unknown): string {
  if (isAxiosError(err) && err.response?.data?.error) return err.response.data.error
  return 'An unexpected error occurred.'
}

export const createTeacherSchema = z.object({
  firstName:   z.string().min(1, 'Required').max(100),
  lastName:    z.string().min(1, 'Required').max(100),
  email:       z.string().min(1, 'Required').email('Invalid email').max(256),
  password:    z.string().min(8, 'At least 8 characters').max(128),
  joiningDate: z.string().min(1, 'Required'),
  phone:       z.string().max(20).optional(),
})

type FormValues = z.infer<typeof createTeacherSchema>

interface CreateTeacherModalProps {
  open: boolean
  onClose: () => void
  onCreated: () => void
}

export function CreateTeacherModal({ open, onClose, onCreated }: CreateTeacherModalProps) {
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({ resolver: zodResolver(createTeacherSchema) })

  const mutation = useMutation({
    mutationFn: teachersApi.create,
    onSuccess: () => {
      toast.success('Teacher created')
      reset()
      onCreated()
      onClose()
    },
    onError: (err) => toast.error(extractError(err)),
  })

  const onSubmit = (data: FormValues) => mutation.mutate({
    firstName:   data.firstName,
    lastName:    data.lastName,
    email:       data.email,
    password:    data.password,
    joiningDate: data.joiningDate,
    phone:       data.phone || undefined,
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
          <DialogTitle>Add Teacher</DialogTitle>
        </DialogHeader>

        <form onSubmit={handleSubmit(onSubmit)} className="flex flex-col gap-4 py-2">
          <div className="grid grid-cols-2 gap-4">
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="ct-firstName">First Name</Label>
              <Input id="ct-firstName" {...register('firstName')} />
              {errors.firstName && <p className="text-xs text-destructive">{errors.firstName.message}</p>}
            </div>
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="ct-lastName">Last Name</Label>
              <Input id="ct-lastName" {...register('lastName')} />
              {errors.lastName && <p className="text-xs text-destructive">{errors.lastName.message}</p>}
            </div>
          </div>

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="ct-email">Email</Label>
            <Input id="ct-email" {...register('email')} />
            {errors.email && <p className="text-xs text-destructive">{errors.email.message}</p>}
          </div>

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="ct-password">Password</Label>
            <Input id="ct-password" type="password" {...register('password')} />
            {errors.password && <p className="text-xs text-destructive">{errors.password.message}</p>}
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="ct-joiningDate">Joining Date</Label>
              <input id="ct-joiningDate" type="date" className={dateInputClass} onInput={clampYear} {...register('joiningDate')} />
              {errors.joiningDate && <p className="text-xs text-destructive">{errors.joiningDate.message}</p>}
            </div>
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="ct-phone">Phone</Label>
              <Input id="ct-phone" {...register('phone')} />
              {errors.phone && <p className="text-xs text-destructive">{errors.phone.message}</p>}
            </div>
          </div>

          <DialogFooter className="pt-2">
            <Button type="button" variant="outline" onClick={onClose}>Cancel</Button>
            <Button type="submit" disabled={isSubmitting || mutation.isPending}>
              {isSubmitting || mutation.isPending ? 'Creating…' : 'Create Teacher'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
