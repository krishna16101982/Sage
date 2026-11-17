-- Standard wording of the order form (Admin Panel > Settings > Order form wording). The New order form fills these in;
-- a seller may change any of them for one order. Placeholders filled in by the form:
--   {freeMonths}  the order's free months        {gstPercent}  the order's GST %
-- Added only when missing, so wording an admin has changed is kept.
MERGE dbo.Settings AS t
USING (VALUES
    ('OrderForm.SignReturnLine', N'Sign and return to i2k2 Networks Pvt Ltd',
        N'Sign-and-return line', N'First line of the order form.', 10),
    ('OrderForm.SignReturnInstruction', N'Complete the Authorized Signatures section at the end of the order form and return the signed document at earliest.',
        N'Sign-and-return instruction', N'How the customer signs and returns the order.', 20),
    ('OrderForm.OrderNotes', N'This Order Form constitutes a legally binding agreement. Upon execution, the order shall be non-cancellable and non-refundable by the Customer.',
        N'Order notes', N'Notes printed with the order.', 30),
    ('OrderForm.FreeMonthNote', N'This order includes {freeMonths} free months at no cost as a one-time offering.',
        N'Free-month note', N'Added when the order has free months. {freeMonths} becomes the number of free months.', 40),
    ('OrderForm.TaxInformation', N'Tax Information: {gstPercent}% GST will be applicable on the total billing amount. If you are a registered GST business, please provide your GSTIN and we will apply for the appropriate treatment. Please direct any tax-related questions to our billing team.',
        N'Tax information', N'{gstPercent} becomes the order''s GST %.', 50),
    ('OrderForm.SignatureClauses', N'I hereby confirm that I am an authorized signatory empowered to execute this Order Form on behalf of the Customer.
I acknowledge and understand that this Order Form constitutes a legally binding agreement.
Order Cancellation: Cancellation of the order or reduction in the committed quantity during the agreed tenure shall not be permitted.
Force Majeure: Neither party shall be held liable for any delay or failure in performance arising due to events beyond reasonable control, including but not limited to fire, floods, natural calamities, acts of war, public disturbances, strikes, sabotage, governmental actions, laws, regulations, or other force majeure events.
Any discrepancy or error in the Order Form must be reported within 7 days from the date of execution.',
        N'Authorized signature clauses', N'One clause per line.', 60),
    ('OrderForm.Footer', N'Thank you for choosing i2k2 Networks for your business needs.
i2k2 Networks Proprietary and Confidential',
        N'Footer', N'One line per row.', 70)
) AS s (SettingKey, SettingValue, Label, Description, SortOrder)
ON t.SettingKey = s.SettingKey
WHEN NOT MATCHED THEN
    INSERT (SettingKey, SettingValue, ValueType, Category, Label, Description, SortOrder, MaxLength)
    VALUES (s.SettingKey, s.SettingValue, 'string', N'Order form wording', s.Label, s.Description, s.SortOrder, 2000);
GO
