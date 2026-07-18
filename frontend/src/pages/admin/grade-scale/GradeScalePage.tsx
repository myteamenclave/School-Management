import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { isAxiosError } from 'axios'
import { Plus, Pencil, Trash2, GraduationCap } from 'lucide-react'
import { Button } from '../../../components/ui/button'
import { Input } from '../../../components/ui/input'
import { Label } from '../../../components/ui/label'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '../../../components/ui/table'
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from '../../../components/ui/dialog'
import { gradeScaleApi, GRADEBOOK_KEYS } from '../../../api/gradebook'
import type { GradeScaleBand } from '../../../api/gradebook'

function extractError(err: unknown): string {
  if (isAxiosError(err) && err.response?.data?.error) return err.response.data.error
  return 'An unexpected error occurred.'
}

interface FormState {
  letter: string
  minScore: string
  maxScore: string
}

const EMPTY_FORM: FormState = { letter: '', minScore: '', maxScore: '' }

export function GradeScalePage() {
  const queryClient = useQueryClient()
  const [dialogOpen, setDialogOpen] = useState(false)
  const [editingId, setEditingId] = useState<string | null>(null)
  const [form, setForm] = useState<FormState>(EMPTY_FORM)
  const [deleteTarget, setDeleteTarget] = useState<GradeScaleBand | null>(null)

  const { data: bands = [], isLoading } = useQuery({
    queryKey: GRADEBOOK_KEYS.scale,
    queryFn: gradeScaleApi.getAll,
  })

  const invalidate = () => queryClient.invalidateQueries({ queryKey: GRADEBOOK_KEYS.scale })

  const saveMutation = useMutation({
    mutationFn: () => {
      const body = {
        letter: form.letter.trim(),
        minScore: Number(form.minScore),
        maxScore: Number(form.maxScore),
      }
      return editingId ? gradeScaleApi.update(editingId, body) : gradeScaleApi.create(body)
    },
    onSuccess: () => {
      toast.success(editingId ? 'Band updated.' : 'Band added.')
      setDialogOpen(false)
      invalidate()
    },
    onError: (err) => toast.error(extractError(err)),
  })

  const deleteMutation = useMutation({
    mutationFn: (id: string) => gradeScaleApi.remove(id),
    onSuccess: () => {
      toast.success('Band deleted.')
      setDeleteTarget(null)
      invalidate()
    },
    onError: (err) => toast.error(extractError(err)),
  })

  const openCreate = () => {
    setEditingId(null)
    setForm(EMPTY_FORM)
    setDialogOpen(true)
  }

  const openEdit = (band: GradeScaleBand) => {
    setEditingId(band.id)
    setForm({
      letter: band.letter,
      minScore: band.minScore.toString(),
      maxScore: band.maxScore.toString(),
    })
    setDialogOpen(true)
  }

  const min = Number(form.minScore)
  const max = Number(form.maxScore)
  const formValid =
    form.letter.trim() !== '' &&
    form.minScore.trim() !== '' &&
    form.maxScore.trim() !== '' &&
    Number.isFinite(min) &&
    Number.isFinite(max) &&
    min >= 0 &&
    max <= 100 &&
    min <= max

  return (
    <div className="px-8 py-8 max-w-3xl mx-auto">
      <div className="flex items-start justify-between mb-6">
        <div className="flex items-center gap-3">
          <GraduationCap size={22} className="text-primary" />
          <div>
            <h1 className="font-heading text-2xl font-semibold text-foreground">Grade Scale</h1>
            <p className="text-sm text-muted-foreground mt-1">
              Letter bands used to map term scores. Edits apply to grades saved after the change;
              existing grades keep their stored letter until re-saved.
            </p>
          </div>
        </div>
        <Button onClick={openCreate}>
          <Plus size={16} className="mr-2" /> Add Band
        </Button>
      </div>

      {isLoading ? (
        <div className="flex items-center justify-center h-48 text-sm text-muted-foreground">
          Loading…
        </div>
      ) : (
        <div className="rounded-lg border border-border overflow-hidden">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead className="w-24">Letter</TableHead>
                <TableHead>Min Score</TableHead>
                <TableHead>Max Score</TableHead>
                <TableHead className="w-24" />
              </TableRow>
            </TableHeader>
            <TableBody>
              {bands.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={4} className="h-32 text-center text-muted-foreground text-sm">
                    No grade scale bands defined.
                  </TableCell>
                </TableRow>
              ) : (
                bands.map((band) => (
                  <TableRow key={band.id}>
                    <TableCell className="font-medium">{band.letter}</TableCell>
                    <TableCell className="tabular-nums">{band.minScore}</TableCell>
                    <TableCell className="tabular-nums">{band.maxScore}</TableCell>
                    <TableCell>
                      <div className="flex gap-1">
                        <Button size="sm" variant="ghost" onClick={() => openEdit(band)}>
                          <Pencil size={14} />
                        </Button>
                        <Button size="sm" variant="ghost" onClick={() => setDeleteTarget(band)}>
                          <Trash2 size={14} className="text-red-500" />
                        </Button>
                      </div>
                    </TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>
        </div>
      )}

      {/* Create / Edit dialog */}
      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent className="sm:max-w-sm">
          <DialogHeader>
            <DialogTitle>{editingId ? 'Edit Band' : 'Add Band'}</DialogTitle>
          </DialogHeader>
          <div className="flex flex-col gap-4 py-2">
            <div className="flex flex-col gap-1.5">
              <Label>Letter</Label>
              <Input
                maxLength={4}
                placeholder="e.g. A"
                value={form.letter}
                onChange={(e) => setForm((f) => ({ ...f, letter: e.target.value }))}
              />
            </div>
            <div className="grid grid-cols-2 gap-3">
              <div className="flex flex-col gap-1.5">
                <Label>Min Score</Label>
                <Input
                  type="number"
                  min={0}
                  max={100}
                  step={0.01}
                  value={form.minScore}
                  onChange={(e) => setForm((f) => ({ ...f, minScore: e.target.value }))}
                />
              </div>
              <div className="flex flex-col gap-1.5">
                <Label>Max Score</Label>
                <Input
                  type="number"
                  min={0}
                  max={100}
                  step={0.01}
                  value={form.maxScore}
                  onChange={(e) => setForm((f) => ({ ...f, maxScore: e.target.value }))}
                />
              </div>
            </div>
            {!formValid && (form.minScore !== '' || form.maxScore !== '') && (
              <p className="text-xs text-red-500">
                Scores must be 0–100 and Min ≤ Max.
              </p>
            )}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDialogOpen(false)}>
              Cancel
            </Button>
            <Button
              onClick={() => saveMutation.mutate()}
              disabled={!formValid || saveMutation.isPending}
            >
              {saveMutation.isPending ? 'Saving…' : 'Save'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Delete confirm */}
      <Dialog open={deleteTarget !== null} onOpenChange={(o) => !o && setDeleteTarget(null)}>
        <DialogContent className="sm:max-w-sm">
          <DialogHeader>
            <DialogTitle>Delete band “{deleteTarget?.letter}”?</DialogTitle>
          </DialogHeader>
          <p className="text-sm text-muted-foreground">
            This removes the letter band. Existing grades keep their stored letter until re-saved.
          </p>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDeleteTarget(null)}>
              Cancel
            </Button>
            <Button
              variant="destructive"
              onClick={() => deleteTarget && deleteMutation.mutate(deleteTarget.id)}
              disabled={deleteMutation.isPending}
            >
              {deleteMutation.isPending ? 'Deleting…' : 'Delete'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  )
}
