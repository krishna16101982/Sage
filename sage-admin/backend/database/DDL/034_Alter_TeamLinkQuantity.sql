-- 034: quantity of the plan's included item (LinkedIn "Sales Navigator Team Link Extend Seat", e.g. 1000 at INR 0).
--   ProductPlans.IncludedItemQuantity: the standard quantity for the plan (Admin Panel > Products > plan).
--   Sales.IncludedItemQuantity: the quantity on this order (starts as the plan's; the seller may change it). Printed on
--   the order form PDF with unit price and total 0.00.
IF COL_LENGTH(N'dbo.ProductPlans', N'IncludedItemQuantity') IS NULL
    ALTER TABLE dbo.ProductPlans ADD IncludedItemQuantity INT NULL
        CONSTRAINT CK_ProductPlans_IncludedItemQuantity CHECK (IncludedItemQuantity BETWEEN 1 AND 10000000);
GO

IF COL_LENGTH(N'dbo.Sales', N'IncludedItemQuantity') IS NULL
    ALTER TABLE dbo.Sales ADD IncludedItemQuantity INT NULL
        CONSTRAINT CK_Sales_IncludedItemQuantity CHECK (IncludedItemQuantity BETWEEN 1 AND 10000000);
GO
