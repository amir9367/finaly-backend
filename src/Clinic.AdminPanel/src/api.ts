import axios from 'axios'

const TOKEN_KEY = 'clinic_admin_token'

export function getToken(): string | null {
  return localStorage.getItem(TOKEN_KEY)
}

export function setToken(token: string | null): void {
  if (token) localStorage.setItem(TOKEN_KEY, token)
  else localStorage.removeItem(TOKEN_KEY)
}

export const api = axios.create({ baseURL: '/api', timeout: 60_000 })

api.interceptors.request.use((config) => {
  const token = getToken()
  if (token) config.headers.Authorization = `Bearer ${token}`
  return config
})

// Expired/revoked session: drop the stale token so the next navigation lands
// on the login screen instead of a wall of 401s.
api.interceptors.response.use(
  (response) => response,
  (error) => {
    if (axios.isAxiosError(error) && error.response?.status === 401) {
      setToken(null)
    }
    return Promise.reject(error)
  },
)

/** Extracts the API's { error } body into a displayable message. */
export function errMsg(e: unknown): string {
  const anyErr = e as { response?: { data?: { error?: string }; status?: number } }
  return anyErr?.response?.data?.error ?? (e as Error)?.message ?? 'خطای ناشناخته'
}

/** Downloads a protected file as a blob and saves it via the browser. */
export async function downloadFile(path: string, filename: string): Promise<void> {
  const response = await api.get(path, { responseType: 'blob' })
  const url = URL.createObjectURL(response.data as Blob)
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.download = filename
  anchor.click()
  URL.revokeObjectURL(url)
}
