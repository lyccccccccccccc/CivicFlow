export const normaliseName = (value: string) => value.trim().replace(/\s+/g, ' ')

export const validateFullName = (value: string) => {
  const name = normaliseName(value)
  return name.length < 2 || name.length > 150 ? 'Full name must be between 2 and 150 characters.' : ''
}

export const validateNewPassword = (value: string) => value.length < 10 || !/[A-Z]/.test(value) || !/[a-z]/.test(value) || !/\d/.test(value) || !/[^A-Za-z0-9]/.test(value)
  ? 'Use at least 10 characters, including uppercase, lowercase, a number and a symbol.' : ''
