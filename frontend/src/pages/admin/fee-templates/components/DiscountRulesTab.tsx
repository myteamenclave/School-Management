import { useState, useEffect } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { isAxiosError } from 'axios'
import { Plus, Trash2 } from 'lucide-react'
import { Button } from '../../../../components/ui/button'
import { Input } from '../../../../components/ui/input'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '../../../../components/ui/select'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '../../../../components/ui/table'
import { feeTemplatesApi, FEE_TEMPLATE_KEYS } from '../../../../api/feeTemplates'
import type { FeeTemplateDto, DiscountRuleInput, DiscountRuleType } from '../../../../api/feeTemplates'

function extractError(err: unknown): string {
  if (isAxiosError(err) && err.response?.data?.error) return err.response.data.error
  return 'An unexpected error occurred.'
}

interface DiscountRulesTabProps {
  template: FeeTemplateDto | undefined
  isEditMode: boolean
  onDirtyChange: (dirty: boolean) => void
  templateId: string
}

export function DiscountRulesTab({ template, isEditMode, onDirtyChange, templateId }: DiscountRulesTabProps) {
  const queryClient = useQueryClient()
  const [localItems, setLocalItems] = useState<DiscountRuleInput[]>([])
  const [savedItems, setSavedItems] = useState<DiscountRuleInput[]>([])

  useEffect(() => {
    if (!template) return
    const items: DiscountRuleInput[] = template.discountRules.map((dr) => ({
      name: dr.name,
      ruleType: dr.ruleType,
      value: dr.value,
      feeLineItemId: dr.feeLineItemId ?? undefined,
    }))
    setLocalItems(items)
    setSavedItems(items)
  }, [template])

  const isDirty = JSON.stringify(localItems) !== JSON.stringify(savedItems)

  useEffect(() => {
    onDirtyChange(isDirty)
  }, [isDirty, onDirtyChange])

  const updateItem = <K extends keyof DiscountRuleInput>(idx: number, field: K, value: DiscountRuleInput[K]) => {
    setLocalItems((prev) => prev.map((item, i) => i === idx ? { ...item, [field]: value } : item))
  }

  const addItem = () => {
    setLocalItems((prev) => [
      ...prev,
      { name: '', ruleType: 'Percentage', value: 0, feeLineItemId: undefined },
    ])
  }

  const removeItem = (idx: number) => {
    setLocalItems((prev) => prev.filter((_, i) => i !== idx))
  }

  const handleDiscard = () => {
    setLocalItems(savedItems)
  }

  const saveDiscountRulesMutation = useMutation({
    mutationFn: (items: DiscountRuleInput[]) => feeTemplatesApi.replaceDiscountRules(templateId, items),
    onSuccess: (updated) => {
      queryClient.setQueryData(FEE_TEMPLATE_KEYS.detail(templateId), updated)
      queryClient.invalidateQueries({ queryKey: ['fee-templates', 'list'] })
      const newItems: DiscountRuleInput[] = updated.discountRules.map((dr) => ({
        name: dr.name,
        ruleType: dr.ruleType,
        value: dr.value,
        feeLineItemId: dr.feeLineItemId ?? undefined,
      }))
      setLocalItems(newItems)
      setSavedItems(newItems)
      toast.success('Discount rules saved')
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
              <TableHead>Type</TableHead>
              <TableHead>Value</TableHead>
              <TableHead>Target</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {localItems.length === 0 ? (
              <TableRow>
                <TableCell colSpan={4} className="h-24 text-center text-muted-foreground text-sm">
                  No discount rules yet.
                </TableCell>
              </TableRow>
            ) : (
              localItems.map((item, idx) => {
                const lineItem = template?.discountRules[idx]
                return (
                  <TableRow key={idx}>
                    <TableCell className="font-medium">{item.name}</TableCell>
                    <TableCell>
                      <span className="inline-flex items-center rounded-md bg-muted px-2 py-0.5 text-xs font-medium text-muted-foreground">
                        {item.ruleType === 'Percentage' ? '%' : 'Flat'}
                      </span>
                    </TableCell>
                    <TableCell className="font-mono text-sm">
                      {item.ruleType === 'Percentage' ? `${item.value}%` : `₱${item.value.toFixed(2)}`}
                    </TableCell>
                    <TableCell className="text-sm text-muted-foreground">
                      {lineItem?.feeLineItemName ?? 'Invoice total'}
                    </TableCell>
                  </TableRow>
                )
              })
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
              <TableHead>Type</TableHead>
              <TableHead>Value</TableHead>
              <TableHead>Target Line Item</TableHead>
              <TableHead className="w-12" />
            </TableRow>
          </TableHeader>
          <TableBody>
            {localItems.length === 0 ? (
              <TableRow>
                <TableCell colSpan={5} className="h-24 text-center text-muted-foreground text-sm">
                  No discount rules yet. Add one below.
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
                    <Select
                      value={item.ruleType}
                      onValueChange={(v) => updateItem(idx, 'ruleType', v as DiscountRuleType)}
                    >
                      <SelectTrigger className="w-24 h-8">
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="Percentage">%</SelectItem>
                        <SelectItem value="FlatAmount">Flat</SelectItem>
                      </SelectContent>
                    </Select>
                  </TableCell>
                  <TableCell>
                    <Input
                      type="number"
                      min={0.01}
                      max={item.ruleType === 'Percentage' ? 100 : undefined}
                      step="0.01"
                      value={item.value}
                      onChange={(e) => updateItem(idx, 'value', parseFloat(e.target.value) || 0)}
                      className="h-8 w-24"
                    />
                  </TableCell>
                  <TableCell>
                    <Select
                      value={item.feeLineItemId ?? 'none'}
                      onValueChange={(v) => updateItem(idx, 'feeLineItemId', v === 'none' ? undefined : v)}
                    >
                      <SelectTrigger className="w-44 h-8">
                        <SelectValue placeholder="Invoice total" />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="none">Invoice total</SelectItem>
                        {template?.lineItems.map((li) => (
                          <SelectItem key={li.id} value={li.id}>{li.name}</SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
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
          <Plus size={14} className="mr-1" /> Add Discount Rule
        </Button>
      </div>

      <div className="flex items-center justify-end gap-2">
        <Button variant="outline" size="sm" onClick={handleDiscard}>Discard</Button>
        <Button
          size="sm"
          disabled={!isDirty || saveDiscountRulesMutation.isPending}
          onClick={() => saveDiscountRulesMutation.mutate(localItems)}
        >
          {saveDiscountRulesMutation.isPending ? 'Saving…' : 'Save Changes'}
        </Button>
      </div>
    </div>
  )
}
