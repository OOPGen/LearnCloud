/** @type {import('tailwindcss').Config} */
export default {
  content: [
    "./index.html",
    "./src/**/*.{js,ts,jsx,tsx}",
  ],
  theme: {
    extend: {
      colors: {
        // Canonical palette per spec - single source of truth
        primary: {
          50: "#F0F3FA",
          100: "#D9E0F2",
          200: "#B8C6E8",
          300: "#8FA4D6",
          400: "#5A94C1", // secondary light per spec
          500: "#307EC0",
          600: "#195699",
          700: "#073A69",
          800: "#0F153A", // MAIN - must meet WCAG AA
          900: "#0B102C",
          950: "#070A1E",
        },
        secondary: {
          50: "#F5EEFA",
          100: "#EBD9F5",
          200: "#D8B7EC",
          300: "#BC92CD",
          400: "#A46DC9",
          500: "#844CAD",
          600: "#5F3F96",
          700: "#3C2C59",
        },
        neutral: {
          0: "#FFFFFF",
          50: "#F8F9FA",
          100: "#F1F3F5",
          200: "#E9ECEF",
          300: "#DEE2E6",
          400: "#B4B6B8",
          500: "#8A8D93",
          600: "#5C5F66",
          700: "#3A3D44",
          800: "#171725",
          900: "#0F0F14",
        },
        success: { 50: "#E8F5E9", 100: "#C8E6C9", 500: "#2E7D32", 600: "#1B5E20" },
        warning: { 50: "#FFF8E1", 100: "#FFECB3", 500: "#B7791F", 600: "#8D6E00" },
        danger: { 50: "#FFEBEE", 100: "#FFCDD2", 500: "#C62828", 600: "#B71C1C" },
      },
      fontFamily: {
        sans: ['Inter', 'system-ui', '-apple-system', 'Segoe UI', 'Roboto', 'sans-serif'],
      },
      minHeight: { touch: '44px' },
      minWidth: { touch: '44px' },
      borderRadius: {
        '4xl': '2rem',
      },
      boxShadow: {
        'glow': '0 0 0 1px rgba(188,146,205,0.25), 0 0 30px rgba(132,76,173,0.35), 0 0 70px rgba(48,126,192,0.25), 0 20px 60px rgba(0,0,0,0.5)',
        'glow-sm': '0 0 0 1px rgba(188,146,205,0.15), 0 0 20px rgba(132,76,173,0.2)',
      },
      keyframes: {
        'fade-in': { '0%': { opacity: '0' }, '100%': { opacity: '1' } },
        'slide-up': { '0%': { transform: 'translateY(8px)', opacity: '0' }, '100%': { transform: 'translateY(0)', opacity: '1' } },
      },
      animation: {
        'fade-in': 'fade-in 0.25s ease-out',
        'slide-up': 'slide-up 0.3s ease-out',
      }
    },
  },
  plugins: [],
}
