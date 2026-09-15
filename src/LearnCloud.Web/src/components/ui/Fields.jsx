// Small form and feedback pieces shared by the school records pages.

export function SelectField({ label, id, value, onChange, children, error, required, helpText, disabled }) {
  const errorId = error ? `${id}-error` : undefined;
  return (
    <div className="space-y-1.5">
      {label && (
        <label htmlFor={id} className="block text-[13px] font-medium text-neutral-700">
          {label}{required && <span className="ml-1 text-danger-500" aria-hidden="true">*</span>}
        </label>
      )}
      <select
        id={id}
        value={value}
        onChange={onChange}
        disabled={disabled}
        aria-invalid={!!error}
        aria-describedby={errorId}
        className={`w-full h-11 px-3 rounded-xl border bg-white text-[15px] text-neutral-900 focus:outline-none focus:border-secondary-600 focus:ring-4 focus:ring-secondary-500/20 disabled:bg-neutral-50 ${error ? 'border-danger-500' : 'border-neutral-200'}`}
      >
        {children}
      </select>
      {error && <p id={errorId} role="alert" className="text-[12px] text-danger-600">{error}</p>}
      {helpText && !error && <p className="text-[12px] text-neutral-500">{helpText}</p>}
    </div>
  );
}

export function CheckboxField({ label, checked, onChange }) {
  return (
    <label className="flex items-center gap-2 text-[13px] text-neutral-700">
      <input type="checkbox" checked={checked} onChange={e => onChange(e.target.checked)} />
      {label}
    </label>
  );
}

export function Notice({ notice, onDismiss }) {
  if (!notice) return null;
  const success = notice.type === 'success';
  return (
    <div role="status" className={`mb-4 p-3 rounded-xl border text-[13px] flex items-center justify-between ${success ? 'bg-success-50 border-success-100 text-success-600' : 'bg-danger-50 border-danger-100 text-danger-600'}`}>
      <span>{notice.message}</span>
      <button onClick={onDismiss} aria-label="Dismiss" className="ml-3 opacity-60 hover:opacity-100">✕</button>
    </div>
  );
}

export function FormError({ message }) {
  return message ? <p role="alert" className="text-[13px] text-danger-600">{message}</p> : null;
}

export function Badge({ children, tone = 'neutral' }) {
  const tones = {
    neutral: 'bg-neutral-100 text-neutral-700',
    primary: 'bg-primary-100 text-primary-800',
    success: 'bg-success-50 text-success-600',
    warning: 'bg-warning-50 text-warning-600',
    danger: 'bg-danger-50 text-danger-600',
  };
  return <span className={`px-2 py-0.5 rounded-full text-[11px] font-medium whitespace-nowrap ${tones[tone] || tones.neutral}`}>{children}</span>;
}

export function Card({ title, actions, children, className = '' }) {
  return (
    <section className={`rounded-2xl bg-white border border-neutral-200 shadow-sm overflow-hidden ${className}`}>
      {(title || actions) && (
        <div className="px-4 py-3 border-b border-neutral-100 flex flex-wrap items-center justify-between gap-2">
          {title && <h2 className="text-[15px] font-semibold text-neutral-900">{title}</h2>}
          {actions && <div className="flex flex-wrap items-center gap-2">{actions}</div>}
        </div>
      )}
      {children}
    </section>
  );
}

export function LoadState({ loading, error, onRetry, empty, emptyMessage, children }) {
  if (error) {
    return (
      <div className="p-8 text-center">
        <p className="text-[14px] text-danger-600">{error}</p>
        {onRetry && <button onClick={onRetry} className="mt-3 text-[13px] text-secondary-600 underline underline-offset-4">Try again</button>}
      </div>
    );
  }
  if (loading) return <div className="p-8 text-center text-[13px] text-neutral-500" role="status">Loading...</div>;
  if (empty) return <div className="p-10 text-center text-[14px] text-neutral-700">{emptyMessage}</div>;
  return children;
}
