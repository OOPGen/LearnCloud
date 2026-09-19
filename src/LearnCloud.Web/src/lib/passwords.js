// Password rules shared by registration and password reset; the API enforces the same.
export function passwordProblem(password) {
  if (!password) return 'Password is required';
  if (password.length < 8) return 'At least 8 characters';
  if (!/[A-Z]/.test(password) || !/[a-z]/.test(password) || !/[0-9]/.test(password) || !/[^A-Za-z0-9]/.test(password))
    return 'Use upper and lower case letters, a number and a symbol';
  return null;
}
