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
import { enrollmentsApi } from '../../../../api/enrollments'
import type { EnrollmentDto } from '../../../../api/enrollments'

interface TransferStudentModalProps {
  enrollment: EnrollmentDto | null
  onClose: () => void
  onTransferred: () => void
}

const schema = z.object({
  sectionId: z.string().uuid('Select a section'),
})

type FormValues = z.infer<typeof schema>

function extractError(err: unknown): string {
  if (isAxiosError(err) && err.response?.data?.error) return err.response.data.error
  return 'An unexpected error occurred.'
}

export function TransferStudentModal({ enrollment, onClose, onTransferred }: TransferStudentModalProps) {
  const { data: grades = [] } = useQuery({
    queryKey: ['grades'],
    queryFn: gradesApi.list,
    enabled: enrollment !== null,
  })

  const allSections = grades.flatMap((g) =>
    g.sections
      .filter((s) => s.id !== enrollment?.sectionId)
      .map((s) => ({ id: s.id, label: `${g.name} — ${s.name}` }))
  )

  const {
    handleSubmit,
    control,
    reset,
    formState: { errors },
  } = useForm<FormValues>({ resolver: zodResolver(schema) })

  const transferMutation = useMutation({
    mutationFn: (data: FormValues) =>
      enrollmentsApi.transfer(enrollment!.id, { sectionId: data.sectionId }),
    onSuccess: () => {
      toast.success('Student transferred')
      reset()
      onTransferred()
      onClose()
    },
    onError: (err) => toast.error(extractError(err)),
  })

  const handleOpenChange = (open: boolean) => {
    if (!open) { reset(); onClose() }
  }

  return (
    <Dialog open={enrollment !== null} onOpenChange={handleOpenChange}>
      <DialogContent className="sm:max-w-sm" onInteractOutside={(e) => e.preventDefault()}>
        <DialogHeader>
          <DialogTitle>Transfer Student</DialogTitle>
        </DialogHeader>

        <form onSubmit={handleSubmit((d) => transferMutation.mutate(d))} className="flex flex-col gap-4 py-2">
          {enrollment && (
            <div className="flex flex-col gap-1">
              <p className="text-sm font-medium text-foreground">
                {enrollment.studentFirstName} {enrollment.studentLastName}
              </p>
              <p className="text-xs text-muted-foreground">
                Currently in: <span className="font-medium">{enrollment.gradeName} — {enrollment.sectionName}</span>
              </p>
            </div>
          )}

          <div className="flex flex-col gap-1.5">
            <Label>Transfer to</Label>
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
            <Button type="submit" disabled={transferMutation.isPending}>
              {transferMutation.isPending ? 'Transferring…' : 'Transfer'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
