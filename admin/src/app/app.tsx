import Box from '@mui/material/Box'
import Typography from '@mui/material/Typography'
import { ModeSwitch } from '@wattwise/ui'

export function App() {
  return (
    <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', p: 2 }}>
      <Typography component="h1" variant="h4">
        Watt-Wise Admin
      </Typography>
      <ModeSwitch />
    </Box>
  )
}
