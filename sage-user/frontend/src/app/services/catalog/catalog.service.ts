import { Injectable, inject } from '@angular/core';
import type { CatalogCategory, SalesRules } from '../../shared/interfaces/catalog.interface';
import { GlobalRestService } from '../rest/global-rest-service';

/** /api/user/catalog — what can be sold (active categories > products > plans with price and cost). */
@Injectable({ providedIn: 'root' })
export class CatalogService {
  private readonly rest = inject(GlobalRestService);

  /** fiscalYear (2027 = FY27): seat prices of that year instead of this one (editing an order keeps its year). */
  catalog = (fiscalYear?: number) => this.rest.get<CatalogCategory[]>(`/api/user/catalog${fiscalYear ? `?fiscalYear=${fiscalYear}` : ''}`);
  /** Maximum discount, backdating limit and sale number prefix (company settings). */
  rules = () => this.rest.get<SalesRules>('/api/user/catalog/rules');
}
