/** Mirrors the User API's CurrentSalesUserResponse. */
export interface CurrentUser {
  id: string;
  username: string;
  email: string;
  firstName: string;
  lastName: string;
  role: 'SalesPerson' | 'SalesManager';
  /** True for a sales manager (they also see their team's sales). */
  isManager: boolean;
  /** The sales person's manager; null for a manager. */
  managerName: string | null;
  mustChangePassword: boolean;
  /** A sales person whose manager lets them ask for sales to be cancelled (managers cancel directly). */
  canRequestCancellation: boolean;
}

/** Body of POST /api/user/auth/login and /refresh-token. The refresh token is an HttpOnly cookie, not in the body. */
export interface LoginResponse {
  accessToken: string;
  expiresIn: number;
  user: CurrentUser;
}
