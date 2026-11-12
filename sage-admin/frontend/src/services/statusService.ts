import { apiGet } from "./apiClient";

export interface ServiceStatus {
  service: string;
  database: string;
}

export const getStatus = () => apiGet<ServiceStatus>("/api/admin/status");
