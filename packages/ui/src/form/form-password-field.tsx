import Visibility from '@mui/icons-material/Visibility'
import VisibilityOff from '@mui/icons-material/VisibilityOff'
import IconButton from '@mui/material/IconButton'
import InputAdornment from '@mui/material/InputAdornment'
import type { FieldValues } from 'react-hook-form'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import FormTextField, { type FormTextFieldProps } from './form-text-field'

export type FormPasswordFieldProps<Values extends FieldValues> = Omit<
  FormTextFieldProps<Values>,
  'type' | 'slotProps'
>

// A form text field for a password, with a button that shows or hides the typed text.
const FormPasswordField = <Values extends FieldValues>({
  autoComplete = 'current-password',
  ...rest
}: FormPasswordFieldProps<Values>) => {
  const { t } = useTranslation('common')
  const [visible, setVisible] = useState(false)

  return (
    <FormTextField
      {...rest}
      autoComplete={autoComplete}
      type={visible ? 'text' : 'password'}
      slotProps={{
        input: {
          endAdornment: (
            <InputAdornment position="end">
              <IconButton
                edge="end"
                aria-label={visible ? t('form.hidePassword') : t('form.showPassword')}
                onClick={() => {
                  setVisible((current) => !current)
                }}
                onMouseDown={(event) => {
                  event.preventDefault()
                }}
              >
                {visible ? <VisibilityOff /> : <Visibility />}
              </IconButton>
            </InputAdornment>
          ),
        },
      }}
    />
  )
}

export default FormPasswordField
