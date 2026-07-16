import { useNavigate } from 'react-router-dom'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useMutation } from '@tanstack/react-query'
import { toast } from 'sonner'
import { isAxiosError } from 'axios'
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter,
} from '../../../../components/ui/dialog'
import { Button } from '../../../../components/ui/button'
import { Input } from '../../../../components/ui/input'
import { Label } from '../../../../components/ui/label'
import { feeInvoicesApi } from '../../../../api/feeInvoices'
import type { FeeTemplateDto } from '../../../../api/feeTemplates'

function extractError(err: unknown): string {
  if (isAxiosError(err) && err.response?.data?.error) return err.response.data.error
  return 'An unexpected error occurred.'
}

const schema = z.object({
  dueDates: z.array(z.object({
    templateInstallmentId: z.string(),
    dueDate: z.string().min(1, 'Required'),
  })),
})
type FormValues = z.infer<typeof schema>

interface GenerateInvoicesDialogProps {
  open: boolean
  template: FeeTemplateDto
  onClose: () => void
}

export function GenerateInvoicesDialog({ open, template, onClose }: GenerateInvoicesDialogProps) {
  const navigate = useNavigate()

  const { register, handleSubmit, reset, formState: { errors } } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      dueDates: template.installments.map((inst) => ({
        templateInstallmentId: inst.id,
        dueDate: '',
      })),
    },
  })

  const generateMutation = useMutation({
    mutationFn: (values: FormValues) =>
      feeInvoicesApi.generate({
        gradeId: template.gradeId,
        academicYearId: template.academicYearId,
        installmentDueDates: values.dueDates,
      }),
    onSuccess: (result) => {
      toast.success(
        `Generated ${result.generated} invoice${result.generated !== 1 ? 's' : ''}. ` +
        (result.skipped > 0 ? `${result.skipped} skipped (already had active invoices).` : '')
      )
      reset()
      onClose()
      navigate(
        `/admin/fee-invoices?academicYearId=${template.academicYearId}&gradeId=${template.gradeId}`
      )
    },
    onError: (err) => toast.error(extractError(err)),
  })

  const handleClose = () => {
    reset()
    onClose()
  }

  return (
    <Dialog open={open} onOpenChange={(v) => { if (!v) handleClose() }}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>Generate Invoices — {template.name}</DialogTitle>
        </DialogHeader>

        <div className="flex flex-col gap-1 text-sm">
          <div className="flex items-center gap-2">
            <span className="text-muted-foreground w-24">Grade</span>
            <span className="font-medium text-foreground">{template.gradeName}</span>
          </div>
          <div className="flex items-center gap-2">
            <span className="text-muted-foreground w-24">Academic Year</span>
            <span className="font-medium text-foreground">{template.academicYearName}</span>
          </div>
        </div>

        <form
          id="generate-form"
          onSubmit={handleSubmit((v) => generateMutation.mutate(v))}
          className="flex flex-col gap-4 mt-2"
        >
          {template.installments.length === 0 ? (
            <p className="text-sm text-muted-foreground">
              This template has no installments. Invoices will be generated without an installment
              schedule.
            </p>
          ) : (
            <div className="flex flex-col gap-3">
              <p className="text-sm text-muted-foreground">Enter a due date for each installment.</p>
              {template.installments.map((inst, idx) => (
                <div key={inst.id} className="flex items-center gap-3">
                  <div className="flex-1">
                    <Label className="text-sm font-medium">
                      {inst.name}{' '}
                      <span className="font-normal text-muted-foreground">({inst.percentage}%)</span>
                    </Label>
                  </div>
                  <div className="flex flex-col gap-1">
                    <input
                      type="hidden"
                      {...register(`dueDates.${idx}.templateInstallmentId`)}
                    />
                    <Input
                      type="date"
                      className="w-44"
                      {...register(`dueDates.${idx}.dueDate`)}
                    />
                    {errors.dueDates?.[idx]?.dueDate && (
                      <p className="text-xs text-destructive">
                        {errors.dueDates[idx].dueDate?.message}
                      </p>
                    )}
                  </div>
                </div>
              ))}
            </div>
          )}
        </form>

        <DialogFooter>
          <Button variant="outline" onClick={handleClose} disabled={generateMutation.isPending}>
            Cancel
          </Button>
          <Button
            type="submit"
            form="generate-form"
            disabled={generateMutation.isPending}
          >
            {generateMutation.isPending ? 'Generating…' : 'Generate'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
