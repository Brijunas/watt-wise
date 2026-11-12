import type { FieldValues, Path, UseFormSetError } from 'react-hook-form'
import { encodeValidationMessage, errorCode, errorFieldMessages } from '@wattwise/core'

/**
 * Puts the field errors of a `Validation.Failed` answer on the matching form fields and returns
 * whether any field was marked. The server's texts are English, so the field shows the translated
 * "not valid" text instead. A field the form doesn't have leaves the error to the general message.
 */
const applyFieldErrors = <Values extends FieldValues>(
  error: unknown,
  fields: readonly Path<Values>[],
  setError: UseFormSetError<Values>,
): boolean => {
  if (errorCode(error) !== 'Validation.Failed') {
    return false
  }
  const messages = errorFieldMessages(error)
  let marked = false
  for (const field of fields) {
    if (Object.hasOwn(messages, field)) {
      setError(field, { message: encodeValidationMessage('invalid') })
      marked = true
    }
  }
  return marked
}

export default applyFieldErrors
