import { useState, useEffect } from 'react'
import { useParams, useNavigate, useSearchParams, useBlocker } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useForm, Controller } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { toast } from 'sonner'
import { isAxiosError } from 'axios'
import { ChevronRight, AlertTriangle } from 'lucide-react'
import { Button } from '../../../components/ui/button'
import { Input } from '../../../components/ui/input'
import { Label } from '../../../components/ui/label'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '../../../components/ui/tabs'
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from '../../../components/ui/dialog'
import { feeTemplatesApi, FEE_TEMPLATE_KEYS } from '../../../api/feeTemplates'
import type { FeeTemplateDto } from '../../../api/feeTemplates'
import { LineItemsTab } from './components/LineItemsTab'
import { InstallmentsTab } from './components/InstallmentsTab'
import { DiscountRulesTab } from './components/DiscountRulesTab'

function extractError(err: unknown): string {
  if (isAxiosError(err) && err.response?.data?.error) return err.response.data.error
  return 'An unexpected error occurred.'
}

function StatusBadge({ isActive }: { isActive: boolean }) {
  return (
    <span className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ${
      isActive
        ? 'bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-400'
        : 'bg-zinc-100 text-zinc-500 dark:bg-zinc-800 dark:text-zinc-400'
    }`}>
      {isActive ? 'Active' : 'Inactive'}
    </span>
  )
}

function ConfirmDiscardDialog({
  open,
  onConfirm,
  onCancel,
}: {
  open: boolean
  onConfirm: () => void
  onCancel: () => void
}) {
  return (
    <Dialog open={open} onOpenChange={(v) => { if (!v) onCancel() }}>
      <DialogContent className="sm:max-w-sm">
        <DialogHeader>
          <DialogTitle>Discard unsaved changes?</DialogTitle>
        </DialogHeader>
        <p className="text-sm text-muted-foreground">
          You have unsaved changes that will be lost.
        </p>
        <DialogFooter>
          <Button variant="outline" onClick={onCancel}>Cancel</Button>
          <Button variant="destructive" onClick={onConfirm}>Discard Changes</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

const headerSchema = z.object({
  name:     z.string().min(1, 'Required').max(200),
  isActive: z.boolean(),
})
type HeaderFormValues = z.infer<typeof headerSchema>

function TemplateHeaderSection({
  template,
  isEditMode,
  onEnterEdit,
  onDirtyChange,
  templateId,
}: {
  template: FeeTemplateDto | undefined
  isEditMode: boolean
  onEnterEdit: () => void
  onDirtyChange: (dirty: boolean) => void
  templateId: string
}) {
  const queryClient = useQueryClient()
  const { control, register, handleSubmit, reset, formState: { errors, isDirty } } =
    useForm<HeaderFormValues>({
      resolver: zodResolver(headerSchema),
      defaultValues: { name: '', isActive: true },
    })

  useEffect(() => {
    if (template) {
      reset({ name: template.name, isActive: template.isActive })
    }
  }, [template, reset])

  useEffect(() => {
    onDirtyChange(isDirty)
  }, [isDirty, onDirtyChange])

  const headerMutation = useMutation({
    mutationFn: (data: HeaderFormValues) =>
      feeTemplatesApi.updateHeader(templateId, { name: data.name, isActive: data.isActive }),
    onSuccess: (updated) => {
      queryClient.setQueryData(FEE_TEMPLATE_KEYS.detail(templateId), updated)
      queryClient.invalidateQueries({ queryKey: ['fee-templates', 'list'] })
      reset({ name: updated.name, isActive: updated.isActive })
      toast.success('Template updated')
      onDirtyChange(false)
    },
    onError: (err) => toast.error(extractError(err)),
  })

  if (!isEditMode) {
    return (
      <div className="rounded-lg border border-border bg-card px-6 py-5">
        <div className="flex items-start justify-between gap-4">
          <div className="flex flex-col gap-2">
            <h2 className="text-xl font-semibold text-foreground">{template?.name ?? '…'}</h2>
            <div className="flex items-center gap-2 flex-wrap">
              {template && (
                <>
                  <span className="inline-flex items-center rounded-md bg-muted px-2.5 py-0.5 text-xs font-medium text-muted-foreground">
                    {template.gradeName}
                  </span>
                  <span className="inline-flex items-center rounded-md bg-muted px-2.5 py-0.5 text-xs font-medium text-muted-foreground">
                    {template.academicYearName}
                  </span>
                  <StatusBadge isActive={template.isActive} />
                </>
              )}
            </div>
          </div>
          <Button size="sm" variant="outline" onClick={onEnterEdit}>Edit</Button>
        </div>
      </div>
    )
  }

  return (
    <div className="rounded-lg border border-border bg-card px-6 py-5">
      <form onSubmit={handleSubmit((data) => headerMutation.mutate(data))} className="flex flex-col gap-4">
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="th-name">Template Name</Label>
          <Input id="th-name" {...register('name')} />
          {errors.name && <p className="text-xs text-destructive">{errors.name.message}</p>}
        </div>

        <div className="flex items-center gap-4 flex-wrap">
          <div className="flex flex-col gap-0.5">
            <span className="text-xs text-muted-foreground">Grade</span>
            <span className="text-sm font-medium">{template?.gradeName}</span>
          </div>
          <div className="flex flex-col gap-0.5">
            <span className="text-xs text-muted-foreground">Academic Year</span>
            <span className="text-sm font-medium">{template?.academicYearName}</span>
          </div>
        </div>

        <Controller
          name="isActive"
          control={control}
          render={({ field }) => (
            <div className="flex items-center gap-2">
              <input
                type="checkbox"
                id="th-isActive"
                checked={field.value ?? false}
                onChange={(e) => field.onChange(e.target.checked)}
                className="h-4 w-4 rounded border-border accent-primary cursor-pointer"
              />
              <label htmlFor="th-isActive" className="text-sm text-foreground cursor-pointer">
                Active
              </label>
            </div>
          )}
        />

        <div className="flex items-center gap-2">
          <Button type="submit" size="sm" disabled={headerMutation.isPending}>
            {headerMutation.isPending ? 'Saving…' : 'Save Header'}
          </Button>
        </div>
      </form>
    </div>
  )
}

export function FeeTemplatePage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const [searchParams, setSearchParams] = useSearchParams()
  const isEditMode = searchParams.get('edit') === 'true'

  const [headerDirty, setHeaderDirty] = useState(false)
  const [lineItemsDirty, setLineItemsDirty] = useState(false)
  const [installmentsDirty, setInstallmentsDirty] = useState(false)
  const [discountRulesDirty, setDiscountRulesDirty] = useState(false)

  const isDirty = headerDirty || lineItemsDirty || installmentsDirty || discountRulesDirty

  const enterEditMode = () => setSearchParams({ edit: 'true' })

  const { data: template, isLoading, isError } = useQuery({
    queryKey: FEE_TEMPLATE_KEYS.detail(id!),
    queryFn: () => feeTemplatesApi.getById(id!),
  })

  const blocker = useBlocker(
    ({ currentLocation, nextLocation }) =>
      isDirty && currentLocation.pathname !== nextLocation.pathname
  )

  useEffect(() => {
    if (!isDirty) return
    const handler = (e: BeforeUnloadEvent) => { e.preventDefault() }
    window.addEventListener('beforeunload', handler)
    return () => window.removeEventListener('beforeunload', handler)
  }, [isDirty])

  if (isLoading) {
    return (
      <div className="flex items-center justify-center h-48 text-sm text-muted-foreground">
        Loading…
      </div>
    )
  }

  if (isError) {
    return (
      <div className="flex items-center justify-center h-48 text-sm text-destructive">
        Failed to load template.
      </div>
    )
  }

  return (
    <div className="px-8 py-8 max-w-5xl mx-auto">
      <nav className="flex items-center gap-2 text-sm text-muted-foreground mb-6">
        <button
          onClick={() => navigate('/admin/fee-templates')}
          className="hover:text-foreground transition-colors"
        >
          Fee Templates
        </button>
        <ChevronRight size={14} />
        <span className="text-foreground font-medium">{template?.name ?? '…'}</span>
      </nav>

      {isEditMode && template && !template.isActive && (
        <div className="mb-4 flex items-center gap-2 rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-800 dark:border-amber-800 dark:bg-amber-950/30 dark:text-amber-300">
          <AlertTriangle size={15} className="shrink-0" />
          This template is inactive and will not appear in the default list.
        </div>
      )}

      <TemplateHeaderSection
        template={template}
        isEditMode={isEditMode}
        onEnterEdit={enterEditMode}
        onDirtyChange={setHeaderDirty}
        templateId={id!}
      />

      <Tabs defaultValue="line-items" className="mt-6">
        <TabsList>
          <TabsTrigger value="line-items">
            Line Items {lineItemsDirty && <span className="ml-1 text-amber-500">●</span>}
          </TabsTrigger>
          <TabsTrigger value="installments">
            Installments {installmentsDirty && <span className="ml-1 text-amber-500">●</span>}
          </TabsTrigger>
          <TabsTrigger value="discount-rules">
            Discount Rules {discountRulesDirty && <span className="ml-1 text-amber-500">●</span>}
          </TabsTrigger>
        </TabsList>

        <TabsContent value="line-items">
          <LineItemsTab
            template={template}
            isEditMode={isEditMode}
            onDirtyChange={setLineItemsDirty}
            templateId={id!}
          />
        </TabsContent>
        <TabsContent value="installments">
          <InstallmentsTab
            template={template}
            isEditMode={isEditMode}
            onDirtyChange={setInstallmentsDirty}
            templateId={id!}
          />
        </TabsContent>
        <TabsContent value="discount-rules">
          <DiscountRulesTab
            template={template}
            isEditMode={isEditMode}
            onDirtyChange={setDiscountRulesDirty}
            templateId={id!}
          />
        </TabsContent>
      </Tabs>

      {blocker.state === 'blocked' && (
        <ConfirmDiscardDialog
          open
          onConfirm={() => blocker.proceed?.()}
          onCancel={() => blocker.reset?.()}
        />
      )}
    </div>
  )
}
