using System.Collections.Generic;
using SageAdminApi.Globals;
using SageCommonLib.AppValidations;
using SageCommonLib.Globals;
using SageCommonLib.Models;
using SageCommonLib.Models.Interfaces;

namespace SageAdminApi.AppValidations
{
    /// <summary>
    /// Admin-specific validation built on the shared SageCommonLib validators. Returns IFunctionReturn (Status=false on failure).
    /// </summary>
    public class ApplicationValidationFunctions
    {
        private static IFunctionReturn Invalid(List<string> _messages, string _methodName)
        {
            return new FunctionReturn
            {
                Status = false,
                MessageType = Constants.ReturnMessageTypes.ValidationError,
                Message = _messages,
                HttpStatusCode = Constants.HttpStatusCodes.BadRequest,
                MethodName = _methodName
            };
        }

        /// <summary>
        /// Validate the common admin user fields (username, email, optional mobile).
        /// </summary>
        public static IFunctionReturn ValidateUserIdentity(string _username, string _email, string _mobile, string _methodName)
        {
            var _errors = new List<string>();
            if (string.IsNullOrWhiteSpace(_username)) _errors.Add(ApplicationConstants.ValidationMessages.UsernameRequired);
            else if (!ValidationFunctions.IsValidUsername(_username)) _errors.Add(ApplicationConstants.ValidationMessages.UsernameInvalid);
            if (!ValidationFunctions.IsValidEmail(_email)) _errors.Add(ApplicationConstants.ValidationMessages.EmailInvalid);
            if (!string.IsNullOrWhiteSpace(_mobile) && !ValidationFunctions.IsValidPhone(_mobile)) _errors.Add(ApplicationConstants.ValidationMessages.MobileInvalid);
            return _errors.Count == 0 ? new FunctionReturn { MethodName = _methodName } : Invalid(_errors, _methodName);
        }

        /// <summary>
        /// Validate a new password and its confirmation.
        /// </summary>
        public static IFunctionReturn ValidateNewPassword(string _password, string _confirmPassword, string _methodName)
        {
            var _errors = new List<string>(ValidationFunctions.CheckPasswordStrength(_password));
            if (_password != _confirmPassword) _errors.Add(ApplicationConstants.ValidationMessages.PasswordMismatch);
            return _errors.Count == 0 ? new FunctionReturn { MethodName = _methodName } : Invalid(_errors, _methodName);
        }

        private static void CheckText(List<string> _errors, string _value, int _max, bool _required, string _message)
        {
            string _trimmed = (_value ?? string.Empty).Trim();
            if ((_required && _trimmed.Length == 0) || _trimmed.Length > _max) _errors.Add(_message);
        }

        /// <summary>
        /// Validate a new module (code is immutable afterwards).
        /// </summary>
        /// <summary>A sidebar link such as /products or /reports/sales; empty means "not in the sidebar".</summary>
        public static bool IsValidMenuPath(string _menuPath)
        {
            string _value = (_menuPath ?? string.Empty).Trim();
            return _value.Length == 0 || (_value.Length <= 200 && System.Text.RegularExpressions.Regex.IsMatch(_value, "^/[a-z0-9-]+(/[a-z0-9-]+)*$"));
        }

        public static IFunctionReturn ValidateModule(string _code, string _name, string _category, string _menuPath, string _methodName)
        {
            var _errors = new List<string>();
            if (!ValidationFunctions.IsValidCode(_code)) _errors.Add(ApplicationConstants.ValidationMessages.ModuleCodeInvalid);
            CheckText(_errors, _name, 100, true, ApplicationConstants.ValidationMessages.ModuleNameRequired);
            CheckText(_errors, _category, 100, false, ApplicationConstants.ValidationMessages.CategoryTooLong);
            if (!IsValidMenuPath(_menuPath)) _errors.Add(ApplicationConstants.ValidationMessages.MenuPathInvalid);
            return _errors.Count == 0 ? new FunctionReturn { MethodName = _methodName } : Invalid(_errors, _methodName);
        }

        /// <summary>
        /// Validate module edits (name, category, menu link).
        /// </summary>
        public static IFunctionReturn ValidateModuleUpdate(string _name, string _category, string _menuPath, string _methodName)
        {
            var _errors = new List<string>();
            CheckText(_errors, _name, 100, true, ApplicationConstants.ValidationMessages.ModuleNameRequired);
            CheckText(_errors, _category, 100, false, ApplicationConstants.ValidationMessages.CategoryTooLong);
            if (!IsValidMenuPath(_menuPath)) _errors.Add(ApplicationConstants.ValidationMessages.MenuPathInvalid);
            return _errors.Count == 0 ? new FunctionReturn { MethodName = _methodName } : Invalid(_errors, _methodName);
        }

        /// <summary>
        /// Validate a new permission (action becomes the second half of the Module.Action code).
        /// </summary>
        public static IFunctionReturn ValidatePermission(string _action, string _name, string _description, string _methodName)
        {
            var _errors = new List<string>();
            if (!ValidationFunctions.IsValidCode(_action)) _errors.Add(ApplicationConstants.ValidationMessages.ActionInvalid);
            CheckText(_errors, _name, 150, true, ApplicationConstants.ValidationMessages.PermissionNameRequired);
            CheckText(_errors, _description, 500, false, ApplicationConstants.ValidationMessages.DescriptionTooLong);
            return _errors.Count == 0 ? new FunctionReturn { MethodName = _methodName } : Invalid(_errors, _methodName);
        }

        /// <summary>
        /// Validate permission edits (name, description).
        /// </summary>
        public static IFunctionReturn ValidatePermissionUpdate(string _name, string _description, string _methodName)
        {
            var _errors = new List<string>();
            CheckText(_errors, _name, 150, true, ApplicationConstants.ValidationMessages.PermissionNameRequired);
            CheckText(_errors, _description, 500, false, ApplicationConstants.ValidationMessages.DescriptionTooLong);
            return _errors.Count == 0 ? new FunctionReturn { MethodName = _methodName } : Invalid(_errors, _methodName);
        }

        /// <summary>
        /// Validate a new permission group (code is immutable afterwards).
        /// </summary>
        public static IFunctionReturn ValidateGroup(string _name, string _code, string _description, string _methodName)
        {
            var _errors = new List<string>();
            if (!ValidationFunctions.IsValidCode(_code)) _errors.Add(ApplicationConstants.ValidationMessages.GroupCodeInvalid);
            CheckText(_errors, _name, 100, true, ApplicationConstants.ValidationMessages.GroupNameRequired);
            CheckText(_errors, _description, 500, false, ApplicationConstants.ValidationMessages.DescriptionTooLong);
            return _errors.Count == 0 ? new FunctionReturn { MethodName = _methodName } : Invalid(_errors, _methodName);
        }

        /// <summary>
        /// Validate an admin user's details. Username only on create (pass checkUsername=false to skip).
        /// </summary>
        public static IFunctionReturn ValidateAdminUser(string _firstName, string _lastName, string _username, bool _checkUsername, string _email, string _mobile, string _methodName)
        {
            var _errors = new List<string>();
            CheckText(_errors, _firstName, 100, true, ApplicationConstants.ValidationMessages.FirstNameRequired);
            CheckText(_errors, _lastName, 100, true, ApplicationConstants.ValidationMessages.LastNameRequired);
            if (_checkUsername)
            {
                if (string.IsNullOrWhiteSpace(_username)) _errors.Add(ApplicationConstants.ValidationMessages.UsernameRequired);
                else if (!ValidationFunctions.IsValidUsername(_username)) _errors.Add(ApplicationConstants.ValidationMessages.UsernameInvalid);
            }
            if (!ValidationFunctions.IsValidEmail(_email) || _email.Length > 256) _errors.Add(ApplicationConstants.ValidationMessages.EmailInvalid);
            if (!string.IsNullOrWhiteSpace(_mobile) && !ValidationFunctions.IsValidPhone(_mobile)) _errors.Add(ApplicationConstants.ValidationMessages.MobileInvalid);
            return _errors.Count == 0 ? new FunctionReturn { MethodName = _methodName } : Invalid(_errors, _methodName);
        }

        /// <summary>
        /// Validate a sales user's details and role/manager combination. Username only on create (pass checkUsername=false to skip).
        /// </summary>
        public static IFunctionReturn ValidateSalesUser(string _firstName, string _lastName, string _username, bool _checkUsername, string _email, string _mobile,
            string _role, System.Guid? _managerId, string _methodName)
        {
            var _errors = new List<string>();
            CheckText(_errors, _firstName, 100, true, ApplicationConstants.ValidationMessages.FirstNameRequired);
            CheckText(_errors, _lastName, 100, true, ApplicationConstants.ValidationMessages.LastNameRequired);
            if (_checkUsername)
            {
                if (string.IsNullOrWhiteSpace(_username)) _errors.Add(ApplicationConstants.ValidationMessages.UsernameRequired);
                else if (!ValidationFunctions.IsValidUsername(_username)) _errors.Add(ApplicationConstants.ValidationMessages.UsernameInvalid);
            }
            if (!ValidationFunctions.IsValidEmail(_email) || _email.Length > 256) _errors.Add(ApplicationConstants.ValidationMessages.EmailInvalid);
            if (!string.IsNullOrWhiteSpace(_mobile) && !ValidationFunctions.IsValidPhone(_mobile)) _errors.Add(ApplicationConstants.ValidationMessages.MobileInvalid);
            if (_role == ApplicationDatabaseConstants.SalesRoles.SalesPerson)
            {
                if (!_managerId.HasValue) _errors.Add(ApplicationConstants.ValidationMessages.SalesManagerRequired);
            }
            else if (_role == ApplicationDatabaseConstants.SalesRoles.SalesManager)
            {
                if (_managerId.HasValue) _errors.Add(ApplicationConstants.ValidationMessages.SalesManagerNotAllowed);
            }
            else _errors.Add(ApplicationConstants.ValidationMessages.SalesRoleInvalid);
            return _errors.Count == 0 ? new FunctionReturn { MethodName = _methodName } : Invalid(_errors, _methodName);
        }

        /// <summary>Plan durations allowed by dbo.ProductPlans (CK_ProductPlans_DurationUnit).</summary>
        public static readonly string[] DurationUnits = { "Month", "Quarter", "Year" };

        private const decimal MaxAmount = 9_999_999_999_999_999.99m;

        private static bool IsValidAmount(decimal? _value) => _value.HasValue && _value.Value >= 0 && _value.Value <= MaxAmount && decimal.Round(_value.Value, 2) == _value.Value;

        /// <summary>
        /// Validate a product category (code only on create: pass null to skip it).
        /// </summary>
        public static IFunctionReturn ValidateProductCategory(string _code, bool _checkCode, string _name, string _description, string _methodName)
        {
            var _errors = new List<string>();
            if (_checkCode && !ValidationFunctions.IsValidCode(_code)) _errors.Add(ApplicationConstants.ValidationMessages.CategoryCodeInvalid);
            CheckText(_errors, _name, 100, true, ApplicationConstants.ValidationMessages.CategoryNameRequired);
            CheckText(_errors, _description, 500, false, ApplicationConstants.ValidationMessages.DescriptionTooLong);
            return _errors.Count == 0 ? new FunctionReturn { MethodName = _methodName } : Invalid(_errors, _methodName);
        }

        /// <summary>
        /// Validate a product (code only on create).
        /// </summary>
        public static IFunctionReturn ValidateProduct(long _categoryId, string _code, bool _checkCode, string _name, string _description, string _methodName)
        {
            var _errors = new List<string>();
            if (_categoryId <= 0) _errors.Add(ApplicationConstants.ValidationMessages.CategoryRequired);
            if (_checkCode && !ValidationFunctions.IsValidCode(_code)) _errors.Add(ApplicationConstants.ValidationMessages.ProductCodeInvalid);
            CheckText(_errors, _name, 100, true, ApplicationConstants.ValidationMessages.ProductNameRequired);
            CheckText(_errors, _description, 500, false, ApplicationConstants.ValidationMessages.DescriptionTooLong);
            return _errors.Count == 0 ? new FunctionReturn { MethodName = _methodName } : Invalid(_errors, _methodName);
        }

        /// <summary>
        /// Validate a plan: name, duration, and price/cost (required, 0 or more, at most 2 decimals).
        /// </summary>
        public static IFunctionReturn ValidateProductPlan(string _name, string _description, string _durationUnit, int _durationCount, decimal? _price, decimal? _cost, string _methodName)
        {
            var _errors = new List<string>();
            CheckText(_errors, _name, 100, true, ApplicationConstants.ValidationMessages.PlanNameRequired);
            CheckText(_errors, _description, 500, false, ApplicationConstants.ValidationMessages.DescriptionTooLong);
            if (System.Array.IndexOf(DurationUnits, _durationUnit) < 0) _errors.Add(ApplicationConstants.ValidationMessages.DurationUnitInvalid);
            if (_durationCount < 1 || _durationCount > 120) _errors.Add(ApplicationConstants.ValidationMessages.DurationCountInvalid);
            if (!IsValidAmount(_price)) _errors.Add(ApplicationConstants.ValidationMessages.PriceInvalid);
            if (!IsValidAmount(_cost)) _errors.Add(ApplicationConstants.ValidationMessages.CostInvalid);
            return _errors.Count == 0 ? new FunctionReturn { MethodName = _methodName } : Invalid(_errors, _methodName);
        }

        /// <summary>
        /// Validate group edits (name, description, status).
        /// </summary>
        public static IFunctionReturn ValidateGroupUpdate(string _name, string _description, string _status, string _methodName)
        {
            var _errors = new List<string>();
            CheckText(_errors, _name, 100, true, ApplicationConstants.ValidationMessages.GroupNameRequired);
            CheckText(_errors, _description, 500, false, ApplicationConstants.ValidationMessages.DescriptionTooLong);
            if (_status != ApplicationDatabaseConstants.Status.Active && _status != ApplicationDatabaseConstants.Status.Inactive)
                _errors.Add(ApplicationConstants.ValidationMessages.GroupStatusInvalid);
            return _errors.Count == 0 ? new FunctionReturn { MethodName = _methodName } : Invalid(_errors, _methodName);
        }
    }
}
