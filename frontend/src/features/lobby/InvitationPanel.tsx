import { useState } from 'react'
import { lobbyInvitationUrl, type InvitationLobby } from './invitations.ts'

export function InvitationPanel({ lobby }: { lobby: InvitationLobby }) {
  const url = lobbyInvitationUrl(lobby)
  const [status, setStatus] = useState('')
  const [sharing, setSharing] = useState(false)

  async function copy() {
    try {
      await navigator.clipboard.writeText(url)
      setStatus('Invitation link copied.')
    } catch {
      setStatus('Copy failed. Select and copy the link above.')
    }
  }
  async function copyCode() {
    try {
      await navigator.clipboard.writeText(lobby.lobbyCode)
      setStatus('Lobby code copied.')
    } catch {
      setStatus('Copy failed. Select and copy the code above.')
    }
  }
  async function share() {
    setSharing(true)
    try {
      await navigator.share({ title: 'Join my WordGame lobby', url })
      setStatus('Invitation shared.')
    } catch (error) {
      setStatus(
        typeof error === 'object' &&
          error !== null &&
          'name' in error &&
          error.name === 'AbortError'
          ? 'Sharing cancelled.'
          : 'Sharing failed. You can copy the link instead.',
      )
    } finally {
      setSharing(false)
    }
  }
  return (
    <section
      className="lobby-card party-invite"
      aria-labelledby="invite-heading"
    >
      <h2 id="invite-heading">Invite friends</h2>
      <label htmlFor="invitation-code">Lobby code</label>
      <div className="party-code-row">
        <input
          id="invitation-code"
          className="party-code"
          value={lobby.lobbyCode}
          readOnly
          onFocus={(event) => event.currentTarget.select()}
        />
        <button type="button" onClick={() => void copyCode()}>
          Copy code
        </button>
      </div>
      <label htmlFor="invitation-link">Invitation link</label>
      <input
        id="invitation-link"
        value={url}
        readOnly
        onFocus={(event) => event.currentTarget.select()}
      />
      <div className="lobby-actions">
        <button type="button" onClick={() => void copy()}>
          Copy invitation link
        </button>
        {typeof navigator.share === 'function' && (
          <button type="button" disabled={sharing} onClick={() => void share()}>
            Share invitation
          </button>
        )}
      </div>
      <p role="status">{status}</p>
    </section>
  )
}
