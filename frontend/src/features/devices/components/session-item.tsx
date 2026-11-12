import Button from '@mui/material/Button'
import Card from '@mui/material/Card'
import CardActions from '@mui/material/CardActions'
import CardContent from '@mui/material/CardContent'
import Chip from '@mui/material/Chip'
import Stack from '@mui/material/Stack'
import Typography from '@mui/material/Typography'
import { useTranslation } from 'react-i18next'
import type { SessionResponse } from '@wattwise/api-client'

export interface SessionItemProps {
  session: SessionResponse
  /** An end-session request for this or another session is running. */
  busy: boolean
  onEnd: (id: string) => void
}

// One signed-in device: its label, the dates, and the button that ends its session.
const SessionItem = ({ session, busy, onEnd }: SessionItemProps) => {
  const { t, i18n } = useTranslation('devices')
  const format = new Intl.DateTimeFormat(i18n.resolvedLanguage, {
    dateStyle: 'medium',
    timeStyle: 'short',
  })

  return (
    <Card component="li" variant="outlined">
      <CardContent>
        <Stack direction="row" spacing={1} sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
          <Typography component="h2" variant="h6">
            {session.deviceLabel}
          </Typography>
          {session.current && <Chip size="small" color="primary" label={t('thisDevice')} />}
        </Stack>
        <Typography variant="body2" color="text.secondary">
          {t('signedInAt', { date: format.format(new Date(session.signedInAt)) })}
        </Typography>
        <Typography variant="body2" color="text.secondary">
          {t('lastUsedAt', { date: format.format(new Date(session.lastUsedAt)) })}
        </Typography>
      </CardContent>
      <CardActions>
        <Button
          disabled={busy}
          aria-label={t('endSessionFor', { device: session.deviceLabel })}
          onClick={() => {
            onEnd(session.id)
          }}
        >
          {t('endSession')}
        </Button>
      </CardActions>
    </Card>
  )
}

export default SessionItem
