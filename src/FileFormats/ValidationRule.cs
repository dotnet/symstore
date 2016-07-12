// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FileFormats
{
    public class ValidationRule
    {
        Func<bool> _checkFunc;
        ValidationRule[] _prereqs;

        public ValidationRule(string errorMessage, Func<bool> checkFunc) : this(errorMessage, checkFunc, null) { }

        public ValidationRule(string errorMessage, Func<bool> checkFunc, params ValidationRule[] prerequisiteValidations)
        {
            ErrorMessage = errorMessage;
            _checkFunc = checkFunc;
            _prereqs = prerequisiteValidations;
        }

        public string ErrorMessage { get; private set; }

        public bool CheckPrerequisites()
        {
            return _prereqs == null || _prereqs.All(v => v.Check());
        }

        public bool Check()
        {
            return CheckPrerequisites() && _checkFunc();
        }

        public void CheckThrowing()
        {
            if (!Check())
            {
                throw new BadInputFormatException(ErrorMessage);
            }
        }
    }
}
