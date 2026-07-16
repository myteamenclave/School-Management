import { useState } from 'react'
import { useMutation } from '@tanstack/react-query'
import { toast } from 'sonner'
import { isAxiosError } from 'axios'
import { Send, FileText } from 'lucide-react'
import { Button } from '../../../../components/ui/button'
import { feeAssignmentsApi } from '../../../../api/feeAssignments'
import type { FeeTemplateDto } from '../../../../api/feeTemplates'
import { GenerateInvoicesDialog } from './GenerateInvoicesDialog'

function extractError(err: unknown): string {
  if (isAxiosError(err) && err.response?.data?.error) return err.response.data.error
  return 'An unexpected error occurred.'
}

interface InvoicingTabProps {
  template: FeeTemplateDto
}

export function InvoicingTab({ template }: InvoicingTabProps) {
  const [generateOpen, setGenerateOpen] = useState(false)

  const broadcastMutation = useMutation({
    mutationFn: () => feeAssignmentsApi.broadcast(template.id),
    onSuccess: (result) => {
      toast.success(
        `Assigned ${result.assigned} student${result.assigned !== 1 ? 's' : ''}. ` +
        `${result.skipped} already had an assignment and were skipped.`
      )
    },
    onError: (err) => toast.error(extractError(err)),
  })

  return (
    <div className="mt-6 flex flex-col gap-6 max-w-2xl">
      {/* Broadcast section */}
      <div className="rounded-lg border border-border bg-card px-6 py-5 flex flex-col gap-3">
        <div>
          <h3 className="text-base font-semibold text-foreground">Grade Assignment</h3>
          <p className="text-sm text-muted-foreground mt-1">
            Assign this template to all students enrolled in{' '}
            <span className="font-medium text-foreground">Grade {template.gradeName}</span> for{' '}
            <span className="font-medium text-foreground">year {template.academicYearName}</span> who
            don't yet have a fee assignment for this year.
          </p>
        </div>
        <div>
          <Button
            size="sm"
            disabled={broadcastMutation.isPending}
            onClick={() => broadcastMutation.mutate()}
          >
            <Send size={14} className="mr-1.5" />
            {broadcastMutation.isPending ? 'Assigning…' : 'Broadcast to Grade'}
          </Button>
        </div>
      </div>

      {/* Generate section */}
      <div className="rounded-lg border border-border bg-card px-6 py-5 flex flex-col gap-3">
        <div>
          <h3 className="text-base font-semibold text-foreground">Generate Draft Invoices</h3>
          <p className="text-sm text-muted-foreground mt-1">
            Generate a Draft invoice for each student assigned to this template. You'll enter due
            dates for each installment before generating.
          </p>
        </div>
        <div>
          <Button size="sm" variant="outline" onClick={() => setGenerateOpen(true)}>
            <FileText size={14} className="mr-1.5" /> Generate Invoices
          </Button>
        </div>
      </div>

      <GenerateInvoicesDialog
        open={generateOpen}
        template={template}
        onClose={() => setGenerateOpen(false)}
      />
    </div>
  )
}
