-- 027: indexes for the list, report and lookup queries of both APIs. Safe to run more than once (each step checks first).
-- Each index names the query it serves. Already covered before this script (DDL 013 / 020-026): every PublicId, unique
-- names and codes, refresh / reset token hashes, Sales by seller / manager / customer / renewal date, AuditLogs by date,
-- actor and entity, SaleCancelRequests by sale and one pending request per sale.
-- Searches with a leading wildcard (LIKE '%text%') cannot use an index; with large tables use full-text search instead.
-- SQL Server Express / Standard build these offline: on a busy production database run the script in a quiet period.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- 1. Sales by date, without a seller: Admin Sales list (newest first, date filter), its totals, Summary per person /
--    manager, Dashboard. INCLUDE makes the totals and summary read only this index.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Sales') AND name = N'IX_Sales_CreatedAtUtc')
    CREATE NONCLUSTERED INDEX IX_Sales_CreatedAtUtc ON dbo.Sales (CreatedAtUtc DESC)
        INCLUDE (SalesUserId, ManagerId, CustomerId, Status, Quantity, ListAmount, DiscountAmount, SaleAmount, CostAmount);
GO

-- 2. My sales / Team sales (User Panel) and the Admin list filtered by seller or manager: the totals (count, sale amount,
--    margin) are read from the index instead of looking up every sale row.
IF NOT EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
               JOIN sys.indexes i ON i.object_id = ic.object_id AND i.index_id = ic.index_id
               WHERE i.object_id = OBJECT_ID(N'dbo.Sales') AND i.name = N'IX_Sales_SalesUser_CreatedAtUtc' AND c.name = N'SaleAmount')
    CREATE NONCLUSTERED INDEX IX_Sales_SalesUser_CreatedAtUtc ON dbo.Sales (SalesUserId, CreatedAtUtc DESC)
        INCLUDE (Status, SaleAmount, CostAmount, CustomerId) WITH (DROP_EXISTING = ON);
GO
IF NOT EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
               JOIN sys.indexes i ON i.object_id = ic.object_id AND i.index_id = ic.index_id
               WHERE i.object_id = OBJECT_ID(N'dbo.Sales') AND i.name = N'IX_Sales_Manager_CreatedAtUtc' AND c.name = N'SaleAmount')
    CREATE NONCLUSTERED INDEX IX_Sales_Manager_CreatedAtUtc ON dbo.Sales (ManagerId, CreatedAtUtc DESC)
        INCLUDE (SalesUserId, Status, SaleAmount, CostAmount, CustomerId) WITH (DROP_EXISTING = ON);
GO

-- 3. "Active sales" count shown for every customer in both Customers lists: (CustomerId, Status) answers it from the index.
IF NOT EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
               JOIN sys.indexes i ON i.object_id = ic.object_id AND i.index_id = ic.index_id
               WHERE i.object_id = OBJECT_ID(N'dbo.Sales') AND i.name = N'IX_Sales_CustomerId' AND c.name = N'Status')
    CREATE NONCLUSTERED INDEX IX_Sales_CustomerId ON dbo.Sales (CustomerId, Status) WITH (DROP_EXISTING = ON);
GO

-- 4. Audit log filtered by action (newest first).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.AuditLogs') AND name = N'IX_AuditLogs_Action_CreatedAtUtc')
    CREATE NONCLUSTERED INDEX IX_AuditLogs_Action_CreatedAtUtc ON dbo.AuditLogs (Action, CreatedAtUtc DESC);
GO

-- 5. Audit log filtered by one admin (newest first) and My Profile's recent activity.
IF NOT EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
               JOIN sys.indexes i ON i.object_id = ic.object_id AND i.index_id = ic.index_id
               WHERE i.object_id = OBJECT_ID(N'dbo.AuditLogs') AND i.name = N'IX_AuditLogs_ActorUserId' AND c.name = N'CreatedAtUtc')
    CREATE NONCLUSTERED INDEX IX_AuditLogs_ActorUserId ON dbo.AuditLogs (ActorUserId, CreatedAtUtc DESC) WITH (DROP_EXISTING = ON);
GO

-- 6. My team (User Panel): each member's pending cancel requests.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.SaleCancelRequests') AND name = N'IX_SaleCancelRequests_Requester_Pending')
    CREATE NONCLUSTERED INDEX IX_SaleCancelRequests_Requester_Pending ON dbo.SaleCancelRequests (RequestedBySalesUserId)
        INCLUDE (SaleId) WHERE Status = 'Pending';
GO

-- 7. Customers list sorted by city or by date added (only active customers are listed).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Customers') AND name = N'IX_Customers_City')
    CREATE NONCLUSTERED INDEX IX_Customers_City ON dbo.Customers (City, Name) WHERE IsActive = 1;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Customers') AND name = N'IX_Customers_CreatedAtUtc')
    CREATE NONCLUSTERED INDEX IX_Customers_CreatedAtUtc ON dbo.Customers (CreatedAtUtc DESC) WHERE IsActive = 1;
GO
