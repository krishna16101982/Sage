/** Mirrors the API's CurrentUserResponse. Permissions come from SQL, never hardcoded in the UI. */
export interface CurrentUser {
  id: string;
  username: string;
  email: string;
  firstName: string;
  lastName: string;
  isSuperAdmin: boolean;
  mustChangePassword: boolean;
  permissions: string[]; // e.g. "Employee.View"
  modules: { code: string; name: string }[];
}

/** Body of POST /api/admin/auth/login and /refresh-token. The refresh token is an HttpOnly cookie, not in the body. */
export interface LoginResponse {
  accessToken: string;
  expiresIn: number;
  user: CurrentUser;
}
