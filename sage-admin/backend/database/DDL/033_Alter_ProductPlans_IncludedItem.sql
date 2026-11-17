-- 033: order-form text per plan.
--   IncludedItemTitle / IncludedItemDescription: an item that comes with the plan, printed on the User Panel's order form
--   (e.g. LinkedIn "Sales Navigator Team Link Extend Seat"). Optional; the description may use {paidMonths},
--   {freeMonths}, {totalMonths} and {seats}, filled in from the order.
--   Description grows to 1000 characters: it is the plan's product description on the order form (Advanced and
--   Advanced Plus each have their own long text).
IF COL_LENGTH(N'dbo.ProductPlans', N'IncludedItemTitle') IS NULL
    ALTER TABLE dbo.ProductPlans ADD IncludedItemTitle NVARCHAR(200) NULL, IncludedItemDescription NVARCHAR(1000) NULL;
GO

IF COL_LENGTH(N'dbo.ProductPlans', N'Description') < 2000   -- bytes: NVARCHAR(1000)
    ALTER TABLE dbo.ProductPlans ALTER COLUMN Description NVARCHAR(1000) NULL;
GO
