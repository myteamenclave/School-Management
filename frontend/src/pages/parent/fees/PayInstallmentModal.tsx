import { useEffect, useMemo, useState } from 'react'
import { loadStripe, type Stripe } from '@stripe/stripe-js'
import { Elements, PaymentElement, useElements, useStripe } from '@stripe/react-stripe-js'
import { useMutation } from '@tanstack/react-query'
import { toast } from 'sonner'
import { isAxiosError } from 'axios'
import { CreditCard } from 'lucide-react'
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter,
} from '../../../components/ui/dialog'
import { Button } from '../../../components/ui/button'
import { parentPortalApi, type InitiatePaymentResult } from '../../../api/parentPortal'

function extractError(err: unknown): string {
  if (isAxiosError(err) && err.response?.data?.error) return err.response.data.error
  return 'An unexpected error occurred.'
}

interface PayInstallmentModalProps {
  open: boolean
  childId: string
  installmentId: string
  installmentName: string
  amountLabel: string // pre-formatted currency, e.g. "₱600.00"
  onClose: () => void
  onPaid: () => void
}

// Inner form — rendered only once Stripe Elements is initialised with the client secret.
function PaymentForm({
  childId, paymentId, amountLabel, onPaid, onClose,
}: {
  childId: string
  paymentId: string
  amountLabel: string
  onPaid: () => void
  onClose: () => void
}) {
  const stripe = useStripe()
  const elements = useElements()
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const handleSubmit = async () => {
    if (!stripe || !elements) return
    setSubmitting(true)
    setError(null)

    // Inline confirmation — redirect only if the payment method strictly requires it.
    const { error: stripeError, paymentIntent } = await stripe.confirmPayment({
      elements,
      redirect: 'if_required',
    })

    if (stripeError) {
      setError(stripeError.message ?? 'Payment failed. Please try again.')
      setSubmitting(false)
      return
    }

    if (paymentIntent?.status === 'succeeded') {
      try {
        // Return-path reconcile for instant UI (the webhook is the authoritative backstop).
        await parentPortalApi.confirmPayment(childId, paymentId)
      } catch {
        // Payment succeeded at Stripe; the webhook will still reconcile. Don't alarm the parent.
      }
      toast.success('Payment successful')
      onPaid()
      return
    }

    setError('Payment is still processing. Please check back shortly.')
    setSubmitting(false)
  }

  return (
    <>
      <div className="mt-2">
        <PaymentElement />
        {error && <p className="mt-3 text-sm text-red-600 dark:text-red-400">{error}</p>}
      </div>
      <DialogFooter>
        <Button variant="outline" onClick={onClose} disabled={submitting}>Cancel</Button>
        <Button onClick={handleSubmit} disabled={!stripe || submitting}>
          {submitting ? 'Processing…' : `Pay ${amountLabel}`}
        </Button>
      </DialogFooter>
    </>
  )
}

export function PayInstallmentModal({
  open, childId, installmentId, installmentName, amountLabel, onClose, onPaid,
}: PayInstallmentModalProps) {
  const [init, setInit] = useState<InitiatePaymentResult | null>(null)

  const mutation = useMutation({
    mutationFn: () => parentPortalApi.payInstallment(childId, installmentId),
    onSuccess: setInit,
    onError: (err) => toast.error(extractError(err)),
  })

  // Initiate the payment (create the intent) once when the modal opens.
  useEffect(() => {
    if (open) {
      setInit(null)
      mutation.mutate()
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, installmentId])

  // loadStripe must be called once per publishable key, not on every render.
  const stripePromise = useMemo<Promise<Stripe | null> | null>(
    () => (init ? loadStripe(init.publishableKey) : null),
    [init],
  )

  return (
    <Dialog open={open} onOpenChange={(v) => { if (!v) onClose() }}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <CreditCard size={18} className="text-primary" />
            Pay {installmentName}
          </DialogTitle>
        </DialogHeader>

        {mutation.isError && init === null ? (
          <div className="mt-2 text-sm text-muted-foreground">
            Couldn't start the payment. Please close and try again.
          </div>
        ) : !init || !stripePromise ? (
          <div className="mt-2 text-sm text-muted-foreground">Preparing secure payment…</div>
        ) : (
          <Elements stripe={stripePromise} options={{ clientSecret: init.clientSecret }}>
            <PaymentForm
              childId={childId}
              paymentId={init.paymentId}
              amountLabel={amountLabel}
              onPaid={onPaid}
              onClose={onClose}
            />
          </Elements>
        )}
      </DialogContent>
    </Dialog>
  )
}
