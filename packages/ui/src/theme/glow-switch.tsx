import LightbulbIcon from '@mui/icons-material/LightbulbRounded'
import Switch from '@mui/material/Switch'
import { styled, useThemeProps } from '@mui/material/styles'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { isGlowOn, setGlow } from './glow'

export type GlowSwitchProps = Record<string, never>

export type GlowSwitchClassKey = 'root' | 'icon' | 'label' | 'switch'

const name = 'WwGlowSwitch'

interface OwnerState {
  ownerState: GlowSwitchProps
}

const GlowSwitchRoot = styled('label', { name, slot: 'root' })<OwnerState>(({ theme }) => ({
  display: 'inline-flex',
  alignItems: 'center',
  gap: theme.spacing(0.5),
  cursor: 'pointer',
}))

const GlowSwitchIcon = styled(LightbulbIcon, { name, slot: 'icon' })<OwnerState>(({ theme }) => ({
  color: theme.vars.palette.text.secondary,
  '@media (forced-colors: active)': { color: 'ButtonText' },
}))

// The word shows from `md` up; below it the icon and the accessible name are enough.
const GlowSwitchLabel = styled('span', { name, slot: 'label' })<OwnerState>(({ theme }) => ({
  display: 'none',
  color: theme.vars.palette.text.secondary,
  ...theme.typography.body2,
  [theme.breakpoints.up('md')]: { display: 'inline' },
}))

// The track, thumb, focus ring and forced-colours rules come from the theme's `MuiSwitch`.
const GlowSwitchSwitch = styled(Switch, { name, slot: 'switch' })<OwnerState>({})

const GlowSwitch = (inProps: GlowSwitchProps) => {
  const props = useThemeProps({ props: inProps, name })
  const { t } = useTranslation('common')
  const [on, setOn] = useState(isGlowOn)

  return (
    <GlowSwitchRoot ownerState={props}>
      <GlowSwitchIcon ownerState={props} aria-hidden />
      <GlowSwitchLabel ownerState={props}>{t('glow.label')}</GlowSwitchLabel>
      <GlowSwitchSwitch
        ownerState={props}
        size="small"
        checked={on}
        onChange={(_event, checked) => {
          setGlow(checked)
          setOn(checked)
        }}
        slotProps={{ input: { 'aria-label': t('glow.name') } }}
      />
    </GlowSwitchRoot>
  )
}

export default GlowSwitch
