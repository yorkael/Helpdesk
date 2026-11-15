// Values exactly as the API sends them (UserRole in Helpdesk.Domain).
export type Role = 'Client' | 'Agent' | 'Admin'

export interface CurrentUser {
  id: string
  name: string
  email: string
  role: Role
}

export interface Credentials {
  email: string
  password: string
}

// The login and refresh answers as the API sends them.
export interface AuthTokens {
  accessToken: string
  accessTokenExpiresAt: string
  refreshToken: string
  refreshTokenExpiresAt: string
}

// What the client checks and uses from those answers; the expiry dates are neither read nor checked.
export interface SessionTokens {
  accessToken: string
  refreshToken: string
}
