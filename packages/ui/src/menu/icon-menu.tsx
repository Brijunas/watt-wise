import IconButton from '@mui/material/IconButton'
import Menu from '@mui/material/Menu'
import MenuItem from '@mui/material/MenuItem'
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
}

// An icon button that opens a menu of radio items; choosing an item selects it and closes the menu.
export function IconMenu<Key extends string>({
  label,
  icon,
  options,
  onSelect,
}: IconMenuProps<Key>) {
  const menuId = useId()
  const [anchor, setAnchor] = useState<HTMLElement | null>(null)

  const open = anchor !== null

  return (
    <>
      <IconButton
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
      </IconButton>
      <Menu
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
            {option.content}
          </MenuItem>
        ))}
      </Menu>
    </>
  )
}
