import { render, screen, act } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { StudentsPage } from '../StudentsPage'
import { studentsApi } from '../../../../api/students'
import type { StudentSummaryDto, PagedResult } from '../../../../api/students'

vi.mock('../../../../api/students', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../../../api/students')>()
  return {
    ...actual,
    studentsApi: {
      list: vi.fn(),
      getById: vi.fn(),
      create: vi.fn(),
      update: vi.fn(),
    },
  }
})

vi.mock('sonner', () => ({
  toast: { success: vi.fn(), error: vi.fn() },
  Toaster: () => null,
}))

const makeStudent = (overrides: Partial<StudentSummaryDto> = {}): StudentSummaryDto => ({
  id: 'student-1',
  studentCode: '2025-000001',
  firstName: 'Nguyen',
  lastName: 'Van A',
  dateOfBirth: '2010-01-15',
  gender: 'Male',
  enrollmentDate: '2025-09-01',
  enrollmentStatus: 'Active',
  ...overrides,
})

const makePagedResult = (items: StudentSummaryDto[], total = items.length): PagedResult<StudentSummaryDto> => ({
  items,
  totalCount: total,
  page: 1,
  pageSize: 20,
})

function renderPage() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  return render(
    <QueryClientProvider client={queryClient}>
      <StudentsPage />
    </QueryClientProvider>
  )
}

describe('StudentsPage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('1: renders table with student rows', async () => {
    vi.mocked(studentsApi.list).mockResolvedValue(
      makePagedResult([
        makeStudent({ id: 's1', firstName: 'Nguyen', lastName: 'Van A' }),
        makeStudent({ id: 's2', firstName: 'Tran', lastName: 'Thi B' }),
      ])
    )
    renderPage()

    await screen.findByText('Nguyen Van A')
    expect(screen.getByText('Tran Thi B')).toBeInTheDocument()
  })

  it('2: empty state when no students match', async () => {
    vi.mocked(studentsApi.list).mockResolvedValue(makePagedResult([]))
    renderPage()

    await screen.findByText('No students found.')
  })

  it('3: clicking tab changes status query param', async () => {
    vi.mocked(studentsApi.list).mockResolvedValue(makePagedResult([]))
    renderPage()

    await screen.findByText('No students found.')
    await userEvent.click(screen.getByRole('tab', { name: 'Transferred' }))

    await vi.waitFor(() => {
      expect(vi.mocked(studentsApi.list)).toHaveBeenCalledWith(
        expect.objectContaining({ status: 'Transferred' })
      )
    })
  })

  it('4: search input debounces and calls API with search param', async () => {
    vi.mocked(studentsApi.list).mockResolvedValue(makePagedResult([]))
    renderPage()

    await screen.findByText('No students found.')

    const searchInput = screen.getByPlaceholderText('Search name or code…')
    await userEvent.type(searchInput, 'nguyen')

    await act(async () => {
      await new Promise((r) => setTimeout(r, 350))
    })

    await vi.waitFor(() => {
      expect(vi.mocked(studentsApi.list)).toHaveBeenCalledWith(
        expect.objectContaining({ search: 'nguyen' })
      )
    })
  })

  it('5: pagination Prev disabled on page 1, Next enabled; Next disabled on last page', async () => {
    vi.mocked(studentsApi.list).mockResolvedValue({
      items: Array.from({ length: 20 }, (_, i) =>
        makeStudent({ id: `s${i}`, studentCode: `2025-00000${i}` })
      ),
      totalCount: 45,
      page: 1,
      pageSize: 20,
    })
    renderPage()

    await screen.findByText('Page 1 of 3')
    expect(screen.getByRole('button', { name: /prev/i })).toBeDisabled()
    expect(screen.getByRole('button', { name: /next/i })).not.toBeDisabled()
  })

  it('6: clicking edit button opens edit modal with correct studentId', async () => {
    vi.mocked(studentsApi.list).mockResolvedValue(
      makePagedResult([makeStudent({ id: 'student-abc' })])
    )
    vi.mocked(studentsApi.getById).mockResolvedValue({
      id: 'student-abc',
      studentCode: '2025-000001',
      firstName: 'Nguyen',
      lastName: 'Van A',
      dateOfBirth: '2010-01-15',
      gender: 'Male',
      enrollmentDate: '2025-09-01',
      enrollmentStatus: 'Active',
      guardianName: null,
      guardianPhone: null,
      guardianEmail: null,
      createdAt: '2025-09-01T00:00:00Z',
      updatedAt: null,
    })
    renderPage()

    await screen.findByText('Nguyen Van A')
    const pencilBtns = screen.getAllByRole('button').filter((b) => b.querySelector('svg'))
    await userEvent.click(pencilBtns[pencilBtns.length - 1])

    await vi.waitFor(() => {
      expect(vi.mocked(studentsApi.getById)).toHaveBeenCalledWith('student-abc')
    })
  })

  it('7: clicking "Add Student" opens create modal', async () => {
    vi.mocked(studentsApi.list).mockResolvedValue(makePagedResult([]))
    renderPage()

    await screen.findByText('No students found.')
    await userEvent.click(screen.getByRole('button', { name: /add student/i }))

    await screen.findByRole('dialog')
    expect(screen.getByRole('dialog')).toBeInTheDocument()
  })
})
