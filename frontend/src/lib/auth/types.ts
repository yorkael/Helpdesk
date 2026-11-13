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

export interface AuthTokens {
  accessToken: string
  accessTokenExpiresAt: string
  refreshToken: string
  refreshTokenExpiresAt: string
}
