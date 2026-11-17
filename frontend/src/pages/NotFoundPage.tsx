import { Link } from 'react-router'

export function NotFoundPage() {
  return (
    <main className="page">
      <h1>Page not found</h1>
      <p>We couldn't find that page.</p>
      <Link to="/">Back to homepage</Link>
    </main>
  )
}
