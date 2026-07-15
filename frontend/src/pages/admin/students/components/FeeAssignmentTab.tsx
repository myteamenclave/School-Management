import { useState, useEffect } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { isAxiosError } from 'axios'
import { Plus, Trash2 } from 'lucide-react'
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '../../../../components/ui/select'
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '../../../../components/ui/table'
import { Button } from '../../../../components/ui/button'
import { academicYearsApi, ACADEMIC_YEAR_KEYS } from '../../../../api/academicYears'
import { feeAssignmentsApi, FEE_ASSIGNMENT_KEYS } from '../../../../api/feeAssignments'
import { SetFeeAssignmentModal } from './SetFeeAssignmentModal'
import { AddDiscountModal } from './AddDiscountModal'

interface FeeAssignmentTabProps {
  studentId: string
}

function extractError(err: unknown): string {
  if (isAxiosError(err) && err.response?.data?.error) return err.response.data.error
  return 'An unexpected error occurred.'
}

export function FeeAssignmentTab({ studentId }: FeeAssignmentTabProps) {
  const queryClient = useQueryClient()
  const [selectedYearId, setSelectedYearId] = useState('')
  const [assignOpen, setAssignOpen] = useState(false)
  const [addDiscountOpen, setAddDiscountOpen] = useState(false)

  const { data: years = [] } = useQuery({
    queryKey: ACADEMIC_YEAR_KEYS.all,
    queryFn: academicYearsApi.list,
  })

  useEffect(() => {
    if (years.length > 0 && !selectedYearId) {
      const active = years.find((y) => y.status === 'Active')
      setSelectedYearId(active?.id ?? years[0].id)
    }
  }, [years, selectedYearId])

  const { data: assignment, isLoading: assignmentLoading } = useQuery({
    queryKey: FEE_ASSIGNMENT_KEYS.studentAssignment(studentId, selectedYearId),
    queryFn: () => feeAssignmentsApi.getStudentAssignment(studentId, selectedYearId),
    enabled: !!selectedYearId,
  })

  const { data: discounts = [], isLoading: discountsLoading } = useQuery({
    queryKey: FEE_ASSIGNMENT_KEYS.studentDiscounts(studentId, selectedYearId),
    queryFn: () => feeAssignmentsApi.getStudentDiscounts(studentId, selectedYearId),
    enabled: !!selectedYearId && !!assignment,
  })

  const removeMutation = useMutation({
    mutationFn: () => feeAssignmentsApi.removeStudentAssignment(studentId, selectedYearId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: FEE_ASSIGNMENT_KEYS.studentAssignment(studentId, selectedYearId) })
      queryClient.invalidateQueries({ queryKey: FEE_ASSIGNMENT_KEYS.studentDiscounts(studentId, selectedYearId) })
      toast.success('Fee assignment removed')
    },
    onError: (err) => toast.error(extractError(err)),
  })

  const removeDiscountMutation = useMutation({
    mutationFn: (id: string) => feeAssignmentsApi.removeStudentDiscount(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: FEE_ASSIGNMENT_KEYS.studentDiscounts(studentId, selectedYearId) })
      toast.success('Discount removed')
    },
    onError: (err) => toast.error(extractError(err)),
  })

  const handleRemoveAssignment = () => {
    if (!window.confirm('Remove fee assignment for this year?')) return
    removeMutation.mutate()
  }

  const handleRemoveDiscount = (id: string, name: string) => {
    if (!window.confirm(`Remove discount "${name}"?`)) return
    removeDiscountMutation.mutate(id)
  }

  const alreadyAssignedRuleIds = new Set(discounts.map((d) => d.discountRuleId))

  return (
    <div className="flex flex-col gap-6">
      {/* Year selector */}
      <div className="flex items-center gap-2">
        <label className="text-sm font-medium text-foreground whitespace-nowrap">Academic Year</label>
        <Select value={selectedYearId} onValueChange={setSelectedYearId}>
          <SelectTrigger className="w-48">
            <SelectValue placeholder="Select year" />
          </SelectTrigger>
          <SelectContent>
            {years.map((y) => (
              <SelectItem key={y.id} value={y.id}>{y.name}</SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {/* Assignment card */}
      {assignmentLoading ? (
        <div className="text-sm text-muted-foreground">Loading…</div>
      ) : !assignment ? (
        <div className="rounded-lg border border-border bg-muted/40 px-6 py-5 flex items-center justify-between">
          <p className="text-sm text-muted-foreground">No fee template assigned for this year.</p>
          <Button size="sm" onClick={() => setAssignOpen(true)} disabled={!selectedYearId}>
            <Plus size={14} className="mr-1.5" /> Assign Template
          </Button>
        </div>
      ) : (
        <div className="rounded-lg border border-border bg-card px-6 py-5 flex flex-col gap-3">
          <div className="flex items-start justify-between">
            <div className="flex flex-col gap-1">
              <span className="text-sm font-semibold text-foreground">{assignment.templateName}</span>
              <span className="text-xs text-muted-foreground">{assignment.academicYearName}</span>
            </div>
            <div className="flex items-center gap-2">
              <Button size="sm" variant="outline" onClick={() => setAssignOpen(true)}>
                Override
              </Button>
              <Button
                size="sm"
                variant="ghost"
                className="text-destructive hover:text-destructive"
                onClick={handleRemoveAssignment}
                disabled={removeMutation.isPending}
              >
                <Trash2 size={14} />
              </Button>
            </div>
          </div>
        </div>
      )}

      {/* Discount rules */}
      {assignment && (
        <div className="flex flex-col gap-3">
          <div className="flex items-center justify-between">
            <h3 className="text-sm font-semibold text-foreground">Discount Rules</h3>
            <Button size="sm" variant="outline" onClick={() => setAddDiscountOpen(true)}>
              <Plus size={14} className="mr-1.5" /> Add Discount
            </Button>
          </div>

          {discountsLoading ? (
            <div className="text-sm text-muted-foreground">Loading…</div>
          ) : (
            <div className="rounded-lg border border-border overflow-hidden">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Rule Name</TableHead>
                    <TableHead>Type</TableHead>
                    <TableHead>Value</TableHead>
                    <TableHead className="w-10" />
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {discounts.length === 0 ? (
                    <TableRow>
                      <TableCell colSpan={4} className="h-20 text-center text-muted-foreground text-sm">
                        No discount rules applied.
                      </TableCell>
                    </TableRow>
                  ) : (
                    discounts.map((d) => (
                      <TableRow key={d.id}>
                        <TableCell className="font-medium text-sm">{d.discountRuleName}</TableCell>
                        <TableCell>
                          <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${
                            d.ruleType === 'Percentage'
                              ? 'bg-blue-100 text-blue-700 dark:bg-blue-900/30 dark:text-blue-400'
                              : 'bg-orange-100 text-orange-700 dark:bg-orange-900/30 dark:text-orange-400'
                          }`}>
                            {d.ruleType}
                          </span>
                        </TableCell>
                        <TableCell className="font-mono text-sm">
                          {d.ruleType === 'Percentage'
                            ? `${d.value}%`
                            : `₱${d.value.toLocaleString()}`}
                        </TableCell>
                        <TableCell>
                          <Button
                            size="sm"
                            variant="ghost"
                            className="h-7 w-7 p-0 text-destructive hover:text-destructive"
                            onClick={() => handleRemoveDiscount(d.id, d.discountRuleName)}
                            disabled={removeDiscountMutation.isPending}
                          >
                            <Trash2 size={13} />
                          </Button>
                        </TableCell>
                      </TableRow>
                    ))
                  )}
                </TableBody>
              </Table>
            </div>
          )}
        </div>
      )}

      {/* Modals */}
      {selectedYearId && (
        <SetFeeAssignmentModal
          open={assignOpen}
          studentId={studentId}
          academicYearId={selectedYearId}
          currentTemplateId={assignment?.feeTemplateId}
          onClose={() => setAssignOpen(false)}
          onSaved={() => {
            queryClient.invalidateQueries({ queryKey: FEE_ASSIGNMENT_KEYS.studentAssignment(studentId, selectedYearId) })
          }}
        />
      )}

      {selectedYearId && assignment && (
        <AddDiscountModal
          open={addDiscountOpen}
          studentId={studentId}
          academicYearId={selectedYearId}
          assignedTemplateId={assignment.feeTemplateId}
          alreadyAssignedRuleIds={alreadyAssignedRuleIds}
          onClose={() => setAddDiscountOpen(false)}
          onAdded={() => {
            queryClient.invalidateQueries({ queryKey: FEE_ASSIGNMENT_KEYS.studentDiscounts(studentId, selectedYearId) })
          }}
        />
      )}
    </div>
  )
}
