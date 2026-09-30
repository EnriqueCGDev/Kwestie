export interface RegisterRequest {
  readonly email: string;
  readonly password: string;
}

export interface LoginRequest {
  readonly email: string;
  readonly password: string;
}

export interface RegisterResponse {
  readonly userId: string;
}

export interface AuthenticationResponse {
  readonly userId: string;
  readonly accessToken: string;
  readonly accessTokenExpiresAtUtc: string;
  readonly refreshTokenExpiresAtUtc: string;
}
