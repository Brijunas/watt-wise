import { useEffect, useRef } from 'react'
import { Navigate } from 'react-router'
import { useSignOutMutation } from '@wattwise/api-client'
import { paths } from '@/config/routes'

// Ends the session of an account without admin access and sends it to the no-access page. The
// sign-out is sent once per instance (the ref), so StrictMode's second effect run sends no second
// request.
const DenyAccess = ({ onDenied }: { onDenied: () => void }) => {
  const [signOut] = useSignOutMutation()
  const sentRef = useRef(false)

  useEffect(() => {
    if (!sentRef.current) {
      sentRef.current = true
      onDenied()
      void signOut()
    }
  }, [onDenied, signOut])

  return <Navigate to={paths.noAccess} replace />
}

export default DenyAccess
