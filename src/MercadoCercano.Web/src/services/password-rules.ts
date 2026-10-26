import { Validators, type ValidatorFn } from '@angular/forms'

const strongPassword: ValidatorFn = (control) => {
  const value: unknown = control.value
  return typeof value === 'string' && /\p{Lu}/u.test(value) && /\p{Ll}/u.test(value) &&
    /\p{Nd}/u.test(value) && /[^\p{L}\p{Nd}]/u.test(value) && !/\p{White_Space}/u.test(value)
    ? null : { passwordStrength: true }
}
export const passwordValidators = [Validators.required, Validators.minLength(8), Validators.maxLength(32), strongPassword]
export const matchingPasswords: ValidatorFn = (control) =>
  control.get('password')?.value === control.get('confirmation')?.value ? null : { passwordMismatch: true }
