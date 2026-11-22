import CheckRoundedIcon from '@mui/icons-material/CheckRounded'
import IconButton from '@mui/material/IconButton'
import ListItemIcon from '@mui/material/ListItemIcon'
import Menu from '@mui/material/Menu'
import MenuItem from '@mui/material/MenuItem'
import { styled, useThemeProps } from '@mui/material/styles'
import { useId, useState, type ReactNode } from 'react'

export interface IconMenuOption<Key extends string> {
  key: Key
  selected: boolean
  content: ReactNode
  lang?: string
}

export interface IconMenuProps<Key extends string> {
  label: string
  icon: ReactNode
  options: readonly IconMenuOption<Key>[]
  onSelect: (key: Key) => void
  /** Exists so the switches can wrap IconMenu with styled(). */
  className?: string
}

export type IconMenuClassKey = 'root' | 'menu'

// The props every option type shares, so one owner state serves every `Key`.
interface OwnerState {
  label: string
  icon: ReactNode
  className?: string
}

const name = 'WwIconMenu'

const IconMenuRoot = styled(IconButton, { name, slot: 'root' })<{
  ownerState: OwnerState
}>({})

const IconMenuMenu = styled(Menu, { name, slot: 'menu' })<{
  ownerState: OwnerState
}>({})

// An icon button that opens a menu of radio items; choosing an item selects it and closes the menu.
const IconMenu = <Key extends string>(inProps: IconMenuProps<Key>) => {
  const props = useThemeProps({ props: inProps, name })
  const { label, icon, options, onSelect, className } = props
  const menuId = useId()
  const [anchor, setAnchor] = useState<HTMLElement | null>(null)

  const open = anchor !== null

  return (
    <>
      <IconMenuRoot
        className={className}
        ownerState={props}
        color="inherit"
        aria-label={label}
        aria-haspopup="menu"
        aria-expanded={open}
        aria-controls={open ? menuId : undefined}
        onClick={(event) => {
          setAnchor(event.currentTarget)
        }}
      >
        {icon}
      </IconMenuRoot>
      <IconMenuMenu
        ownerState={props}
        id={menuId}
        anchorEl={anchor}
        open={open}
        onClose={() => {
          setAnchor(null)
        }}
      >
        {options.map((option) => (
          <MenuItem
            key={option.key}
            role="menuitemradio"
            lang={option.lang}
            aria-checked={option.selected}
            selected={option.selected}
            onClick={() => {
              onSelect(option.key)
              setAnchor(null)
            }}
          >
            <ListItemIcon>
              {option.selected ? <CheckRoundedIcon fontSize="small" /> : null}
            </ListItemIcon>
            {option.content}
          </MenuItem>
        ))}
      </IconMenuMenu>
    </>
  )
}

export default IconMenu
