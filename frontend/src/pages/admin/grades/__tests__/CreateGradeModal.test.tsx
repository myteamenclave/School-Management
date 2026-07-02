import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { CreateGradeModal } from '../components/CreateGradeModal'
import { gradesApi } from '../../../../api/grades'
import type { GradeDto } from '../../../../api/grades'

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

const mockToast = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn() }))
vi.mock('sonner', () => ({ toast: mockToast, Toaster: () => null }))

const sampleGrade: GradeDto = {
  id: 'grade-1',
  name: 'Grade 1',
  displayOrder: 1,
  sections: [],
}

function renderModal(onCreated = vi.fn(), onClose = vi.fn()) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  return render(
    <QueryClientProvider client={queryClient}>
      <CreateGradeModal open={true} onClose={onClose} onCreated={onCreated} />
    </QueryClientProvider>
  )
}

describe('CreateGradeModal', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('1: submits correct payload', async () => {
    vi.mocked(gradesApi.create).mockResolvedValue(sampleGrade)
    const onCreated = vi.fn()
    renderModal(onCreated)

    await userEvent.type(screen.getByLabelText(/grade name/i), 'Grade 1')
    await userEvent.clear(screen.getByLabelText(/display order/i))
    await userEvent.type(screen.getByLabelText(/display order/i), '1')

    await userEvent.click(screen.getByRole('button', { name: /create grade/i }))

    await waitFor(() => {
      expect(vi.mocked(gradesApi.create).mock.calls[0]?.[0]).toMatchObject({
        name: 'Grade 1',
        displayOrder: 1,
      })
    })
    expect(onCreated).toHaveBeenCalledWith('grade-1')
  })

  it('2: shows error toast on 409', async () => {
    const err = Object.assign(new Error('Conflict'), {
      isAxiosError: true,
      response: { status: 409, data: { error: 'Conflict' } },
    })
    vi.mocked(gradesApi.create).mockRejectedValue(err)
    renderModal()

    await userEvent.type(screen.getByLabelText(/grade name/i), 'Grade 1')
    await userEvent.click(screen.getByRole('button', { name: /create grade/i }))

    await waitFor(() => {
      expect(mockToast.error).toHaveBeenCalledWith(
        expect.stringContaining('already exists')
      )
    })
  })

  it('3: submit button is disabled while pending', async () => {
    // Never resolves — simulates in-flight mutation
    vi.mocked(gradesApi.create).mockImplementation(() => new Promise(() => {}))
    renderModal()

    await userEvent.type(screen.getByLabelText(/grade name/i), 'Grade 1')

    const submitBtn = screen.getByRole('button', { name: /create grade/i })
    await userEvent.click(submitBtn)

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /creating/i })).toBeDisabled()
    })
  })
})
