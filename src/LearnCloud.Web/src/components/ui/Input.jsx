import React, { useState } from 'react';

export function Input({ 
  label, 
  id, 
  error, 
  helpText, 
  required, 
  leftIcon, 
  rightIcon, 
  className = "",
  containerClassName = "",
  type = "text",
  ...props 
}) {
  const inputId = id || `input-${label?.toLowerCase().replace(/\s+/g, '-')}`;
  const errorId = error ? `${inputId}-error` : undefined;
  const helpId = helpText ? `${inputId}-help` : undefined;
  
  return (
    <div className={`space-y-1.5 ${containerClassName}`}>
      {label && (
        <label htmlFor={inputId} className="block text-[13px] font-medium text-neutral-700 tracking-[-0.01em]">
          {label}
          {required && <span className="ml-1 text-danger-500" aria-hidden="true">*</span>}
        </label>
      )}
      <div className="relative">
        {leftIcon && (
          <span className="absolute left-3 top-1/2 -translate-y-1/2 text-neutral-400 w-4 h-4 grid place-items-center pointer-events-none" aria-hidden="true">
            {leftIcon}
          </span>
        )}
        <input
          id={inputId}
          type={type}
          aria-invalid={!!error}
          aria-describedby={[errorId, helpId].filter(Boolean).join(' ') || undefined}
          aria-required={required}
          className={`
            w-full h-11 rounded-xl border bg-white
            text-[15px] text-neutral-900 placeholder:text-neutral-400
            transition-all duration-200
            focus:outline-none focus:border-secondary-600 focus:ring-4 focus:ring-secondary-500/20
            disabled:bg-neutral-50 disabled:text-neutral-500 disabled:cursor-not-allowed
            ${leftIcon ? 'pl-10' : 'pl-3.5'}
            ${rightIcon ? 'pr-10' : 'pr-3.5'}
            ${error ? 'border-danger-500 focus:border-danger-500 focus:ring-danger-500/20 bg-danger-50/30' : 'border-neutral-200 hover:border-neutral-300'}
            ${className}
          `}
          {...props}
        />
        {rightIcon && (
          <span className="absolute right-3 top-1/2 -translate-y-1/2 text-neutral-400">
            {rightIcon}
          </span>
        )}
      </div>
      {error && (
        <p id={errorId} role="alert" className="text-[12px] text-danger-600 flex items-start gap-1 animate-slide-up">
          <span aria-hidden="true">•</span>
          <span>{error}</span>
        </p>
      )}
      {helpText && !error && (
        <p id={helpId} className="text-[12px] text-neutral-500 leading-relaxed">
          {helpText}
        </p>
      )}
    </div>
  );
}

export function PasswordInput({ label = "Password", showToggle = true, ...props }) {
  const [show, setShow] = useState(false);
  return (
    <div className="space-y-1.5">
      <Input
        label={label}
        type={show ? "text" : "password"}
        rightIcon={
          showToggle ? (
            <button
              type="button"
              onClick={() => setShow(!show)}
              className="text-neutral-500 hover:text-neutral-700 focus:outline-none focus:text-primary-800 p-1 -m-1 rounded"
              aria-label={show ? "Hide password" : "Show password"}
              tabIndex={-1}
            >
              {show ? (
                <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                  <path d="M9.88 9.88a3 3 0 1 0 4.24 4.24"/><path d="M10.73 5.08A10.94 10.94 0 0 1 12 5c7 0 10 7 10 7a13.16 13.16 0 0 1-1.67 2.68"/><path d="M6.61 6.61A13.526 13.526 0 0 0 2 12s3 7 10 7a9.59 9.59 0 0 0 5.39-1.61"/><line x1="2" y1="2" x2="22" y2="22"/>
                </svg>
              ) : (
                <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                  <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/><circle cx="12" cy="12" r="3"/>
                </svg>
              )}
            </button>
          ) : null
        }
        {...props}
      />
    </div>
  );
}

export default Input;
