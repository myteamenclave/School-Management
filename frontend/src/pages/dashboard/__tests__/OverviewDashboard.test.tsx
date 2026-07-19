import { render, screen } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter } from 'react-router-dom'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { OverviewDashboard } from '../components/OverviewDashboard'
import { dashboardApi } from '../../../api/dashboard'
import type { DashboardOverviewDto } from '../../../api/dashboard'
import { academicYearsApi } from '../../../api/academicYears'
import type { AcademicYearDto } from '../../../api/academicYears'

vi.mock('../../../api/dashboard', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../../api/dashboard')>()
  return { ...actual, dashboardApi: { overview: vi.fn() } }
})

vi.mock('../../../api/academicYears', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../../api/academicYears')>()
  return { ...actual, academicYearsApi: { list: vi.fn() } }
})

// Recharts needs a real layout box; stub to passthrough divs so jsdom renders cleanly.
vi.mock('recharts', () => {
  const Pass = ({ children }: { children?: React.ReactNode }) => <div>{children}</div>
  const Nil = () => null
  return {
    ResponsiveContainer: Pass,
    BarChart: Pass,
    LineChart: Pass,
    Bar: Nil,
    Line: Nil,
    XAxis: Nil,
    YAxis: Nil,
    CartesianGrid: Nil,
    Tooltip: Nil,
    Legend: Nil,
  }
})

const year: AcademicYearDto = {
  id: 'year-1',
  name: '2025–2026',
  startDate: '2025-09-01',
  endDate: '2026-06-30',
  status: 'Active',
  isCurrent: true,
  semesters: [],
}

function makeOverview(overrides: Partial<DashboardOverviewDto> = {}): DashboardOverviewDto {
  return {
    academicYearId: 'year-1',
    academicYearName: '2025–2026',
    finance: {
      billed: 1000,
      collected: 400,
      outstanding: 600,
      overdue: 300,
      collectionRate: 0.4,
      issuedInvoiceCount: 1,
      draftInvoiceCount: 2,
    },
    financeMonthly: [{ year: 2025, month: 10, billed: 400, collected: 400 }],
    attendanceMonthly: [{ year: 2025, month: 10, totalRecords: 10, presentCount: 9, presentRate: 0.9 }],
    enrollment: {
      totalEnrolled: 24,
      byGrade: [{ gradeId: 'g1', gradeName: 'Grade 5', count: 24 }],
      byStatus: [{ status: 'Active', count: 24 }],
    },
    teachers: {
      teacherCount: 3,
      assignmentCount: 5,
      sectionsWithEnrollments: 2,
      sectionsWithoutAnyTeacher: 1,
      teachersWithoutAssignment: 0,
    },
    ...overrides,
  }
}

function renderDashboard() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <MemoryRouter>
      <QueryClientProvider client={queryClient}>
        <OverviewDashboard displayName="Demo Admin" />
      </QueryClientProvider>
    </MemoryRouter>
  )
}

describe('OverviewDashboard', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(academicYearsApi.list).mockResolvedValue([year])
  })

  it('renders finance KPIs and collection rate from the API', async () => {
    vi.mocked(dashboardApi.overview).mockResolvedValue(makeOverview())
    renderDashboard()

    expect(await screen.findByText('40%')).toBeInTheDocument() // collection rate
    expect(screen.getByText('Collected')).toBeInTheDocument()
    expect(screen.getByText('Billed vs. collected')).toBeInTheDocument()
  })

  it('surfaces the draft-invoice exception line', async () => {
    vi.mocked(dashboardApi.overview).mockResolvedValue(makeOverview())
    renderDashboard()

    expect(await screen.findByText(/draft invoices? not yet issued/)).toBeInTheDocument()
  })

  it('flags a section-without-teacher coverage gap', async () => {
    vi.mocked(dashboardApi.overview).mockResolvedValue(makeOverview())
    renderDashboard()

    expect(await screen.findByText(/section\(s\) with students but no teacher/)).toBeInTheDocument()
  })

  it('shows an empty state when no issued invoices exist for the year', async () => {
    vi.mocked(dashboardApi.overview).mockResolvedValue(
      makeOverview({ financeMonthly: [{ year: 2025, month: 10, billed: 0, collected: 0 }] })
    )
    renderDashboard()

    expect(await screen.findByText('No issued invoices for this year yet.')).toBeInTheDocument()
  })

  it('defaults the year query to the current academic year', async () => {
    vi.mocked(dashboardApi.overview).mockResolvedValue(makeOverview())
    renderDashboard()

    await screen.findByText('40%')
    expect(vi.mocked(dashboardApi.overview)).toHaveBeenCalledWith('year-1')
  })

  it('renders the retry affordance on error', async () => {
    vi.mocked(dashboardApi.overview).mockRejectedValue(new Error('boom'))
    renderDashboard()

    expect(await screen.findByText('Failed to load the dashboard.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Retry' })).toBeInTheDocument()
  })
})
