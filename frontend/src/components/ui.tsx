import { useId, type ComponentProps, type ReactNode } from 'react'

export function LoadingStatus({ children }: { children: ReactNode }) {
  return (
    <p role="status">
      <span className="wg-spinner" aria-hidden="true" /> {children}
    </p>
  )
}

export function Button({
  variant = 'primary',
  busy = false,
  disabled,
  children,
  className = '',
  type = 'button',
  ...props
}: ComponentProps<'button'> & {
  variant?: 'primary' | 'secondary' | 'danger'
  busy?: boolean
}) {
  return (
    <button
      {...props}
      type={type}
      className={`wg-button ${className}`}
      data-variant={variant}
      disabled={disabled || busy}
      aria-busy={busy || undefined}
    >
      {busy && <span className="wg-spinner" aria-hidden="true" />}
      {children}
    </button>
  )
}

export function Card({ className = '', ...props }: ComponentProps<'section'>) {
  return <section {...props} className={`wg-card ${className}`} />
}

export function Badge({
  tone = 'primary',
  className = '',
  ...props
}: ComponentProps<'span'> & {
  tone?: 'primary' | 'success' | 'warning' | 'error'
}) {
  return (
    <span {...props} className={`wg-badge ${className}`} data-tone={tone} />
  )
}

export function TextField({
  label,
  id,
  ...props
}: ComponentProps<'input'> & { label: string }) {
  const generatedId = useId()
  const fieldId = id ?? generatedId
  return (
    <>
      <label htmlFor={fieldId}>{label}</label>
      <input {...props} id={fieldId} />
    </>
  )
}
