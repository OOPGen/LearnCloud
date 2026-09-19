import React, { useEffect, useRef } from 'react';

export function Dialog({ open, onOpenChange, title, description, children, footer, size = "md" }) {
  const overlayRef = useRef(null);
  const contentRef = useRef(null);
  // Callers pass a new onOpenChange on every render. Reading it through a ref keeps the
  // effect below to opening and closing; it used to rerun on every keystroke in the form and
  // move the cursor back to the first field.
  const onOpenChangeRef = useRef(onOpenChange);
  useEffect(() => { onOpenChangeRef.current = onOpenChange; });

  useEffect(() => {
    if (!open) return;

    const prevOverflow = document.body.style.overflow;
    const prevFocus = document.activeElement;
    document.body.style.overflow = 'hidden';

    const handleEsc = (e) => {
      if (e.key === 'Escape') onOpenChangeRef.current?.(false);
    };

    document.addEventListener('keydown', handleEsc);

    // Focus the first focusable element once, when the dialog opens
    const focusable = contentRef.current?.querySelectorAll('button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])');
    if (focusable?.[0]) focusable[0].focus();

    return () => {
      document.body.style.overflow = prevOverflow;
      document.removeEventListener('keydown', handleEsc);
      // Back to where the user was, e.g. the button that opened the dialog
      if (prevFocus instanceof HTMLElement && prevFocus.isConnected) prevFocus.focus();
    };
  }, [open]);

  if (!open) return null;

  const sizeClasses = {
    sm: "max-w-sm",
    md: "max-w-md",
    lg: "max-w-lg",
    xl: "max-w-xl",
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      <div
        ref={overlayRef}
        className="absolute inset-0 bg-primary-950/60 backdrop-blur-sm animate-fade-in"
        onClick={() => onOpenChange?.(false)}
        aria-hidden="true"
      />
      <div
        ref={contentRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby={title ? "dialog-title" : undefined}
        aria-describedby={description ? "dialog-desc" : undefined}
        className={`
          relative w-full bg-white rounded-2xl shadow-2xl border border-neutral-200
          animate-slide-up
          ${sizeClasses[size]}
        `}
      >
        {(title || description) && (
          <div className="p-6 pb-4 border-b border-neutral-100">
            {title && <h2 id="dialog-title" className="text-[18px] font-semibold tracking-[-0.02em] text-neutral-900">{title}</h2>}
            {description && <p id="dialog-desc" className="mt-1.5 text-[14px] text-neutral-600 leading-relaxed">{description}</p>}
          </div>
        )}
        <div className="p-6">
          {children}
        </div>
        {footer && (
          <div className="p-4 bg-neutral-50 rounded-b-2xl border-t border-neutral-100 flex justify-end gap-2">
            {footer}
          </div>
        )}
        <button
          onClick={() => onOpenChange?.(false)}
          className="absolute top-4 right-4 w-8 h-8 rounded-full grid place-items-center text-neutral-400 hover:text-neutral-700 hover:bg-neutral-100 transition"
          aria-label="Close dialog"
        >
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true">
            <line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/>
          </svg>
        </button>
      </div>
    </div>
  );
}

export default Dialog;
