import React from 'react';

const variants = {
  primary: "bg-primary-800 text-white hover:bg-primary-900 active:bg-primary-950 shadow-sm hover:shadow focus:ring-primary-800/20 disabled:bg-neutral-200 disabled:text-neutral-500",
  secondary: "bg-white text-neutral-800 border border-neutral-300 hover:bg-neutral-50 active:bg-neutral-100 focus:ring-neutral-200 disabled:bg-neutral-50 disabled:text-neutral-400",
  ghost: "bg-transparent text-neutral-700 hover:bg-neutral-100 active:bg-neutral-200 focus:ring-neutral-200 disabled:text-neutral-400",
  destructive: "bg-danger-500 text-white hover:bg-danger-600 active:bg-danger-600 shadow-sm focus:ring-danger-500/20 disabled:bg-neutral-200",
  link: "bg-transparent text-secondary-600 hover:text-secondary-700 underline underline-offset-4 h-auto p-0 min-h-0 focus:ring-0",
};

const sizes = {
  sm: "h-9 px-3 text-sm rounded-lg min-h-0",
  md: "h-11 px-4 text-[14px] font-medium rounded-xl min-h-touch", // default enterprise
  lg: "h-12 px-6 text-[15px] font-semibold rounded-full min-h-touch",
  icon: "h-11 w-11 p-0 rounded-xl grid place-items-center min-h-touch min-w-touch",
};

export function Button({ 
  variant = "primary", 
  size = "md", 
  loading = false, 
  leftIcon, 
  rightIcon, 
  children, 
  className = "", 
  disabled,
  ...props 
}) {
  const isDisabled = disabled || loading;
  return (
    <button
      className={`
        inline-flex items-center justify-center gap-2 
        font-medium tracking-[-0.01em]
        transition-all duration-200 ease-out
        focus:outline-none focus:ring-4
        active:scale-[0.98]
        disabled:cursor-not-allowed disabled:active:scale-100
        ${variants[variant] || variants.primary}
        ${sizes[size] || sizes.md}
        ${className}
      `}
      disabled={isDisabled}
      aria-busy={loading}
      {...props}
    >
      {loading && (
        <svg className="animate-spin w-4 h-4" viewBox="0 0 24 24" fill="none" aria-hidden="true">
          <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"/>
          <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"/>
        </svg>
      )}
      {!loading && leftIcon && <span className="w-4 h-4 grid place-items-center" aria-hidden="true">{leftIcon}</span>}
      <span>{children}</span>
      {!loading && rightIcon && <span className="w-4 h-4 grid place-items-center" aria-hidden="true">{rightIcon}</span>}
    </button>
  );
}

export default Button;
