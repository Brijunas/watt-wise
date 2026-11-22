import Visibility from '@mui/icons-material/VisibilityRounded'
import VisibilityOff from '@mui/icons-material/VisibilityOffRounded'
import IconButton from '@mui/material/IconButton'
import InputAdornment from '@mui/material/InputAdornment'
import { styled, useThemeProps } from '@mui/material/styles'
import type { FieldValues } from 'react-hook-form'
import { useState, type ReactElement } from 'react'
import { useTranslation } from 'react-i18next'
import FormTextField, { type FormTextFieldProps } from './form-text-field'

export type FormPasswordFieldProps<Values extends FieldValues> = Omit<
  FormTextFieldProps<Values>,
  'type' | 'slotProps'
>

export type FormPasswordFieldClassKey = 'root' | 'toggleButton'

// The props every `Values` shares, so one owner state serves every form.
interface OwnerState {
  label: string
  visible: boolean
}

const name = 'WwFormPasswordField'

// `styled()` loses the generic call signature, so it is put back with a cast.
const FormPasswordFieldRoot = styled(FormTextField, { name, slot: 'root' })<{
  ownerState: OwnerState
}>({}) as unknown as <Values extends FieldValues>(
  props: FormTextFieldProps<Values> & { ownerState: OwnerState },
) => ReactElement

const FormPasswordFieldToggleButton = styled(IconButton, { name, slot: 'toggleButton' })<{
  ownerState: OwnerState
}>({})

// A form text field for a password, with a button that shows or hides the typed text.
const FormPasswordField = <Values extends FieldValues>(inProps: FormPasswordFieldProps<Values>) => {
  const { autoComplete = 'current-password', ...rest } = useThemeProps({ props: inProps, name })
  const { t } = useTranslation('common')
  const [visible, setVisible] = useState(false)
  const ownerState: OwnerState = { label: rest.label, visible }

  return (
    <FormPasswordFieldRoot
      {...rest}
      ownerState={ownerState}
      autoComplete={autoComplete}
      type={visible ? 'text' : 'password'}
      slotProps={{
        input: {
          endAdornment: (
            <InputAdornment position="end">
              <FormPasswordFieldToggleButton
                ownerState={ownerState}
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
              </FormPasswordFieldToggleButton>
            </InputAdornment>
          ),
        },
      }}
    />
  )
}

export default FormPasswordField
