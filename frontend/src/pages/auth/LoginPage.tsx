import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { BookOpen, Eye, EyeOff, Lock, Phone, Shield, User } from 'lucide-react'
import { authApi } from '../../api/auth'
import { useAuthStore } from '../../store/auth.store'
import { Button } from '../../components/ui/button'
import { Input } from '../../components/ui/input'
import { Label } from '../../components/ui/label'

const loginSchema = z.object({
  email: z.string().email('Enter a valid email'),
  password: z.string().min(1, 'Password is required'),
  rememberMe: z.boolean(),
})

type LoginFormValues = z.infer<typeof loginSchema>

export function LoginPage() {
  const [showPwd, setShowPwd] = useState(false)
  const navigate = useNavigate()
  const setUser = useAuthStore((s) => s.setUser)

  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<LoginFormValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: { rememberMe: false },
  })

  const onSubmit = async (values: LoginFormValues) => {
    try {
      const res = await authApi.login(values.email, values.password, values.rememberMe)
      setUser(res.data.user)
      navigate('/dashboard', { replace: true })
    } catch (err: unknown) {
      const status = (err as { response?: { status?: number } })?.response?.status
      if (status === 401) {
        setError('root', { message: 'Invalid email or password.' })
      } else {
        setError('root', { message: 'Something went wrong. Please try again.' })
      }
    }
  }

  return (
    <div className="flex h-screen">

      {/* Left: Brand panel */}
      <div className="relative flex w-[480px] flex-shrink-0 flex-col overflow-hidden bg-primary px-[52px] py-[56px]">
        {/* Decorative rings */}
        <div className="pointer-events-none absolute -right-30 -top-30 h-[380px] w-[380px] rounded-full border border-white/[.06]" />
        <div className="pointer-events-none absolute -right-20 -top-20 h-[260px] w-[260px] rounded-full border border-white/[.04]" />
        <div className="pointer-events-none absolute -bottom-35 -left-20 h-[420px] w-[420px] rounded-full bg-white/[.02]" />

        {/* Logo */}
        <div className="relative z-10 flex items-center gap-[13px]">
          <div className="flex h-[42px] w-[42px] flex-shrink-0 items-center justify-center rounded-[10px] bg-white/[.13]">
            <BookOpen size={20} color="white" strokeWidth={1.75} />
          </div>
          <div>
            <div className="font-heading text-[20px] font-bold leading-none text-white">SchoolMS</div>
            <div className="mt-0.5 text-[11.5px] tracking-[0.02em] text-white/40">Management System</div>
          </div>
        </div>

        {/* Center copy */}
        <div className="relative z-10 flex flex-1 flex-col justify-center">
          <h1 className="mb-3.5 font-heading text-[28px] font-medium leading-[1.4] text-white">
            Managing your school,<br />simplified.
          </h1>
          <p className="mb-[52px] text-[15px] leading-[1.75] text-white/50">
            Student records, attendance, grades, and fee collection — all in one place.
          </p>

          <div className="flex flex-col gap-5">
            <div className="flex items-start gap-3.5">
              <div className="mt-px flex h-[34px] w-[34px] flex-shrink-0 items-center justify-center rounded-[9px] bg-white/[.09]">
                <Shield size={16} color="rgba(255,255,255,0.65)" strokeWidth={1.75} />
              </div>
              <div>
                <div className="mb-[3px] text-[14px] font-semibold text-white/85">Built for your school</div>
                <div className="text-[13px] leading-[1.55] text-white/40">Everything is organized around your institution</div>
              </div>
            </div>

            <div className="flex items-start gap-3.5">
              <div className="mt-px flex h-[34px] w-[34px] flex-shrink-0 items-center justify-center rounded-[9px] bg-white/[.09]">
                <Lock size={16} color="rgba(255,255,255,0.65)" strokeWidth={1.75} />
              </div>
              <div>
                <div className="mb-[3px] text-[14px] font-semibold text-white/85">Secure and private</div>
                <div className="text-[13px] leading-[1.55] text-white/40">Your data is protected and only visible to you</div>
              </div>
            </div>

            <div className="flex items-start gap-3.5">
              <div className="mt-px flex h-[34px] w-[34px] flex-shrink-0 items-center justify-center rounded-[9px] bg-white/[.09]">
                <Phone size={16} color="rgba(255,255,255,0.65)" strokeWidth={1.75} />
              </div>
              <div>
                <div className="mb-[3px] text-[14px] font-semibold text-white/85">Need help?</div>
                <div className="text-[13px] leading-[1.55] text-white/40">Contact your school office for account support</div>
              </div>
            </div>
          </div>
        </div>

        {/* Footer */}
        <div className="relative z-10">
          <p className="text-[12px] text-white/25">Powered by Enclave</p>
        </div>
      </div>

      {/* Right: Form */}
      <div className="flex flex-1 flex-col items-center justify-center bg-white px-16">
        <div className="w-full max-w-[400px]">
          <h2 className="mb-2 font-heading text-[28px] font-semibold leading-[1.2] text-foreground">
            Welcome back
          </h2>
          <p className="mb-9 text-[16px] text-muted-foreground">
            Sign in to your SchoolMS account
          </p>

          <form onSubmit={handleSubmit(onSubmit)} noValidate>
            <div className="flex flex-col gap-5">

              {/* Email */}
              <div className="flex flex-col gap-[7px]">
                <Label htmlFor="email">Email address</Label>
                <div className="relative">
                  <span className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-muted-foreground">
                    <User size={16} strokeWidth={1.75} />
                  </span>
                  <Input
                    id="email"
                    type="email"
                    placeholder="you@school.edu"
                    autoComplete="email"
                    className="pl-10"
                    aria-invalid={!!errors.email}
                    {...register('email')}
                  />
                </div>
                {errors.email && (
                  <p className="text-xs text-destructive">{errors.email.message}</p>
                )}
              </div>

              {/* Password */}
              <div className="flex flex-col gap-[7px]">
                <div className="flex items-center justify-between">
                  <Label htmlFor="password">Password</Label>
                  <span className="text-[13px] text-secondary">Forgot password?</span>
                </div>
                <div className="relative">
                  <span className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-muted-foreground">
                    <Lock size={16} strokeWidth={1.75} />
                  </span>
                  <Input
                    id="password"
                    type={showPwd ? 'text' : 'password'}
                    placeholder="••••••••"
                    autoComplete="current-password"
                    className="pl-10 pr-11"
                    aria-invalid={!!errors.password}
                    {...register('password')}
                  />
                  <button
                    type="button"
                    onClick={() => setShowPwd((v) => !v)}
                    className="absolute right-3 top-1/2 -translate-y-1/2 p-1 text-muted-foreground hover:text-foreground transition-colors"
                    aria-label={showPwd ? 'Hide password' : 'Show password'}
                  >
                    {showPwd ? <EyeOff size={16} strokeWidth={1.75} /> : <Eye size={16} strokeWidth={1.75} />}
                  </button>
                </div>
                {errors.password && (
                  <p className="text-xs text-destructive">{errors.password.message}</p>
                )}
              </div>

              {/* Keep me signed in */}
              <div className="flex items-center gap-[9px]">
                <input
                  id="remember"
                  type="checkbox"
                  className="h-4 w-4 cursor-pointer rounded accent-primary"
                  {...register('rememberMe')}
                />
                <label htmlFor="remember" className="cursor-pointer select-none text-[14px] text-muted-foreground">
                  Keep me signed in
                </label>
              </div>

              {/* Form-level error */}
              {errors.root && (
                <p role="alert" className="text-sm text-destructive">{errors.root.message}</p>
              )}

              {/* Submit */}
              <Button type="submit" className="mt-1 h-12 w-full text-[16px]" disabled={isSubmitting}>
                {isSubmitting ? 'Signing in…' : 'Sign In'}
              </Button>
            </div>
          </form>

          <p className="mt-8 text-center text-[13px] text-muted-foreground/60">
            Need access? Contact your school administrator
          </p>
        </div>
      </div>

    </div>
  )
}
