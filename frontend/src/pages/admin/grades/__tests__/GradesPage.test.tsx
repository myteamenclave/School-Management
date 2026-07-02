import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { GradesPage } from '../GradesPage'
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

vi.mock('sonner', () => ({
  toast: { success: vi.fn(), error: vi.fn() },
  Toaster: () => null,
}))

// ── fixtures ──────────────────────────────────────────────────────────────────

const gradeWithSections: GradeDto = {
  id: 'grade-1',
  name: 'Grade 1',
  displayOrder: 1,
  sections: [
    { id: 'sec-1', gradeId: 'grade-1', name: 'Section A' },
    { id: 'sec-2', gradeId: 'grade-1', name: 'Section B' },
  ],
}

const emptyGrade: GradeDto = {
  id: 'grade-2',
  name: 'Grade 2',
  displayOrder: 2,
  sections: [],
}

// ── helpers ───────────────────────────────────────────────────────────────────

function renderPage() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  return render(
    <QueryClientProvider client={queryClient}>
      <GradesPage />
    </QueryClientProvider>
  )
}

// ── tests ─────────────────────────────────────────────────────────────────────

describe('GradesPage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('1: renders empty state when no grades', async () => {
    vi.mocked(gradesApi.list).mockResolvedValue([])
    renderPage()

    await screen.findByText(/no grades yet/i)
    expect(screen.getAllByRole('button', { name: /add grade/i })).toHaveLength(2)
  })

  it('2: renders grade list with section counts', async () => {
    vi.mocked(gradesApi.list).mockResolvedValue([gradeWithSections, emptyGrade])
    renderPage()

    await screen.findByText('Grade 1')
    expect(screen.getByText('Grade 2')).toBeInTheDocument()
    expect(screen.getByText('2 sections')).toBeInTheDocument()
    expect(screen.getByText('0 sections')).toBeInTheDocument()
  })

  it('3: accordion expands on click and shows section chips', async () => {
    vi.mocked(gradesApi.list).mockResolvedValue([gradeWithSections])
    renderPage()

    await screen.findByText('Grade 1')

    // Initially, section chips should not be visible (accordion is collapsed)
    expect(screen.queryByText('Section A')).not.toBeInTheDocument()

    // Click the accordion trigger
    await userEvent.click(screen.getByText('Grade 1'))

    // Section chips should now be visible
    await screen.findByText('Section A')
    expect(screen.getByText('Section B')).toBeInTheDocument()
  })

  it('4: Delete Grade button is disabled when grade has sections', async () => {
    vi.mocked(gradesApi.list).mockResolvedValue([gradeWithSections])
    renderPage()

    await screen.findByText('Grade 1')

    // Expand accordion
    await userEvent.click(screen.getByText('Grade 1'))

    await screen.findByRole('button', { name: /delete grade/i })
    expect(screen.getByRole('button', { name: /delete grade/i })).toBeDisabled()
  })

  it('5: Delete Grade button is enabled when grade has no sections', async () => {
    vi.mocked(gradesApi.list).mockResolvedValue([emptyGrade])
    renderPage()

    await screen.findByText('Grade 2')

    // Expand accordion
    await userEvent.click(screen.getByText('Grade 2'))

    await screen.findByRole('button', { name: /delete grade/i })
    expect(screen.getByRole('button', { name: /delete grade/i })).not.toBeDisabled()
  })

  it('6: opens create modal on "Add Grade" click', async () => {
    vi.mocked(gradesApi.list).mockResolvedValue([])
    renderPage()

    await screen.findByText(/no grades yet/i)

    const addBtns = screen.getAllByRole('button', { name: /add grade/i })
    await userEvent.click(addBtns[0])

    await screen.findByRole('dialog')
    expect(screen.getByRole('dialog')).toBeInTheDocument()
  })
})
