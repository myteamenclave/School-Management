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
import type { FeeTemplateDto, LineItemInput } from '../../../../api/feeTemplates'

function extractError(err: unknown): string {
  if (isAxiosError(err) && err.response?.data?.error) return err.response.data.error
  return 'An unexpected error occurred.'
}

const currencyFmt = new Intl.NumberFormat('en-PH', {
  style: 'currency',
  currency: 'PHP',
  minimumFractionDigits: 2,
})

interface LineItemsTabProps {
  template: FeeTemplateDto | undefined
  isEditMode: boolean
  onDirtyChange: (dirty: boolean) => void
  templateId: string
}

export function LineItemsTab({ template, isEditMode, onDirtyChange, templateId }: LineItemsTabProps) {
  const queryClient = useQueryClient()
  const [localItems, setLocalItems] = useState<LineItemInput[]>([])
  const [savedItems, setSavedItems] = useState<LineItemInput[]>([])

  useEffect(() => {
    if (!template) return
    const items: LineItemInput[] = template.lineItems.map((li) => ({
      id: li.id,
      name: li.name,
      amount: li.amount,
      displayOrder: li.displayOrder,
    }))
    setLocalItems(items)
    setSavedItems(items)
  }, [template])

  const isDirty = JSON.stringify(localItems) !== JSON.stringify(savedItems)

  useEffect(() => {
    onDirtyChange(isDirty)
  }, [isDirty, onDirtyChange])

  const updateItem = (idx: number, field: keyof LineItemInput, value: string | number) => {
    setLocalItems((prev) => prev.map((item, i) => i === idx ? { ...item, [field]: value } : item))
  }

  const addItem = () => {
    setLocalItems((prev) => [
      ...prev,
      { name: '', amount: 0, displayOrder: prev.length + 1 },
    ])
  }

  const removeItem = (idx: number) => {
    setLocalItems((prev) => prev.filter((_, i) => i !== idx))
  }

  const handleDiscard = () => {
    setLocalItems(savedItems)
  }

  const saveLineItemsMutation = useMutation({
    mutationFn: (items: LineItemInput[]) => feeTemplatesApi.replaceLineItems(templateId, items),
    onSuccess: (updated) => {
      queryClient.setQueryData(FEE_TEMPLATE_KEYS.detail(templateId), updated)
      queryClient.invalidateQueries({ queryKey: ['fee-templates', 'list'] })
      const newItems: LineItemInput[] = updated.lineItems.map((li) => ({
        id: li.id,
        name: li.name,
        amount: li.amount,
        displayOrder: li.displayOrder,
      }))
      setLocalItems(newItems)
      setSavedItems(newItems)
      toast.success('Line items saved')
    },
    onError: (err) => toast.error(extractError(err)),
  })

  if (!isEditMode) {
    return (
      <div className="rounded-lg border border-border overflow-hidden mt-4">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Name</TableHead>
              <TableHead>Amount</TableHead>
              <TableHead>Order</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {localItems.length === 0 ? (
              <TableRow>
                <TableCell colSpan={3} className="h-24 text-center text-muted-foreground text-sm">
                  No line items yet.
                </TableCell>
              </TableRow>
            ) : (
              localItems.map((item, idx) => (
                <TableRow key={item.id ?? idx}>
                  <TableCell className="font-medium">{item.name}</TableCell>
                  <TableCell className="font-mono text-sm">{currencyFmt.format(item.amount)}</TableCell>
                  <TableCell className="text-sm text-muted-foreground">{item.displayOrder}</TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </div>
    )
  }

  return (
    <div className="mt-4 flex flex-col gap-4">
      <div className="rounded-lg border border-border overflow-hidden">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Name</TableHead>
              <TableHead>Amount</TableHead>
              <TableHead>Order</TableHead>
              <TableHead className="w-12" />
            </TableRow>
          </TableHeader>
          <TableBody>
            {localItems.length === 0 ? (
              <TableRow>
                <TableCell colSpan={4} className="h-24 text-center text-muted-foreground text-sm">
                  No line items yet. Add one below.
                </TableCell>
              </TableRow>
            ) : (
              localItems.map((item, idx) => (
                <TableRow key={item.id ?? idx}>
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
                      step="0.01"
                      value={item.amount}
                      onChange={(e) => updateItem(idx, 'amount', parseFloat(e.target.value) || 0)}
                      className="h-8 w-28"
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
          <Plus size={14} className="mr-1" /> Add Line Item
        </Button>
      </div>

      <div className="flex items-center justify-end gap-2">
        <Button variant="outline" size="sm" onClick={handleDiscard}>Discard</Button>
        <Button
          size="sm"
          disabled={!isDirty || saveLineItemsMutation.isPending}
          onClick={() => saveLineItemsMutation.mutate(localItems)}
        >
          {saveLineItemsMutation.isPending ? 'Saving…' : 'Save Changes'}
        </Button>
      </div>
    </div>
  )
}
