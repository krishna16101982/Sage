import { Injectable, inject } from '@angular/core';
import type { CatalogCategory } from '../../shared/interfaces/catalog.interface';
import { GlobalRestService } from '../rest/global-rest-service';

/** /api/user/catalog — what can be sold (active categories > products > plans with price and cost). */
@Injectable({ providedIn: 'root' })
export class CatalogService {
  private readonly rest = inject(GlobalRestService);

  catalog = () => this.rest.get<CatalogCategory[]>('/api/user/catalog');
}
