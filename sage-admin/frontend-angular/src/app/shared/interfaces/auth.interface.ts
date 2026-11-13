/** Mirrors the API's CurrentUserResponse. Permissions come from SQL, never hardcoded in the UI. */
export interface CurrentUser {
  id: string;
  username: string;
  email: string;
  firstName: string;
  lastName: string;
  isSuperAdmin: boolean;
  mustChangePassword: boolean;
  permissions: string[]; // e.g. "Product.View"
  modules: { code: string; name: string }[];
  /** The sidebar, from the Modules table: modules with a menu link that this user can view, in sort order. */
  menu: MenuItem[];
}

export interface MenuItem {
  code: string;
  name: string;
  category: string | null;
  /** e.g. '/products' */
  path: string;
}

/** Body of POST /api/admin/auth/login and /refresh-token. The refresh token is an HttpOnly cookie, not in the body. */
export interface LoginResponse {
  accessToken: string;
  expiresIn: number;
  user: CurrentUser;
}
