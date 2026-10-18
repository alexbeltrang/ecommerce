import { useEffect, useState } from 'react'
import './App.css'

type ApiHealth = {
  status: string
  service: string
}

type ConnectionState =
  | { status: 'loading' }
  | { status: 'connected'; service: string }
  | { status: 'error'; message: string }

function App() {
  const [connection, setConnection] = useState<ConnectionState>({
    status: 'loading',
  })

  useEffect(() => {
    const controller = new AbortController()

    async function checkApi() {
      try {
        const response = await fetch('/api/v1/health', {
          signal: controller.signal,
        })

        if (!response.ok) {
          throw new Error(`La API respondió con el código ${response.status}.`)
        }

        const health = (await response.json()) as ApiHealth
        setConnection({ status: 'connected', service: health.service })
      } catch (error) {
        if (controller.signal.aborted) {
          return
        }

        setConnection({
          status: 'error',
          message:
            error instanceof Error
              ? error.message
              : 'No fue posible conectar con la API.',
        })
      }
    }

    void checkApi()
    return () => controller.abort()
  }, [])

  return (
    <main className="welcome">
      <div className="welcome__card">
        <p className="welcome__eyebrow">TIENDA LOCAL · COLOMBIA</p>
        <h1>Mercado Cercano</h1>
        <p className="welcome__copy">
          Una forma más sencilla de encontrar productos de confianza, cerca de
          ti.
        </p>

        <section className="api-status" aria-labelledby="api-status-title">
          <h2 id="api-status-title">Estado de la plataforma</h2>
          {connection.status === 'loading' && (
            <p role="status">Conectando con los servicios…</p>
          )}
          {connection.status === 'connected' && (
            <p className="api-status__connected" role="status">
              API disponible · {connection.service}
            </p>
          )}
          {connection.status === 'error' && (
            <p className="api-status__error" role="alert">
              API no disponible · {connection.message}
            </p>
          )}
        </section>
      </div>
    </main>
  )
}

export default App
