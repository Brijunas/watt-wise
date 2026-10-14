import TextField, { type TextFieldProps } from '@mui/material/TextField'
import { decodeValidationMessage } from '@wattwise/core'
import { useController, type Control, type FieldPath, type FieldValues } from 'react-hook-form'
import { useTranslation } from 'react-i18next'

export type FormTextFieldProps<Values extends FieldValues> = Omit<
  TextFieldProps,
  'name' | 'value' | 'defaultValue' | 'onChange' | 'onBlur' | 'error' | 'helperText' | 'inputRef'
> & {
  control: Control<Values>
  name: FieldPath<Values>
  label: string
}

// A MUI text field bound to a react-hook-form field. The error message is an encoded validation
// key (see @wattwise/core), translated here so it follows the current language.
const FormTextField = <Values extends FieldValues>({
  control,
  name,
  label,
  fullWidth = true,
  ...rest
}: FormTextFieldProps<Values>) => {
  const { t } = useTranslation('validation')
  const { field, fieldState } = useController({ control, name })

  const message =
    fieldState.error === undefined ? undefined : decodeValidationMessage(fieldState.error.message)

  return (
    <TextField
      {...rest}
      name={field.name}
      value={field.value ?? ''}
      onChange={field.onChange}
      onBlur={field.onBlur}
      inputRef={field.ref}
      label={label}
      fullWidth={fullWidth}
      error={message !== undefined}
      helperText={message === undefined ? undefined : t(message.key, message.params)}
    />
  )
}

export default FormTextField
