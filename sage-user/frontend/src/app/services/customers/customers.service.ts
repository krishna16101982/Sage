import { Injectable, inject } from '@angular/core';
import type { PagedResult } from '../../shared/interfaces/api.interface';
import type { Customer, CustomerInput, CustomerQuery } from '../../shared/interfaces/customer.interface';
import { GlobalRestService, toQuery } from '../rest/global-rest-service';

/** /api/user/customers — shared customers: search and add. */
@Injectable({ providedIn: 'root' })
export class CustomersService {
  private readonly rest = inject(GlobalRestService);
  private readonly base = '/api/user/customers';

  list = (q: CustomerQuery) => this.rest.get<PagedResult<Customer>>(`${this.base}${toQuery(q)}`);
  get = (id: string) => this.rest.get<Customer>(`${this.base}/${id}`);
  create = (data: CustomerInput) => this.rest.post<Customer>(this.base, data);
}
