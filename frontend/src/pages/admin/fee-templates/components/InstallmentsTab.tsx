import { useState, useEffect } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { isAxiosError } from 'axios'
import { Plus, Trash2 } from 'lucide-react'
import { Button } from '../../../../components/ui/button'
import { Input } from '../../../../components/ui/input'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '../../../../components/ui/table'
import { feeTemplatesApi, FEE_TEMPLATE_KEYS } from '../../../../api/feeTemplates'
import type { FeeTemplateDto, InstallmentInput } from '../../../../api/feeTemplates'

function extractError(err: unknown): string {
  if (isAxiosError(err) && err.response?.data?.error) return err.response.data.error
  return 'An unexpected error occurred.'
}

interface InstallmentsTabProps {
  template: FeeTemplateDto | undefined
  isEditMode: boolean
  onDirtyChange: (dirty: boolean) => void
  templateId: string
}

export function InstallmentsTab({ template, isEditMode, onDirtyChange, templateId }: InstallmentsTabProps) {
  const queryClient = useQueryClient()
  const [localItems, setLocalItems] = useState<InstallmentInput[]>([])
  const [savedItems, setSavedItems] = useState<InstallmentInput[]>([])

  useEffect(() => {
    if (!template) return
    const items: InstallmentInput[] = template.installments.map((inst) => ({
      name: inst.name,
      percentage: inst.percentage,
      dueLabel: inst.dueLabel ?? '',
      displayOrder: inst.displayOrder,
    }))
    setLocalItems(items)
    setSavedItems(items)
  }, [template])

  const isDirty = JSON.stringify(localItems) !== JSON.stringify(savedItems)

  useEffect(() => {
    onDirtyChange(isDirty)
  }, [isDirty, onDirtyChange])

  const totalPct = localItems.reduce((sum, i) => sum + (i.percentage || 0), 0)
  const sumOk = localItems.length === 0 || Math.abs(totalPct - 100) < 0.01

  const updateItem = (idx: number, field: keyof InstallmentInput, value: string | number) => {
    setLocalItems((prev) => prev.map((item, i) => i === idx ? { ...item, [field]: value } : item))
  }

  const addItem = () => {
    setLocalItems((prev) => [
      ...prev,
      { name: '', percentage: 0, dueLabel: '', displayOrder: prev.length + 1 },
    ])
  }

  const removeItem = (idx: number) => {
    setLocalItems((prev) => prev.filter((_, i) => i !== idx))
  }

  const handleDiscard = () => {
    setLocalItems(savedItems)
  }

  const saveInstallmentsMutation = useMutation({
    mutationFn: (items: InstallmentInput[]) => feeTemplatesApi.replaceInstallments(templateId, items),
    onSuccess: (updated) => {
      queryClient.setQueryData(FEE_TEMPLATE_KEYS.detail(templateId), updated)
      queryClient.invalidateQueries({ queryKey: ['fee-templates', 'list'] })
      const newItems: InstallmentInput[] = updated.installments.map((inst) => ({
        name: inst.name,
        percentage: inst.percentage,
        dueLabel: inst.dueLabel ?? '',
        displayOrder: inst.displayOrder,
      }))
      setLocalItems(newItems)
      setSavedItems(newItems)
      toast.success('Installments saved')
    },
    onError: (err) => toast.error(extractError(err)),
  })

  const percentageSummary = (
    <div className={`flex items-center gap-2 text-sm ${sumOk ? 'text-muted-foreground' : 'text-destructive'}`}>
      <span>Total: <strong>{totalPct.toFixed(2)}%</strong> / 100%</span>
      {!sumOk && localItems.length > 0 && (
        <span className="text-xs">(must equal 100%)</span>
      )}
    </div>
  )

  if (!isEditMode) {
    return (
      <div className="mt-4 flex flex-col gap-3">
        {percentageSummary}
        <div className="rounded-lg border border-border overflow-hidden">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Name</TableHead>
                <TableHead>Percentage</TableHead>
                <TableHead>Due Label</TableHead>
                <TableHead>Order</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {localItems.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={4} className="h-24 text-center text-muted-foreground text-sm">
                    No installments yet.
                  </TableCell>
                </TableRow>
              ) : (
                localItems.map((item, idx) => (
                  <TableRow key={idx}>
                    <TableCell className="font-medium">{item.name}</TableCell>
                    <TableCell className="font-mono text-sm">{item.percentage.toFixed(2)}%</TableCell>
                    <TableCell className="text-sm text-muted-foreground">{item.dueLabel || '—'}</TableCell>
                    <TableCell className="text-sm text-muted-foreground">{item.displayOrder}</TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>
        </div>
      </div>
    )
  }

  return (
    <div className="mt-4 flex flex-col gap-4">
      {percentageSummary}

      <div className="rounded-lg border border-border overflow-hidden">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Name</TableHead>
              <TableHead>Percentage (%)</TableHead>
              <TableHead>Due Label</TableHead>
              <TableHead>Order</TableHead>
              <TableHead className="w-12" />
            </TableRow>
          </TableHeader>
          <TableBody>
            {localItems.length === 0 ? (
              <TableRow>
                <TableCell colSpan={5} className="h-24 text-center text-muted-foreground text-sm">
                  No installments yet. Add one below.
                </TableCell>
              </TableRow>
            ) : (
              localItems.map((item, idx) => (
                <TableRow key={idx}>
                  <TableCell>
                    <Input
                      value={item.name}
                      onChange={(e) => updateItem(idx, 'name', e.target.value)}
                      className="h-8"
                    />
                  </TableCell>
                  <TableCell>
                    <Input
                      type="number"
                      min={0}
                      max={100}
                      step="0.01"
                      value={item.percentage}
                      onChange={(e) => updateItem(idx, 'percentage', parseFloat(e.target.value) || 0)}
                      className="h-8 w-24"
                    />
                  </TableCell>
                  <TableCell>
                    <Input
                      placeholder="e.g. Upon enrollment"
                      value={item.dueLabel}
                      onChange={(e) => updateItem(idx, 'dueLabel', e.target.value)}
                      className="h-8"
                    />
                  </TableCell>
                  <TableCell>
                    <Input
                      type="number"
                      value={item.displayOrder}
                      onChange={(e) => updateItem(idx, 'displayOrder', parseInt(e.target.value) || 0)}
                      className="h-8 w-16"
                    />
                  </TableCell>
                  <TableCell>
                    <Button size="sm" variant="ghost" onClick={() => removeItem(idx)}>
                      <Trash2 size={14} className="text-destructive" />
                    </Button>
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </div>

      <div>
        <Button size="sm" variant="outline" onClick={addItem}>
          <Plus size={14} className="mr-1" /> Add Installment
        </Button>
      </div>

      <div className="flex items-center justify-end gap-2">
        <Button variant="outline" size="sm" onClick={handleDiscard}>Discard</Button>
        <Button
          size="sm"
          disabled={!isDirty || saveInstallmentsMutation.isPending || (!sumOk && localItems.length > 0)}
          onClick={() => saveInstallmentsMutation.mutate(localItems)}
        >
          {saveInstallmentsMutation.isPending ? 'Saving…' : 'Save Changes'}
        </Button>
      </div>
    </div>
  )
}
