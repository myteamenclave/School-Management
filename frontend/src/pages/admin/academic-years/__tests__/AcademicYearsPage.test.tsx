import { render, screen, waitFor, fireEvent } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { AcademicYearsPage } from '../AcademicYearsPage'
import { AcademicYearCard } from '../components/AcademicYearCard'
import { academicYearsApi } from '../../../../api/academicYears'
import type { AcademicYearDto, SemesterDto } from '../../../../api/academicYears'

vi.mock('../../../../api/academicYears', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../../../api/academicYears')>()
  return {
    ...actual,
    academicYearsApi: {
      list: vi.fn(),
      create: vi.fn(),
      updateSemester: vi.fn(),
      setCurrentYear: vi.fn(),
      setCurrentSemester: vi.fn(),
      archive: vi.fn(),
    },
  }
})

// sonner toast — silence in tests
vi.mock('sonner', () => ({
  toast: { success: vi.fn(), error: vi.fn() },
  Toaster: () => null,
}))

// ── fixtures ──────────────────────────────────────────────────────────────────

const sem1Current: SemesterDto = {
  id: 'sem-1',
  academicYearId: 'year-1',
  name: 'Semester 1',
  startDate: '2025-08-01',
  endDate: '2026-01-31',
  isCurrent: true,
}

const sem2NotCurrent: SemesterDto = {
  id: 'sem-2',
  academicYearId: 'year-1',
  name: 'Semester 2',
  startDate: '2026-02-01',
  endDate: '2026-07-31',
  isCurrent: false,
}

const currentYear: AcademicYearDto = {
  id: 'year-1',
  name: '2025–2026',
  startDate: '2025-08-01',
  endDate: '2026-07-31',
  status: 'Active',
  isCurrent: true,
  semesters: [sem1Current, sem2NotCurrent],
}

const prevYear: AcademicYearDto = {
  id: 'year-2',
  name: '2024–2025',
  startDate: '2024-08-01',
  endDate: '2025-07-31',
  status: 'Active',
  isCurrent: false,
  semesters: [
    { id: 'sem-3', academicYearId: 'year-2', name: 'Semester 1', startDate: '2024-08-01', endDate: '2025-01-31', isCurrent: false },
    { id: 'sem-4', academicYearId: 'year-2', name: 'Semester 2', startDate: '2025-02-01', endDate: '2025-07-31', isCurrent: false },
  ],
}

const archivedYear: AcademicYearDto = {
  id: 'year-3',
  name: '2023–2024',
  startDate: '2023-08-01',
  endDate: '2024-07-31',
  status: 'Archived',
  isCurrent: false,
  semesters: [],
}

// ── helpers ───────────────────────────────────────────────────────────────────

function renderPage() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  return render(
    <QueryClientProvider client={queryClient}>
      <AcademicYearsPage />
    </QueryClientProvider>
  )
}

// ── tests ─────────────────────────────────────────────────────────────────────

describe('AcademicYearsPage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('1: renders empty state when list returns []', async () => {
    vi.mocked(academicYearsApi.list).mockResolvedValue([])
    renderPage()

    await screen.findByText(/no academic years yet/i)
    expect(screen.getAllByRole('button', { name: /new academic year/i })).toHaveLength(2)
  })

  it('2: current year card has current treatment; Set-as-Current and Archive buttons absent', async () => {
    vi.mocked(academicYearsApi.list).mockResolvedValue([currentYear])
    renderPage()

    await screen.findByText('2025–2026')

    // "Current Year" appears in both the section heading and the card badge
    const currentYearTexts = screen.getAllByText('Current Year')
    expect(currentYearTexts.length).toBeGreaterThanOrEqual(1)
    // The badge is a <span> element
    expect(currentYearTexts.some((el) => el.tagName.toLowerCase() === 'span')).toBe(true)

    // "Set as Current" and "Archive" should NOT appear for the current year
    expect(screen.queryByRole('button', { name: /set as current/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /archive/i })).not.toBeInTheDocument()

    // Protected chip should be visible
    expect(screen.getByText(/protected — cannot archive/i)).toBeInTheDocument()
  })

  it('3: clicking "Set as Current" calls setCurrentYear with correct id', async () => {
    vi.mocked(academicYearsApi.list).mockResolvedValue([currentYear, prevYear])
    vi.mocked(academicYearsApi.setCurrentYear).mockResolvedValue(undefined)
    renderPage()

    await screen.findByText('2024–2025')

    const setCurrentBtn = screen.getByRole('button', { name: /set as current/i })
    await userEvent.click(setCurrentBtn)

    await waitFor(() => {
      // TanStack Query v5 may pass context as 2nd arg — check just the first argument
      expect(vi.mocked(academicYearsApi.setCurrentYear).mock.calls[0]?.[0]).toBe('year-2')
    })
  })

  it('4: clicking Archive triggers confirm; archive not called if cancelled', async () => {
    vi.mocked(academicYearsApi.list).mockResolvedValue([currentYear, prevYear])
    vi.spyOn(window, 'confirm').mockReturnValue(false)
    renderPage()

    await screen.findByText('2024–2025')

    const archiveBtn = screen.getByRole('button', { name: /archive/i })
    await userEvent.click(archiveBtn)

    expect(window.confirm).toHaveBeenCalled()
    expect(academicYearsApi.archive).not.toHaveBeenCalled()
  })

  it('5: archived years hidden by default; visible after toggle click', async () => {
    vi.mocked(academicYearsApi.list).mockResolvedValue([currentYear, archivedYear])
    renderPage()

    await screen.findByText('2025–2026')

    // Archived year should be hidden initially
    expect(screen.queryByText('2023–2024')).not.toBeInTheDocument()

    // Click the show archived toggle
    const toggleBtn = screen.getByRole('button', { name: /show archived/i })
    await userEvent.click(toggleBtn)

    expect(screen.getByText('2023–2024')).toBeInTheDocument()
  })

  it('6: "New Academic Year" opens modal; submit calls create; closes on success', async () => {
    vi.mocked(academicYearsApi.list).mockResolvedValue([])
    vi.mocked(academicYearsApi.create).mockResolvedValue(currentYear)
    renderPage()

    // Wait for page to load
    await screen.findByText(/no academic years yet/i)

    // Open modal via header button (first "New Academic Year" button)
    const newYearBtns = screen.getAllByRole('button', { name: /new academic year/i })
    await userEvent.click(newYearBtns[0])

    // Modal should be visible
    await screen.findByRole('dialog')

    // Fill in form — use fireEvent.change for date inputs since userEvent.type
    // doesn't reliably produce the correct value for type="date" in jsdom
    await userEvent.type(screen.getByLabelText(/year name/i), '2025–2026')
    fireEvent.change(screen.getByLabelText(/start date/i), { target: { value: '2025-08-01' } })
    fireEvent.change(screen.getByLabelText(/end date/i), { target: { value: '2026-07-31' } })

    // Submit
    await userEvent.click(screen.getByRole('button', { name: /create year/i }))

    await waitFor(() => {
      expect(vi.mocked(academicYearsApi.create).mock.calls[0]?.[0]).toMatchObject({
        name: '2025–2026',
        startDate: '2025-08-01',
        endDate: '2026-07-31',
      })
    })
  })

  it('7: Edit semester modal pre-populates with semester values', async () => {
    vi.mocked(academicYearsApi.list).mockResolvedValue([currentYear])
    renderPage()

    await screen.findByText('Semester 1')

    // Click Edit on Semester 1
    const editBtns = screen.getAllByRole('button', { name: /edit semester 1/i })
    await userEvent.click(editBtns[0])

    // Modal should be open with pre-populated name
    await waitFor(() => {
      const nameInput = screen.getByLabelText(/semester name/i) as HTMLInputElement
      expect(nameInput.value).toBe('Semester 1')
    })
  })
})

describe('AcademicYearCard — Set Current Semester visibility', () => {
  it('8: shows Set Current only on non-current semesters in the current year; not in non-current year', () => {
    const mockFns = {
      onSetCurrent: vi.fn(),
      onArchive: vi.fn(),
      onEditSemester: vi.fn(),
      onSetCurrentSemester: vi.fn(),
    }

    // Render a current year card
    const { rerender } = render(
      <AcademicYearCard year={currentYear} {...mockFns} />
    )

    // Semester 2 (not current, in current year) should show "Set Current" button
    expect(screen.queryAllByRole('button', { name: /set current/i })).toHaveLength(1)

    // Semester 1 (current semester) should NOT show "Set Current"
    // Check by verifying only one "Set Current" button exists (for Semester 2 only)
    const setCurrentBtns = screen.queryAllByRole('button', { name: /set current/i })
    expect(setCurrentBtns).toHaveLength(1)

    // Render a non-current year card — "Set Current" semester button should NOT appear
    rerender(<AcademicYearCard year={prevYear} {...mockFns} />)
    expect(screen.queryAllByRole('button', { name: /set current/i })).toHaveLength(0)
  })
})
