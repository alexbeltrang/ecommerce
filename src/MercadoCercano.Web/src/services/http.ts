type ProblemDetails = {
  title?: string
  detail?: string
  errors?: Record<string, string[]>
}

export class ApiError extends Error {
  readonly status: number

  constructor(
    message: string,
    status: number,
  ) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

export async function apiRequest<T>(
  path: string,
  options: RequestInit = {},
): Promise<T> {
  const headers = new Headers(options.headers)
  headers.set('Accept', 'application/json')
  if (options.body !== undefined && !(options.body instanceof FormData) && !headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json')
  }

  const response = await fetch(path, {
    ...options,
    cache: 'no-store',
    credentials: 'same-origin',
    headers,
  })

  await ensureSuccess(response)
  if (response.status === 204) {
    return undefined as T
  }
  return (await response.json()) as T
}

async function ensureSuccess(response: Response): Promise<void> {
  if (!response.ok) {
    let message = response.status === 401
      ? 'Debes iniciar sesión para continuar.'
      : response.status === 403
        ? 'No tienes permiso para realizar esta acción.'
        : `La solicitud falló (HTTP ${response.status}).`

    if (response.headers.get('Content-Type')?.includes('json')) {
      const problem = (await response.json()) as ProblemDetails
      const validation = problem.errors ? Object.values(problem.errors).flat().join(' ') : undefined
      message = problem.detail ?? validation ?? problem.title ?? message
    }

    throw new ApiError(message, response.status)
  }

}

export async function downloadRequest(path: string, fileName: string): Promise<void> {
  const response = await fetch(path, { cache: 'no-store', credentials: 'same-origin' })
  await ensureSuccess(response)
  const blob = await response.blob()
  const extension = new Map([['image/png', '.png'], ['image/jpeg', '.jpg'], ['image/webp', '.webp']]).get(blob.type)
  if (!extension) throw new Error('El servidor devolvió un formato de comprobante no admitido.')
  saveDownload(blob, fileName + extension)
}

export async function downloadCsvRequest(path: string, fileName: string): Promise<void> {
  const response = await fetch(path, { cache: 'no-store', credentials: 'same-origin' })
  await ensureSuccess(response)
  const blob = await response.blob()
  if (blob.type.split(';')[0] !== 'text/csv') throw new Error('El servidor no devolvió un archivo CSV.')
  saveDownload(blob, fileName)
}

function saveDownload(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = fileName
  try { link.click() }
  finally { setTimeout(() => URL.revokeObjectURL(url), 1000) }
}

export function errorMessage(error: unknown): string {
  return error instanceof Error ? error.message : 'No fue posible completar la operación.'
}
