import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { SectionChip } from '../components/SectionChip'
import { gradesApi } from '../../../../api/grades'
import type { SectionDto } from '../../../../api/grades'

vi.mock('../../../../api/grades', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../../../api/grades')>()
  return {
    ...actual,
    gradesApi: {
      list: vi.fn(),
      create: vi.fn(),
      update: vi.fn(),
      delete: vi.fn(),
      addSection: vi.fn(),
      updateSection: vi.fn(),
      deleteSection: vi.fn(),
    },
  }
})

vi.mock('sonner', () => ({
  toast: { success: vi.fn(), error: vi.fn() },
  Toaster: () => null,
}))

const section: SectionDto = { id: 'sec-1', gradeId: 'grade-1', name: 'Section A' }

function renderChip() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  return render(
    <QueryClientProvider client={queryClient}>
      <SectionChip section={section} gradeId="grade-1" />
    </QueryClientProvider>
  )
}

describe('SectionChip', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('1: renders section name as a button', () => {
    renderChip()
    expect(screen.getByRole('button', { name: 'Section A' })).toBeInTheDocument()
  })

  it('2: clicking chip enters edit mode with name pre-filled', async () => {
    renderChip()
    await userEvent.click(screen.getByRole('button', { name: 'Section A' }))

    const input = screen.getByRole('textbox') as HTMLInputElement
    expect(input).toBeInTheDocument()
    expect(input.value).toBe('Section A')
  })

  it('3: Escape key cancels edit and restores chip', async () => {
    renderChip()
    await userEvent.click(screen.getByRole('button', { name: 'Section A' }))

    await userEvent.keyboard('{Escape}')

    expect(screen.queryByRole('textbox')).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Section A' })).toBeInTheDocument()
  })

  it('4: saving calls updateSection with new name', async () => {
    vi.mocked(gradesApi.updateSection).mockResolvedValue({ ...section, name: 'Section B' })
    renderChip()

    await userEvent.click(screen.getByRole('button', { name: 'Section A' }))

    const input = screen.getByRole('textbox')
    await userEvent.clear(input)
    await userEvent.type(input, 'Section B')

    // Click the checkmark save button
    const saveBtn = screen.getAllByRole('button').find((b) => b.querySelector('svg'))!
    await userEvent.click(saveBtn)

    await waitFor(() => {
      expect(vi.mocked(gradesApi.updateSection).mock.calls[0]).toEqual([
        'grade-1',
        'sec-1',
        { name: 'Section B' },
      ])
    })
  })

  it('5: delete with confirm calls deleteSection', async () => {
    vi.mocked(gradesApi.deleteSection).mockResolvedValue(undefined as never)
    vi.spyOn(window, 'confirm').mockReturnValue(true)
    renderChip()

    // Enter edit mode first to see the delete button
    await userEvent.click(screen.getByRole('button', { name: 'Section A' }))

    // Find and click the delete (trash) button — last button in edit mode
    const buttons = screen.getAllByRole('button')
    const deleteBtn = buttons[buttons.length - 1]
    await userEvent.click(deleteBtn)

    expect(window.confirm).toHaveBeenCalled()
    await waitFor(() => {
      expect(vi.mocked(gradesApi.deleteSection)).toHaveBeenCalledWith('grade-1', 'sec-1')
    })
  })
})
