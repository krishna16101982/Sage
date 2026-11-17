-- 031: the order form's wording ("Edit wording by section"), saved on the same Sales row as JSON: sign-and-return line
-- and instruction, order notes, free-month note, extra item, product title / description / period line, tax information,
-- signature clauses and footer. The standard text comes from Settings > Order form wording; a seller may change any
-- section for one order, and this column keeps exactly what that order says. NULL for a sale from the simple page.
IF COL_LENGTH(N'dbo.Sales', N'OrderWording') IS NULL
    ALTER TABLE dbo.Sales ADD OrderWording NVARCHAR(MAX) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Sales_OrderWording_Json')
    ALTER TABLE dbo.Sales ADD CONSTRAINT CK_Sales_OrderWording_Json CHECK (OrderWording IS NULL OR ISJSON(OrderWording) = 1);
GO
