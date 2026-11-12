/** Mirrors SageCommonLib.Models.ApiResponse<T> (camelCase JSON). */
export interface ApiResponse<T> {
  status: boolean;
  httpStatus: string;
  messageType: string;
  message: string[];
  data: T | null;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export class ApiError extends Error {
  constructor(
    public httpStatus: number,
    public messageType: string,
    public messages: string[],
  ) {
    super(messages[0] ?? "Request failed");
  }
}
